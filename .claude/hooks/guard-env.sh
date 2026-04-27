#!/usr/bin/env bash
# Blocks edits to .env files (but allows .env.example).
# Receives tool input JSON on stdin.
input=$(cat)
file_path=$(echo "$input" | python3 -c "import sys,json; print(json.load(sys.stdin).get('file_path',''))")

if [[ "$file_path" == *.env ]] && [[ "$file_path" != *.env.example ]]; then
    echo "ERROR: Refusing to edit .env — it contains live secrets. Edit .env.example instead and let the user copy it." >&2
    exit 1
fi
