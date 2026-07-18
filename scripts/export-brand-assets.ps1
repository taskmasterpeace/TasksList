param()

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$brandRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'assets\brand'))
$outputRoot = [System.IO.Path]::GetFullPath((Join-Path $brandRoot 'generated'))

if (-not $outputRoot.StartsWith(($brandRoot.TrimEnd('\') + '\'), [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Resolved brand output directory escaped the brand root: $outputRoot"
}

$magick = (Get-Command magick -ErrorAction Stop).Source
$logo = Join-Path $brandRoot 'logo-mark.svg'
$wordmark = Join-Path $brandRoot 'wordmark-horizontal.svg'
$social = Join-Path $brandRoot 'github-social-preview.svg'
foreach ($source in @($logo, $wordmark, $social)) {
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Brand master is missing: $source"
    }
}

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$sizes = @(16, 24, 32, 48, 64, 128, 256, 512)
foreach ($size in $sizes) {
    $target = Join-Path $outputRoot "app-icon-$size.png"
    & $magick -background none -density 384 $logo -resize "${size}x${size}" -strip $target
    if ($LASTEXITCODE -ne 0) { throw "ImageMagick failed to export $target" }
}

$wordmarkTarget = Join-Path $outputRoot 'wordmark-horizontal.png'
& $magick -background none -density 192 $wordmark -resize '1600x420!' -strip $wordmarkTarget
if ($LASTEXITCODE -ne 0) { throw 'ImageMagick failed to export the wordmark.' }

$socialTarget = Join-Path $outputRoot 'github-social-preview.png'
& $magick -background none -density 192 $social -resize '1280x640!' -strip $socialTarget
if ($LASTEXITCODE -ne 0) { throw 'ImageMagick failed to export the GitHub social preview.' }

$icoTarget = Join-Path $outputRoot 'TasksList.ico'
$icoInputs = @(16, 24, 32, 48, 64, 128, 256) | ForEach-Object {
    Join-Path $outputRoot "app-icon-$_.png"
}
& $magick @icoInputs $icoTarget
if ($LASTEXITCODE -ne 0) { throw 'ImageMagick failed to export the Windows icon.' }

$required = @($icoTarget, $wordmarkTarget, $socialTarget) + ($sizes | ForEach-Object {
    Join-Path $outputRoot "app-icon-$_.png"
})
foreach ($asset in $required) {
    if (-not (Test-Path -LiteralPath $asset -PathType Leaf) -or (Get-Item -LiteralPath $asset).Length -eq 0) {
        throw "Brand export is missing or empty: $asset"
    }
}

Write-Host "Task'sList brand assets exported to $outputRoot"
