#include "stdafx.h"
#include <stdio.h>
#include <stdarg.h>

extern "C" {
	// NBIS debug level (extern int debug in wsq.h / jpegl.h). Values > 0 make
	// libwsq print progress messages to stderr on every encode/decode, so keep
	// it off. Genuine "ERROR : ..." messages are printed regardless.
	int debug = 0;
	int __cdecl __mingw_vfprintf(FILE* fileptr, const char* format, va_list arglist);
	int __cdecl __mingw_vfscanf(FILE* fileptr, const char* format, va_list arglist);
	int __cdecl __mingw_vprintf(const char* format, va_list arglist);
	int __cdecl __mingw_vsnprintf(char* dest, size_t count, const char* format, va_list arglist);
	int __cdecl __mingw_vsprintf(char* dest, const char* format, va_list arglist);
	int __cdecl __mingw_vsscanf(const char* source, const char* format, va_list arglist);
	// Variadic wrappers (used by modern MinGW-w64 GCC >= 14)
	int __cdecl __mingw_fprintf(FILE* fileptr, const char* format, ...);
	int __cdecl __mingw_sprintf(char* dest, const char* format, ...);
	int __cdecl __mingw_sscanf(const char* source, const char* format, ...);
}

// The NBIS libraries are compiled with MinGW-w64, whose headers redirect the
// stdio functions to __mingw_* implementations. Forward them to the MSVC CRT.
// The plain (not _s) variants are used on purpose: they have the semantics
// the NBIS code was written for.
#pragma warning(push)
#pragma warning(disable: 4996) // "may be unsafe": the callers own the buffers

int __cdecl __mingw_vfprintf(FILE* fileptr, const char* format, va_list arglist)
{
	return vfprintf(fileptr, format, arglist);
}

int __cdecl __mingw_vfscanf(FILE* fileptr, const char* format, va_list arglist)
{
	return vfscanf(fileptr, format, arglist);
}

int __cdecl __mingw_vprintf(const char* format, va_list arglist)
{
	return vprintf(format, arglist);
}

int __cdecl __mingw_vsnprintf(char* dest, size_t count, const char* format, va_list arglist)
{
	// C99 semantics (always terminates, returns the full length), as MinGW's.
	return vsnprintf(dest, count, format, arglist);
}

int __cdecl __mingw_vsprintf(char* dest, const char* format, va_list arglist)
{
	return vsprintf(dest, format, arglist);
}

int __cdecl __mingw_vsscanf(const char* source, const char* format, va_list arglist)
{
	return vsscanf(source, format, arglist);
}

// ── Variadic wrappers (modern MinGW-w64 GCC >= 14) ───────────────────────────

int __cdecl __mingw_fprintf(FILE* fileptr, const char* format, ...)
{
	va_list args;
	va_start(args, format);
	int result = vfprintf(fileptr, format, args);
	va_end(args);
	return result;
}

int __cdecl __mingw_sprintf(char* dest, const char* format, ...)
{
	va_list args;
	va_start(args, format);
	int result = vsprintf(dest, format, args);
	va_end(args);
	return result;
}

int __cdecl __mingw_sscanf(const char* source, const char* format, ...)
{
	va_list args;
	va_start(args, format);
	int result = vsscanf(source, format, args);
	va_end(args);
	return result;
}

#pragma warning(pop)
