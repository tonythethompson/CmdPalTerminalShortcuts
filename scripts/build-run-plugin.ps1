param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [ValidateSet('x64', 'ARM64')]
    [string]$Platform = 'x64',
    [switch]$Deploy
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'QuickShell.Run\QuickShell.Run.csproj'
$outputRoot = Join-Path $repoRoot "QuickShell.Run\bin\$Platform\$Configuration\net9.0-windows10.0.22621.0"
$pluginRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\PowerToys\PowerToys Run\Plugins\QuickShell'
$stagingRoot = Join-Path $repoRoot "QuickShell.Run\bin\$Platform\$Configuration\package"
$zipPath = Join-Path $repoRoot "QuickShell.Run\bin\$Platform\$Configuration\QuickShell.Run-$Platform.zip"

Write-Host "Building Quick Shell Run plugin ($Configuration | $Platform)..."
dotnet build $project -c $Configuration -p:Platform=$Platform

if (Test-Path $stagingRoot) {
    Remove-Item $stagingRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $stagingRoot | Out-Null
Copy-Item (Join-Path $outputRoot 'QuickShell.Run.dll') $stagingRoot
Copy-Item (Join-Path $outputRoot 'QuickShell.Core.dll') $stagingRoot
Copy-Item (Join-Path $outputRoot 'plugin.json') $stagingRoot
Copy-Item (Join-Path $outputRoot 'Images') (Join-Path $stagingRoot 'Images') -Recurse

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path (Join-Path $stagingRoot '*') -DestinationPath $zipPath
Write-Host "Created $zipPath"

if ($Deploy) {
    if (-not (Test-Path $pluginRoot)) {
        New-Item -ItemType Directory -Force -Path $pluginRoot | Out-Null
    }

    Get-ChildItem $pluginRoot -Force | Remove-Item -Recurse -Force
    Copy-Item -Path (Join-Path $stagingRoot '*') -Destination $pluginRoot -Recurse -Force
    Write-Host "Deployed to $pluginRoot"
    Write-Host "Restart PowerToys to load the plugin."
}
