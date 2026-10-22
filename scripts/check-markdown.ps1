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
$toolsLychee = Join-Path $repoRoot ".tools\lychee\lychee.exe"
$toolsMdLintCandidates = @(
  (Join-Path $repoRoot ".tools\md\node_modules\.bin\markdownlint-cli2.cmd"),
  (Join-Path $repoRoot ".tools\node_modules\.bin\markdownlint-cli2.cmd")
)
if (Test-Path $toolsNode) {
  $env:Path = "$toolsNode;" + $env:Path
}
if (Test-Path (Split-Path $toolsLychee -Parent)) {
  $env:Path = "$(Split-Path $toolsLychee -Parent);" + $env:Path
}

$markdownlint = Get-Command markdownlint-cli2 -ErrorAction SilentlyContinue
$toolsMdLint = $toolsMdLintCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $markdownlint -and $toolsMdLint) {
  $markdownlintCmd = $toolsMdLint
} elseif ($markdownlint) {
  $markdownlintCmd = $markdownlint.Source
} else {
  throw "markdownlint-cli2 not found. Ask the owner before installing; preferred layout is portable Node + npm under ignored .tools/ (see AGENTS.md)."
}

$lycheeCmd = $null
$lycheeOnPath = Get-Command lychee -ErrorAction SilentlyContinue
if ($lycheeOnPath) {
  $lycheeCmd = $lycheeOnPath.Source
} elseif (Test-Path $toolsLychee) {
  $lycheeCmd = $toolsLychee
} else {
  throw "lychee not found. Ask the owner before installing; preferred layout is portable .tools/lychee/lychee.exe (see AGENTS.md)."
}

$files = @(git ls-files -- "*.md" "*.markdown")
if ($files.Count -eq 0) {
  Write-Host "No tracked Markdown files."
  exit 0
}

Write-Host "markdownlint-cli2 ($($files.Count) files)..."
& $markdownlintCmd --config .markdownlint-cli2.jsonc @files
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "lychee via $lycheeCmd ($($files.Count) files)..."
& $lycheeCmd --verbose --no-progress --config .lychee.toml @files
exit $LASTEXITCODE
