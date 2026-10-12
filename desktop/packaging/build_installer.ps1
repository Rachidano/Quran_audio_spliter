# =====================================================================
# Script de fabrication automatisée du Setup.exe Windows x64 (Lot 10)
# =====================================================================
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$DesktopDir = Split-Path -Parent $ScriptDir
$ProjectFile = Join-Path $DesktopDir "src\QuranVideo.Desktop\QuranVideo.Desktop.csproj"
$PublishDir = Join-Path $DesktopDir "publish"
$DistDir = Join-Path $DesktopDir "dist"
$InnoCompiler = "C:\Users\r_kaci\AppData\Local\Programs\Inno Setup 6\ISCC.exe"
$SetupScript = Join-Path $ScriptDir "setup.iss"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Fabrication de l'installateur Windows QuranVideo Setup.exe" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Vérification des outils
if (-not (Test-Path $InnoCompiler)) {
    throw "Compilateur Inno Setup introuvable : $InnoCompiler"
}
if (-not (Test-Path "C:\ffmpeg\bin\ffmpeg.exe")) {
    throw "FFmpeg introuvable : C:\ffmpeg\bin\ffmpeg.exe"
}

# 2. Nettoyage
Write-Host "`n[1/4] Nettoyage des répertoires de sortie..." -ForegroundColor Yellow
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
if (Test-Path $DistDir) { Remove-Item $DistDir -Recurse -Force }
New-Item -ItemType Directory -Path $PublishDir -Force | Out-Null
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null

# 3. Publication .NET 9 autonome (self-contained win-x64)
Write-Host "`n[2/4] Publication .NET 9 autonome (win-x64, self-contained)..." -ForegroundColor Yellow
dotnet publish $ProjectFile `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $PublishDir

if ($LASTEXITCODE -ne 0) {
    throw "Échec de la commande dotnet publish."
}

# 4. Compilation de l'installateur Inno Setup
Write-Host "`n[3/4] Compilation de l'installateur Inno Setup (arabe RTL)..." -ForegroundColor Yellow
& $InnoCompiler $SetupScript

if ($LASTEXITCODE -ne 0) {
    throw "Échec de la compilation Inno Setup."
}

# 5. Contrôle et empreinte du livrable
Write-Host "`n[4/4] Vérification du livrable Setup.exe..." -ForegroundColor Yellow
$SetupExe = Join-Path $DistDir "Setup.exe"

if (-not (Test-Path $SetupExe)) {
    throw "Le fichier Setup.exe n'a pas été généré dans $DistDir."
}

$fileInfo = Get-Item $SetupExe
$hash = Get-FileHash -Path $SetupExe -Algorithm SHA256

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host " INSTALLATEUR GÉNÉRÉ AVEC SUCCÈS !" -ForegroundColor Green
Write-Host " Fichier : $($fileInfo.FullName)" -ForegroundColor White
Write-Host " Taille  : $([math]::Round($fileInfo.Length / 1MB, 2)) Mo ($($fileInfo.Length) octets)" -ForegroundColor White
Write-Host " SHA-256 : $($hash.Hash)" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Green

[PSCustomObject]@{
    Path = $fileInfo.FullName
    SizeMB = [math]::Round($fileInfo.Length / 1MB, 2)
    SizeBytes = $fileInfo.Length
    Sha256 = $hash.Hash
}
