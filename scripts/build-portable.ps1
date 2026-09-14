#Requires -Version 5.1
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot "JianpuEditor.sln"
$buildOutput = Join-Path $repoRoot "JianpuEditor\bin\Release\net472"
$outputDir = Join-Path $repoRoot "installer\output"
$staging = Join-Path $outputDir "JianpuEditor-Portable"

$dotnetCandidates = @(
    $env:DOTNET_EXE,
    "D:\dotnet\dotnet.exe",
    "dotnet"
) | Where-Object { $_ -and (($_ -eq "dotnet") -or (Test-Path $_)) }

$dotnet = $dotnetCandidates | Select-Object -First 1
if (-not $dotnet) {
    throw "dotnet SDK not found. Set DOTNET_EXE or install .NET SDK."
}

Write-Host "Building Release..."
& $dotnet build $solution -c Release
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path $buildOutput)) {
    throw "Build output not found at $buildOutput"
}

if (Test-Path $staging) {
    Remove-Item $staging -Recurse -Force
}
New-Item -ItemType Directory -Path $staging -Force | Out-Null

Copy-Item (Join-Path $buildOutput "*") -Destination $staging -Recurse -Force
Get-ChildItem -Path $staging -Filter "*.pdb" -Recurse | Remove-Item -Force
Set-Content -Path (Join-Path $staging "portable.txt") `
    -Value "This file marks JianpuEditor as running in portable mode: settings.json is kept in this folder instead of %LOCALAPPDATA%. Delete it to switch back to per-user settings." `
    -NoNewline

$zip = Join-Path $outputDir "JianpuEditor-Portable.zip"
if (Test-Path $zip) {
    Remove-Item $zip -Force
}
Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $zip -Force

Write-Host "Portable package created: $zip"
Write-Host "Size: $([math]::Round((Get-Item $zip).Length / 1MB, 2)) MB"
