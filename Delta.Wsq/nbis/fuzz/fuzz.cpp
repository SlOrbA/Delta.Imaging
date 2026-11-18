// Fuzzer for the nbis wrapper (nistwsq.cpp) and the NBIS WSQ decoder.
//
// Mutates valid WSQ files and feeds every mutation to wsq_decode and
// wsq_get_comments, built with AddressSanitizer. Each mutation runs in its own
// child process, which afterwards decodes the unmodified file again: a decode
// of damaged data must neither crash, hang, nor leave the decoder (which keeps
// its state in globals) unable to decode a valid file.
//
// usage: fuzz [-q] [-j jobs] [-m text] <file.wsq>...
//   -q       print only the first failures of each file
//   -j jobs  number of parallel child processes (default: all processors)
//   -m text  run only the mutations whose name contains text
// exit code: 0 if no mutation failed, 1 otherwise. See run.sh.

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <signal.h>
#include <unistd.h>
#include <sys/wait.h>

#include <map>
#include <string>
#include <vector>

#include "nistwsq.h"

extern "C" { int debug = 0; }   // NBIS progress messages off, as in mingw_shim.cpp

typedef std::vector<unsigned char> Bytes;

static const int TIMEOUT_SECONDS = 15;
static const int EXIT_STATE_DAMAGED = 3;

struct Mutation
{
	std::string name;
	Bytes data;
};

static Bytes load(const char* path)
{
	FILE* f = fopen(path, "rb");
	if (!f) { perror(path); exit(2); }
	Bytes data;
	unsigned char chunk[65536];
	size_t n;
	while ((n = fread(chunk, 1, sizeof chunk, f)) > 0)
		data.insert(data.end(), chunk, chunk + n);
	fclose(f);
	return data;
}

// Small deterministic generator: the same mutations on every run and platform.
static unsigned int rng_state;
static unsigned int rng()
{
	rng_state = rng_state * 1664525u + 1013904223u;
	return rng_state >> 8;
}

// Offset of the first Start Of Block marker, or the file size.
static size_t headers_end(const Bytes& d)
{
	size_t pos = 2;
	while (pos + 4 <= d.size())
	{
		const int marker = (d[pos] << 8) | d[pos + 1];
		if (marker == SOB_WSQ)
			return pos + 5 <= d.size() ? pos + 5 : d.size();
		pos += 2 + ((d[pos + 2] << 8) | d[pos + 3]);
	}
	return d.size();
}

// Offset of the first segment with the given marker in the headers, or 0.
static size_t find_segment(const Bytes& d, int wanted)
{
	size_t pos = 2;
	while (pos + 4 <= d.size())
	{
		const int marker = (d[pos] << 8) | d[pos + 1];
		if (marker == wanted) return pos;
		if (marker == SOB_WSQ) break;
		pos += 2 + ((d[pos + 2] << 8) | d[pos + 3]);
	}
	return 0;
}

// A well-formed transform table segment for the given filter lengths: marker,
// length, the two lengths, then (sign, scale, 4-byte value) per coefficient
// of the two half filters.
static Bytes transform_table(int hisz, int losz, bool random_coefficients)
{
	const int coefficients = (hisz + 1) / 2 + (losz + 1) / 2;
	const int length = 4 + 6 * coefficients;
	Bytes segment;
	segment.push_back((unsigned char)(DTT_WSQ >> 8));
	segment.push_back((unsigned char)DTT_WSQ);
	segment.push_back((unsigned char)(length >> 8));
	segment.push_back((unsigned char)length);
	segment.push_back((unsigned char)hisz);
	segment.push_back((unsigned char)losz);
	for (int i = 0; i < 6 * coefficients; i++)
		segment.push_back(random_coefficients ? (unsigned char)rng() : (unsigned char)(i % 6 == 1 ? 9 : i % 6 == 5 ? 1 : 0));
	return segment;
}

