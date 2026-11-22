param(
    [string]$Version = "1.0.0",
    [string]$Repo = "Mericsdc/LRXCore"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$setupExe = Join-Path $root "dist\LRXCore-Setup-$Version.exe"
if (-not (Test-Path $setupExe)) {
    Write-Host "Setup executable not found, running build-release.ps1 first..."
    & (Join-Path $PSScriptRoot "build-release.ps1") -Version $Version
}

gh auth status | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "GitHub CLI (gh) oturumu acik degil. Lutfen once 'gh auth login' komutunu calistirin."
}

$repoExists = $false
try {
    gh repo view $Repo | Out-Null
    if ($LASTEXITCODE -eq 0) { $repoExists = $true }
} catch {
    $repoExists = $false
}

if (-not $repoExists) {
    Write-Host "Creating public GitHub repository: $Repo" -ForegroundColor Cyan
    gh repo create $Repo --public --source . --remote origin --push
} else {
    $hasOrigin = (git remote) -contains "origin"
    if (-not $hasOrigin) {
        git remote add origin "https://github.com/$Repo.git"
    }
    git push -u origin main
}

$tag = "v$Version"
git tag -f $tag
git push -f origin $tag

$releaseExists = $false
try {
    gh release view $tag --repo $Repo | Out-Null
    if ($LASTEXITCODE -eq 0) { $releaseExists = $true }
} catch {
    $releaseExists = $false
}

if (-not $releaseExists) {
    $releaseNotes = @"
LRX Core v$Version kurulum paketi (LRXCore-Setup-$Version.exe).

- Format sonrası toplu uygulama kurulumu (Winget + standart kullanıcı token düşürme destekli)
- Donanım tespiti & sürücü/Visual C++ AIO önerileri
- O&O ShutUp10++ önerilen gizlilik ayarları & Win11 klasik sağ tık menüsü & OneDrive komple kaldırma
- Çökme korumalı Windows hizmet optimizasyonu
- HiBit tarzı derin program kaldırıcı, gereksiz dosya ve güvenli kayıt defteri temizleyici
- GitHub Releases üzerinden otomatik uygulama güncelleme desteği
"@
    gh release create $tag $setupExe `
        --repo $Repo `
        --title "LRX Core $tag" `
        --notes $releaseNotes
} else {
    gh release upload $tag $setupExe --repo $Repo --clobber
}

Write-Host "GitHub yayini tamamlandi: https://github.com/$Repo/releases/tag/$tag" -ForegroundColor Green
