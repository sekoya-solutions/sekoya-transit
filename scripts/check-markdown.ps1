#Requires -Version 5.1
<#
.SYNOPSIS
  Run the same Markdown checks as CI (markdownlint-cli2 + lychee) over tracked files.
.DESCRIPTION
  Mirrors .github/workflows/ci.yml job "Markdown lint and link check".
  Prefer tools on PATH; falls back to repo-local .tools installs when present.
#>
$ErrorActionPreference = "Stop"
Set-Location (Resolve-Path (Join-Path $PSScriptRoot ".."))

$repoRoot = (Get-Location).Path
$toolsNode = Join-Path $repoRoot ".tools\node"
$toolsMdLint = Join-Path $repoRoot ".tools\md\node_modules\.bin\markdownlint-cli2.cmd"
if (Test-Path $toolsNode) {
  $env:Path = "$toolsNode;" + $env:Path
}

$markdownlint = Get-Command markdownlint-cli2 -ErrorAction SilentlyContinue
if (-not $markdownlint -and (Test-Path $toolsMdLint)) {
  $markdownlintCmd = $toolsMdLint
} elseif ($markdownlint) {
  $markdownlintCmd = $markdownlint.Source
} else {
  throw "markdownlint-cli2 not found. Install globally, or place a portable Node + npm install under .tools/md (see AGENTS.md)."
}

$lychee = Get-Command lychee -ErrorAction SilentlyContinue
if (-not $lychee) {
  throw "lychee not found on PATH. Install via winget: winget install lycheeverse.lychee"
}

$files = @(git ls-files -- "*.md" "*.markdown")
if ($files.Count -eq 0) {
  Write-Host "No tracked Markdown files."
  exit 0
}

Write-Host "markdownlint-cli2 ($($files.Count) files)..."
& $markdownlintCmd --config .markdownlint-cli2.jsonc @files
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "lychee ($($files.Count) files)..."
& lychee --verbose --no-progress --config .lychee.toml @files
exit $LASTEXITCODE
