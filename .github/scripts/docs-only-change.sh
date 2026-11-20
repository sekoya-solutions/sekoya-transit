#!/usr/bin/env bash
# Prints "true" when every file changed between two commits is Markdown (.md or .markdown), "false" otherwise.
# Usage: docs-only-change.sh <base commit> <head commit>
# The diff runs from the commits' merge base to the head, like a pull request's "Files changed" tab.
# Any doubt prints "false", so the full CI runs: a missing or all-zero commit (a new branch),
# a commit missing from the clone, a failed diff, or no changed file at all.
set -uo pipefail

base="${1:-}"
head="${2:-}"
zero_sha="0000000000000000000000000000000000000000"

if [[ -z "$base" || -z "$head" || "$base" == "$zero_sha" || "$head" == "$zero_sha" ]]; then
  echo "false"
  exit 0
fi

# --no-renames lists a renamed file under both its old and new paths, so renaming a non-Markdown
# file to .md still counts as a code change.
if ! files="$(git -c core.quotePath=false diff --name-only --no-renames "${base}...${head}" --)"; then
  echo "false"
  exit 0
fi

if [[ -z "$files" ]]; then
  echo "false"
elif grep -qvE '\.(md|markdown)$' <<<"$files"; then
  echo "false"
else
  echo "true"
fi
