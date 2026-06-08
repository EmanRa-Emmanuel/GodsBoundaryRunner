param(
    [Parameter(Mandatory=$true)]
    [string]$InputVideo,
    [string]$OutputPng = "$PSScriptRoot\..\assets\run_spritesheet.png",
    [int]$TargetFps = 24,
    [int]$MaxFrames = 12
)

# Requires ffmpeg and ImageMagick (magick) in PATH.
# Usage: .\make_spritesheet.ps1 -InputVideo C:\path\to\video.mp4 -TargetFps 24 -MaxFrames 8

if (-not (Test-Path $InputVideo)) { Write-Error "Input video not found: $InputVideo"; exit 1 }

$baseTemp = Join-Path -Path $env:TEMP -ChildPath "spritesheet_frames_$([System.Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $baseTemp | Out-Null

try {
    Write-Host "Extracting frames to $baseTemp (up to $MaxFrames frames at $TargetFps fps)"
    $framePattern = Join-Path $baseTemp "frame_%04d.png"
    # Extract frames; limit with -vframes after scaling if desired
    & ffmpeg -y -i "$InputVideo" -vf "fps=$TargetFps,scale=-1:256" -vframes $MaxFrames "$framePattern"
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed with exit code $LASTEXITCODE" }

    # Collect frames
    $frames = Get-ChildItem -Path $baseTemp -Filter "frame_*.png" | Sort-Object Name
    if ($frames.Count -eq 0) { throw "No frames extracted" }

    # Create assets folder
    $assetsDir = Join-Path $PSScriptRoot "..\assets" | Resolve-Path -ErrorAction SilentlyContinue
    if (-not $assetsDir) { New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot "..\assets") | Out-Null }

    Write-Host "Assembling sprite sheet with ImageMagick (magick)"
    $frameFiles = $frames | ForEach-Object { $_.FullName }
    $args = @($frameFiles + @("+append", "$OutputPng"))
    & magick @args
    if ($LASTEXITCODE -ne 0) { throw "ImageMagick convert failed with exit code $LASTEXITCODE" }

    # Write meta with frame count
    $metaPath = Join-Path (Split-Path $OutputPng -Parent) "run_spritesheet.meta"
    $framesCount = $frames.Count
    Set-Content -Path $metaPath -Value $framesCount -Encoding UTF8

    Write-Host "Created $OutputPng with $framesCount frames. Meta: $metaPath"
}
catch {
    Write-Error $_.Exception.Message
}
finally {
    # Don't delete frames - keep them for inspection
    Write-Host "Temp frames kept at $baseTemp"
}
