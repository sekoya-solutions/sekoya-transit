<#
.SYNOPSIS
  Turns a clip made by the Editor capture tool into a looping animated WebP for the README (ADR 0036, Issue #352).
.DESCRIPTION
  GitHub shows an animated image from the repository inside a README, but not a video file. This script encodes the
  clip without sound at 12 frames per second and 800 pixels wide (an 8-second clip gives about 3 MB), with the
  portable ffmpeg in the ignored .tools/ffmpeg folder (see docs/capture.md).
.PARAMETER Clip
  The MP4 clip, for example Builds/Capture/delivery-clip.mp4.
.PARAMETER Output
  The WebP file to write; an existing file is replaced.
.EXAMPLE
  powershell -NoProfile -File scripts/capture-to-webp.ps1 -Clip Builds/Capture/delivery-clip.mp4 -Output Builds/Capture/delivery-clip.webp
#>
param(
  [Parameter(Mandatory = $true)][string]$Clip,
  [Parameter(Mandatory = $true)][string]$Output,
  [int]$Width = 800,
  [int]$FramesPerSecond = 12,
  [int]$Quality = 60
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$ffmpeg = Join-Path $repoRoot ".tools\ffmpeg\bin\ffmpeg.exe"
if (-not (Test-Path $ffmpeg)) {
  throw "ffmpeg not found at $ffmpeg. Ask the owner before downloading it (see AGENTS.md and docs/capture.md)."
}

if (-not (Test-Path $Clip)) {
  throw "Clip not found: $Clip"
}

# -y replaces an existing output instead of waiting on an overwrite prompt; -an drops the sound.
& $ffmpeg -v error -y -i $Clip -an -vf "fps=$FramesPerSecond,scale=${Width}:-2:flags=lanczos" `
  -c:v libwebp_anim -loop 0 -quality $Quality -compression_level 6 $Output
if ($LASTEXITCODE -ne 0) {
  throw "ffmpeg failed with exit code $LASTEXITCODE."
}

$size = (Get-Item $Output).Length
Write-Host "Wrote $Output ($([math]::Round($size / 1MB, 1)) MB)."