// A well-formed Huffman table segment: marker, length, table id, the number
// of codes of each length 1..16, then one value per code.
static Bytes huffman_table(int table_id, const int counts[16], bool random_values)
{
	int values = 0;
	for (int i = 0; i < 16; i++) values += counts[i];
	const int length = 2 + 1 + 16 + values;
	Bytes segment;
	segment.push_back((unsigned char)(DHT_WSQ >> 8));
	segment.push_back((unsigned char)DHT_WSQ);
	segment.push_back((unsigned char)(length >> 8));
	segment.push_back((unsigned char)length);
	segment.push_back((unsigned char)table_id);
	for (int i = 0; i < 16; i++) segment.push_back((unsigned char)counts[i]);
	for (int i = 0; i < values; i++) segment.push_back(random_values ? (unsigned char)rng() : (unsigned char)i);
	return segment;
}

static bool decode(const Bytes& in, Bytes* pixels)
{
	// A heap copy of exactly the right size, so AddressSanitizer sees every
	// read outside the input.
	unsigned char* copy = (unsigned char*)malloc(in.size() ? in.size() : 1);
	if (!in.empty())
		memcpy(copy, in.data(), in.size());

	unsigned char* out = nullptr;
	int w = 0, h = 0, depth = 0, ppi = 0, lossy = 0;
	const int ret = wsq_decode(&out, &w, &h, &depth, &ppi, &lossy, copy, (int)in.size());
	if (ret == 0)
	{
		if (pixels)
			pixels->assign(out, out + (size_t)w * h);
		else
		{
			// Touch every pixel: the buffer must really be w * h bytes.
			volatile unsigned sum = 0;
			for (size_t i = 0; i < (size_t)w * h; i++) sum += out[i];
		}
		free_mem(out);
	}

	unsigned char** comments = nullptr;
	int count = 0;
	if (wsq_get_comments(&comments, &count, copy, (int)in.size()) == 0)
	{
		for (int i = 0; i < count; i++) free_mem(comments[i]);
		free_mem(comments);
	}

	free(copy);
	return ret == 0;
}

static void add(std::vector<Mutation>& all, const std::string& name, const Bytes& data)
{
	all.push_back(Mutation{ name, data });
}

