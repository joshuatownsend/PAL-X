#!/usr/bin/env bash
# Runs dotnet format on any edited .cs file to enforce .editorconfig rules.
# Receives tool input JSON on stdin.
input=$(cat)
file_path=$(echo "$input" | python3 -c "import sys,json; print(json.load(sys.stdin).get('file_path',''))")

if [[ "$file_path" == *.cs ]]; then
    dotnet format "C:/dev/PAL-X/dotnet/Pal.sln" --include "$file_path" 2>/dev/null || true
fi
