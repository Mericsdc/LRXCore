param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$csproj = Join-Path $root "LRXCore.csproj"
$testCsproj = Join-Path $root "LRXCore.Tests\LRXCore.Tests.csproj"
$publishDir = Join-Path $root "publish\win-x64"
$issPath = Join-Path $root "installer\LRXCore.iss"
$distDir = Join-Path $root "dist"

Write-Host "=== 1. Building Release ($Version) ===" -ForegroundColor Cyan
dotnet build $csproj -c Release -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "Release build failed." }

Write-Host "=== 2. Running Non-Destructive Tests ===" -ForegroundColor Cyan
dotnet run --project $testCsproj -c Release
if ($LASTEXITCODE -ne 0) { throw "Verification tests failed." }

Write-Host "=== 3. Publishing Self-Contained win-x64 ===" -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish $csproj -c Release -r win-x64 --self-contained true -p:Version=$Version -p:PublishReadyToRun=false -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "Publish failed." }

$publishedExe = Join-Path $publishDir "LRXCore.exe"
if (-not (Test-Path $publishedExe)) { throw "Published EXE missing: $publishedExe" }

Add-Type -AssemblyName System.Drawing
$assocIcon = [System.Drawing.Icon]::ExtractAssociatedIcon($publishedExe)
if ($null -eq $assocIcon) { throw "Embedded icon could not be extracted from $publishedExe" }
$assocIcon.Dispose()
Write-Host "Verified embedded icon in published EXE: $publishedExe" -ForegroundColor Green

Write-Host "=== 4. Building Inno Setup Installer ===" -ForegroundColor Cyan
$isccCandidates = @(
    "$env:LocalAppData\Programs\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)
$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 (ISCC.exe) not found." }

New-Item -ItemType Directory -Force -Path $distDir | Out-Null
& $iscc "/DMyAppVersion=$Version" $issPath
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed." }

$setupExe = Join-Path $distDir "LRXCore-Setup-$Version.exe"
Get-Item $setupExe | Select-Object FullName, Length, LastWriteTime