static std::vector<Mutation> mutations(const Bytes& original)
{
	std::vector<Mutation> all;
	const size_t headers = headers_end(original);
	const size_t size = original.size();
	char name[128];

	// Truncation: every length in the headers, then sampled lengths.
	for (size_t length = 0; length <= headers && length < size; length++)
	{
		snprintf(name, sizeof name, "truncate to %zu", length);
		add(all, name, Bytes(original.begin(), original.begin() + length));
	}
	for (int i = 0; i < 400; i++)
	{
		const size_t length = headers + rng() % (size - headers);
		snprintf(name, sizeof name, "truncate to %zu", length);
		add(all, name, Bytes(original.begin(), original.begin() + length));
	}

	// Every header byte: several replacement values.
	static const int patterns[] = { 0x00, 0xFF, 0x01, 0x80, 0x7F, -1 /* flip all bits */, -2 /* +1 */ };
	for (size_t offset = 0; offset < headers; offset++)
	{
		for (int pattern : patterns)
		{
			Bytes data = original;
			const unsigned char old = data[offset];
			data[offset] = pattern == -1 ? (unsigned char)~old : pattern == -2 ? (unsigned char)(old + 1) : (unsigned char)pattern;
			if (data[offset] == old) continue;
			snprintf(name, sizeof name, "header byte %zu: %02X -> %02X", offset, old, data[offset]);
			add(all, name, data);
		}
	}

	// Compressed data: single bytes, bit flips, bursts, inserted markers.
	for (int i = 0; i < 2000; i++)
	{
		Bytes data = original;
		const size_t offset = headers + rng() % (size - headers);
		const unsigned char old = data[offset];
		data[offset] = (unsigned char)rng();
		snprintf(name, sizeof name, "data byte %zu: %02X -> %02X", offset, old, data[offset]);
		add(all, name, data);
	}
	for (int i = 0; i < 600; i++)
	{
		Bytes data = original;
		const size_t offset = headers + rng() % (size - headers);
		const int bit = rng() % 8;
		data[offset] ^= (unsigned char)(1 << bit);
		snprintf(name, sizeof name, "data bit %zu.%d", offset, bit);
		add(all, name, data);
	}
	for (int i = 0; i < 600; i++)
	{
		Bytes data = original;
		const size_t offset = headers + rng() % (size - headers);
		const size_t length = 1 + rng() % 64;
		const int fill = rng() % 4;
		for (size_t k = offset; k < offset + length && k < size; k++)
			data[k] = fill == 0 ? 0x00 : fill == 1 ? 0xFF : (unsigned char)rng();
		snprintf(name, sizeof name, "data burst %zu+%zu (%s)", offset, length, fill == 0 ? "00" : fill == 1 ? "FF" : "random");
		add(all, name, data);
	}
	static const int markers[] = { SOI_WSQ, EOI_WSQ, SOF_WSQ, SOB_WSQ, DTT_WSQ, DQT_WSQ, DHT_WSQ, DRT_WSQ, COM_WSQ };
	for (int i = 0; i < 450; i++)
	{
		Bytes data = original;
		const size_t offset = headers + rng() % (size - headers - 1);
		const int marker = markers[i % 9];
		data[offset] = (unsigned char)(marker >> 8);
		data[offset + 1] = (unsigned char)marker;
		snprintf(name, sizeof name, "marker %04X at %zu", marker, offset);
		add(all, name, data);
	}

	// Well-formed transform tables with other filter lengths: in place of
	// the original table, and in the middle of the compressed data, where
	// the wrapper's header validation does not see them. Single changed
	// bytes cannot produce these, because the segment length has to match.
	static const int lengths[] = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 16, 31, 32, 33, 64, 127, 128, 254, 255 };
	const size_t table = find_segment(original, DTT_WSQ);
	if (table != 0)
	{
		const size_t table_end = table + 2 + ((original[table + 2] << 8) | original[table + 3]);
		int n = 0;
		for (int hisz : lengths)
		{
			for (int losz : lengths)
			{
				const Bytes segment = transform_table(hisz, losz, n++ % 2 == 0);

				Bytes data(original.begin(), original.begin() + table);
				data.insert(data.end(), segment.begin(), segment.end());
				data.insert(data.end(), original.begin() + table_end, original.end());
				snprintf(name, sizeof name, "transform table with filter lengths %d, %d", hisz, losz);
				add(all, name, data);

				if (n % 3 != 0) continue;
				const size_t offset = headers + rng() % (size - headers);
				data = original;
				data.insert(data.begin() + offset, segment.begin(), segment.end());
				snprintf(name, sizeof name, "transform table with filter lengths %d, %d inserted at %zu", hisz, losz, offset);
				add(all, name, data);
			}
		}
	}

	// Well-formed Huffman tables at and around the largest size NBIS stores
	// (257 values), with valid and with impossible code length counts.
	static const int tables[][16] = {
		{ 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 255 },     // 257 values
		{ 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 255 },     // 256 values
		{ 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 255 },     // 257 values
		{ 255, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },     // 257 one- and two-bit codes
		{ 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },       // complete after one bit
		{ 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 },
		{ 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 },
		{ 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },       // no codes at all
		{ 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16 },
		{ 0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0 },
	};
	const size_t huffman = find_segment(original, DHT_WSQ);
	if (huffman != 0)
	{
		const size_t huffman_end = huffman + 2 + ((original[huffman + 2] << 8) | original[huffman + 3]);
		const int table_id = original[huffman + 4];
		int n = 0;
		for (const auto& counts : tables)
		{
			for (int variant = 0; variant < 4; variant++, n++)
			{
				const Bytes segment = huffman_table(variant < 2 ? table_id : variant == 2 ? 7 : 8, counts, variant % 2 == 1);

				Bytes data(original.begin(), original.begin() + huffman);
				data.insert(data.end(), segment.begin(), segment.end());
				data.insert(data.end(), original.begin() + huffman_end, original.end());
				snprintf(name, sizeof name, "huffman table %d, variant %d", n / 4, variant);
				add(all, name, data);

				const size_t offset = headers + rng() % (size - headers);
				data = original;
				data.insert(data.begin() + offset, segment.begin(), segment.end());
				snprintf(name, sizeof name, "huffman table %d, variant %d inserted at %zu", n / 4, variant, offset);
				add(all, name, data);
			}
		}
	}

	// Frame dimensions: around the smallest supported image, around the
	// limits of the 16-bit types NBIS uses, and far beyond the data.
	static const int dimensions[] = { 0, 1, 32, 33, 34, 255, 256, 1000, 0x7FFF, 0x8000, 0xB505, 0xFFFF };
	const size_t frame = find_segment(original, SOF_WSQ);
	if (frame != 0)
	{
		for (int height : dimensions)
		{
			for (int width : dimensions)
			{
				Bytes data = original;
				data[frame + 6] = (unsigned char)(height >> 8);
				data[frame + 7] = (unsigned char)height;
				data[frame + 8] = (unsigned char)(width >> 8);
				data[frame + 9] = (unsigned char)width;
				snprintf(name, sizeof name, "frame %d x %d", width, height);
				add(all, name, data);
			}
		}
	}

	// Several random bytes anywhere in the file.
	for (int i = 0; i < 1200; i++)
	{
		Bytes data = original;
		const int changes = 2 + rng() % 6;
		for (int k = 0; k < changes; k++)
			data[rng() % size] = (unsigned char)rng();
		snprintf(name, sizeof name, "%d random bytes, case %d", changes, i);
		add(all, name, data);
	}

	return all;
}

