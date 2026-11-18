#!/usr/bin/env bash
# Builds the nbis wrapper and the NBIS WSQ codec for Linux with
# AddressSanitizer and fuzzes the decoder with mutated sample files.
#
#   run.sh [--unpatched] [work-dir]
#
# --unpatched skips ../patches, to show what the patches fix.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
nbis_dir="$(dirname "$here")"
patched=1
if [ "${1:-}" = "--unpatched" ]; then patched=0; shift; fi
work="${1:-$(mktemp -d)}"
mkdir -p "$work"

# shellcheck source=../nbis-source.env
. "$nbis_dir/nbis-source.env"

src="$work/nbis-src"
if [ ! -d "$src/.git" ]; then
  git clone --quiet "$NBIS_REPOSITORY" "$src"
fi
git -C "$src" checkout --quiet --force "$NBIS_COMMIT"
git -C "$src" clean --quiet -fdx
if [ "$patched" = 1 ]; then
  shopt -s nullglob
  for patch in "$nbis_dir"/patches/*.patch; do
    git -C "$src" apply --whitespace=nowarn "$patch"
  done
  shopt -u nullglob
fi

flags=(-g -O1 -fsanitize=address -fno-omit-frame-pointer -fcommon -w -D__NBISLE__
       -I"$src/commonnbis/include" -I"$src/imgtools/include")

obj="$work/obj"
rm -rf "$obj"; mkdir -p "$obj"
for lib in commonnbis/src/lib/fet commonnbis/src/lib/ioutil commonnbis/src/lib/util \
           imgtools/src/lib/jpegl imgtools/src/lib/wsq; do
  for file in "$src/$lib"/*.c; do
    gcc "${flags[@]}" -c "$file" -o "$obj/$(echo "$lib" | tr / _)_$(basename "$file").o" &
  done
  wait
done

# The wrapper is Windows code: neutralize the calling convention keywords and
# build a copy, so that it picks up the stdafx.h next to this script instead
# of the Windows one next to it.
wrapper="$work/wrapper"
rm -rf "$wrapper"; mkdir -p "$wrapper"
cp "$nbis_dir/nistwsq.cpp" "$nbis_dir/nistwsq.h" "$here/stdafx.h" "$here/fuzz.cpp" "$wrapper/"
g++ "${flags[@]}" -std=c++17 -D'__declspec(x)=' -D__cdecl= -DNISTWSQ_EXPORTS \
    "$wrapper/fuzz.cpp" "$wrapper/nistwsq.cpp" "$obj"/*.o -lm -o "$work/fuzz"

samples=("$nbis_dir"/../tests/TestApplication/Images/fp.wsq "$nbis_dir"/../images/finger.wsq "$nbis_dir"/../images/nores.wsq)
# allocator_may_return_null: a file may claim an image too large to allocate;
# the decoder has to handle the failed allocation.
ASAN_OPTIONS=detect_leaks=0:abort_on_error=0:exitcode=9:allocator_may_return_null=1 "$work/fuzz" -q "${samples[@]}" 2> "$work/asan.log" && status=0 || status=$?

if [ "$status" != 0 ]; then
  echo "--- distinct AddressSanitizer findings ---"
  grep -a -E "^SUMMARY: AddressSanitizer" "$work/asan.log" | sed "s|$src/||" | sort | uniq -c | sort -rn || true
fi
exit "$status"
