// nistwsq.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"
#include "nistwsq.h"

#include <stdlib.h>

// Limits of the NBIS decoder (MAX_DHT_TABLES and MAX_HUFFCOUNTS_WSQ + 1 in
// include/wsq.h).
#define WSQ_MAX_HUFFMAN_TABLES 8
#define WSQ_MAX_HUFFMAN_VALUES 257

// NBIS does not defend itself against malformed input: a table segment that
// is cut short makes it free the same memory twice; a transform table with a
// zero filter length, a Huffman table id out of range or an image smaller
// than WSQ_MIN_IMAGE_SIZE make it access memory outside its buffers. This check rejects those streams before
// NBIS sees them. It walks the marker segments from SOI to the first SOB:
//   - only known markers, each segment completely inside the buffer
//   - transform table (DTT): non-zero filter lengths that match the segment length
//   - Huffman tables (DHT): valid table ids, code counts that fit the segment
//   - frame header (SOF): present, with a large enough image
// It does not make decoding of arbitrary corrupted data safe.
int wsq_validate_stream(const unsigned char* data, int length, int min_image_size)
{
	if (data == nullptr || length < 4)
		return WSQ_ERR_INVALID_ARGUMENT;
	if (((data[0] << 8) | data[1]) != SOI_WSQ)
		return WSQ_ERR_MALFORMED_STREAM;

	bool frame_header_seen = false;
	int pos = 2;
	for (;;)
	{
		// marker (2 bytes) + segment length (2 bytes, includes itself)
		if (length - pos < 4)
			return WSQ_ERR_MALFORMED_STREAM;
		const int marker = (data[pos] << 8) | data[pos + 1];
		const int segment_length = (data[pos + 2] << 8) | data[pos + 3];
		const unsigned char* segment = data + pos + 4; // first byte after the length
		pos += 2;

		if (marker == SOB_WSQ)
			return frame_header_seen ? 0 : WSQ_ERR_MALFORMED_STREAM;
		if (marker != SOF_WSQ && marker != DTT_WSQ && marker != DQT_WSQ &&
			marker != DHT_WSQ && marker != COM_WSQ)
			return WSQ_ERR_MALFORMED_STREAM;
		if (segment_length < 2 || segment_length > length - pos)
			return WSQ_ERR_MALFORMED_STREAM;

		if (marker == DTT_WSQ)
		{
			// length, high-pass size, low-pass size, then 6 bytes for each
			// coefficient of the first half of both filters
			if (segment_length < 4)
				return WSQ_ERR_MALFORMED_STREAM;
			const int hisz = segment[0];
			const int losz = segment[1];
			if (hisz == 0 || losz == 0 ||
				segment_length != 4 + 6 * ((hisz + 1) / 2 + (losz + 1) / 2))
				return WSQ_ERR_MALFORMED_STREAM;
		}
		else if (marker == DHT_WSQ)
		{
			// One or more tables: table id, 16 code-length counts, then one
			// value per code. NBIS indexes a fixed array with the table id.
			int left = segment_length - 2;
			const unsigned char* table = segment;
			while (left > 0)
			{
				if (left < 1 + 16 || table[0] >= WSQ_MAX_HUFFMAN_TABLES)
					return WSQ_ERR_MALFORMED_STREAM;
				int values = 0;
				for (int i = 1; i <= 16; i++)
					values += table[i];
				if (values > WSQ_MAX_HUFFMAN_VALUES || values > left - 17)
					return WSQ_ERR_MALFORMED_STREAM;
				table += 17 + values;
				left -= 17 + values;
			}
		}
		else if (marker == SOF_WSQ)
		{
			// length, black, white, height (2), width (2), ...
			if (segment_length < 8)
				return WSQ_ERR_MALFORMED_STREAM;
			const int height = (segment[2] << 8) | segment[3];
			const int width = (segment[4] << 8) | segment[5];
			if (width < min_image_size || height < min_image_size)
				return WSQ_ERR_IMAGE_TOO_SMALL;
			frame_header_seen = true;
		}

		pos += segment_length;
	}
}