// Starts one mutation in a child process.
static pid_t start(const Mutation& m, const Bytes& original, const Bytes& expected)
{
	const pid_t pid = fork();
	if (pid < 0) { perror("fork"); exit(2); }
	if (pid == 0)
	{
		alarm(TIMEOUT_SECONDS);
		decode(m.data, nullptr);

		Bytes again;
		if (!decode(original, &again) || again != expected)
			_exit(EXIT_STATE_DAMAGED);
		_exit(0);
	}

	return pid;
}

// What went wrong in a finished child, or an empty string.
static std::string problem(int status)
{
	if (WIFSIGNALED(status))
		return WTERMSIG(status) == SIGALRM ? "timeout" : std::string("signal ") + strsignal(WTERMSIG(status));
	if (WEXITSTATUS(status) == EXIT_STATE_DAMAGED)
		return "the unmodified file no longer decodes correctly afterwards";
	if (WEXITSTATUS(status) != 0)
		return "memory error (see the AddressSanitizer log)";
	return "";
}

int main(int argc, char** argv)
{
	bool quiet = false;
	int jobs = (int)sysconf(_SC_NPROCESSORS_ONLN);
	const char* only = nullptr;
	int failures = 0, total = 0;
	for (int i = 1; i < argc; i++)
	{
		if (strcmp(argv[i], "-q") == 0) { quiet = true; continue; }
		if (strcmp(argv[i], "-j") == 0 && i + 1 < argc) { jobs = atoi(argv[++i]); continue; }
		if (strcmp(argv[i], "-m") == 0 && i + 1 < argc) { only = argv[++i]; continue; }
		if (jobs < 1) jobs = 1;

		const Bytes original = load(argv[i]);
		Bytes expected;
		if (!decode(original, &expected))
		{
			fprintf(stderr, "%s: does not decode\n", argv[i]);
			return 2;
		}

		rng_state = 12345;
		std::vector<Mutation> all = mutations(original);
		if (only)
		{
			std::vector<Mutation> selected;
			for (const Mutation& m : all)
				if (m.name.find(only) != std::string::npos) selected.push_back(m);
			all.swap(selected);
		}

		std::map<pid_t, size_t> running;
		int failed = 0;
		size_t next = 0;
		while (next < all.size() || !running.empty())
		{
			while (next < all.size() && (int)running.size() < jobs)
			{
				running[start(all[next], original, expected)] = next;
				next++;
			}

			int status = 0;
			const pid_t pid = wait(&status);
			if (pid < 0) { perror("wait"); return 2; }
			const size_t index = running[pid];
			running.erase(pid);

			const std::string what = problem(status);
			if (what.empty()) continue;
			failed++;
			if (!quiet || failed <= 20)
				printf("FAIL %s: %s: %s\n", argv[i], all[index].name.c_str(), what.c_str());
		}

		printf("%s: %zu mutations, %d failed\n", argv[i], all.size(), failed);
		fflush(stdout);
		total += (int)all.size();
		failures += failed;
	}

	printf("total: %d mutations, %d failed\n", total, failures);
	return failures == 0 ? 0 : 1;
}
