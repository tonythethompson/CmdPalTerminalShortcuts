#Requires -Version 5.1

# Generates all MSIX + Run plugin icons from Assets/logo-micro.svg (single source of truth).

$ErrorActionPreference = 'Stop'



$repoRoot = Split-Path -Parent $PSScriptRoot

$assetsDir = Join-Path $repoRoot 'QuickShell\Assets'

$logoMicroSvg = Join-Path $assetsDir 'logo-micro.svg'

$runImagesDir = Join-Path $repoRoot 'QuickShell.Run\Images'

$generatorProject = Join-Path $PSScriptRoot 'LogoAssetGenerator\LogoAssetGenerator.csproj'



if (-not (Test-Path $logoMicroSvg)) {

    throw "Missing micro logo source: $logoMicroSvg"

}



New-Item -ItemType Directory -Force -Path $assetsDir | Out-Null

New-Item -ItemType Directory -Force -Path $runImagesDir | Out-Null



dotnet build $generatorProject | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "LogoAssetGenerator build failed with exit code $LASTEXITCODE"
}

dotnet run --project $generatorProject --no-build -- $logoMicroSvg $assetsDir

if ($LASTEXITCODE -ne 0) {

    throw "LogoAssetGenerator failed with exit code $LASTEXITCODE"

}



# PowerToys Run plugin icons (same micro art; light theme uses the same asset on light rows).

Copy-Item (Join-Path $assetsDir 'StoreLogo.png') (Join-Path $runImagesDir 'quickshell.dark.png') -Force

Copy-Item (Join-Path $assetsDir 'StoreLogo.png') (Join-Path $runImagesDir 'quickshell.light.png') -Force



Write-Host "Quick Shell assets generated from logo-micro.svg:"
Write-Host "  MSIX: $assetsDir"
Write-Host "  Store listing (Partner Center): $(Join-Path $assetsDir 'StoreListing')"
Write-Host "  Run:  $runImagesDir"