NISTWSQ_API int __cdecl wsq_encode(
	byte** compressed_data, 
	int* compressed_data_length,
	float bitrate, 
	byte* image_data,
	int width, 
	int height, 
	int depth, 		
	int pixels_per_inch, 
	char* comment_text)
{
	if (compressed_data == nullptr || compressed_data_length == nullptr || image_data == nullptr)
		return WSQ_ERR_INVALID_ARGUMENT;
	if (width < WSQ_MIN_IMAGE_SIZE || height < WSQ_MIN_IMAGE_SIZE)
		return WSQ_ERR_IMAGE_TOO_SMALL;

	return wsq_encode_mem(
		compressed_data,
		compressed_data_length, 
		bitrate, 
		image_data,	
		width, 
		height, 
		depth, 
		pixels_per_inch, 
		comment_text);
}

NISTWSQ_API int __cdecl wsq_decode(
	byte** decompressed_data,
	int* width, 
	int* height, 
	int* depth, 
	int* pixels_per_inch, 
	int* lossy_flag,
	byte* wsq_data, 
	int wsq_data_length)
{
	if (decompressed_data == nullptr)
		return WSQ_ERR_INVALID_ARGUMENT;
	*decompressed_data = nullptr;

	if (int ret = wsq_validate_stream(wsq_data, wsq_data_length, WSQ_MIN_IMAGE_SIZE))
		return ret;

	return wsq_decode_mem(
		decompressed_data, 
		width, 
		height, 
		depth,
		pixels_per_inch, 
		lossy_flag, 
		wsq_data, 
		wsq_data_length);
}

// Returns the comments of a WSQ stream as an array of zero-terminated strings.
// The caller frees every string and the array with free_mem. On error nothing
// is returned and nothing is left allocated.
NISTWSQ_API int __cdecl wsq_get_comments(
		byte*** comments_data,
		int* comments_count,
		byte* wsq_data, 
		int wsq_data_length)
{
	if (comments_data == nullptr || comments_count == nullptr)
		return WSQ_ERR_INVALID_ARGUMENT;
	*comments_data = nullptr;
	*comments_count = 0;

	// Comments are metadata: they can be read from any image size.
	if (int ret = wsq_validate_stream(wsq_data, wsq_data_length, 0))
		return ret;

	byte* cbufptr = wsq_data;
	byte* ebufptr = wsq_data + wsq_data_length;

	unsigned short marker = 0;
	byte** comments = nullptr;
	int count = 0;
	int capacity = 0;

	// SOI, then the first marker
	int ret = getc_marker_wsq(&marker, SOI_WSQ, &cbufptr, ebufptr);
	if (ret == 0)
		ret = getc_marker_wsq(&marker, ANY_WSQ, &cbufptr, ebufptr);

	// Until the Start of Block (SOB)
	while (ret == 0 && marker != SOB_WSQ)
	{
		if (marker == COM_WSQ)
		{
			byte* comment = nullptr;
			ret = getc_comment(&comment, &cbufptr, ebufptr);
			if (ret != 0)
				break;

			if (count == capacity)
			{
				const int new_capacity = capacity == 0 ? 4 : capacity * 2;
				byte** grown = (byte**)realloc(comments, sizeof(byte*) * new_capacity);
				if (grown == nullptr)
				{
					free(comment);
					ret = WSQ_ERR_OUT_OF_MEMORY;
					break;
				}

				comments = grown;
				capacity = new_capacity;
			}

			comments[count++] = comment;
		}
		else
		{
			ret = getc_skip_marker_segment(marker, &cbufptr, ebufptr);
			if (ret != 0)
				break;
		}

		ret = getc_marker_wsq(&marker, ANY_WSQ, &cbufptr, ebufptr);
	}

	if (ret != 0)
	{
		for (int i = 0; i < count; i++)
			free(comments[i]);
		free(comments);
		return ret;
	}

	*comments_count = count;
	*comments_data = comments;
	return 0;
}

NISTWSQ_API void __cdecl free_mem(void* pointer)
{
	free(pointer);
}