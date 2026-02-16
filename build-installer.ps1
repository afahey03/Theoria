# Build script for Theoria Desktop installer
# Creates a setup wizard using Inno Setup

$ErrorActionPreference = "Stop"

# Configuration
$ProjectPath = "src\Theoria.Desktop\Theoria.Desktop.csproj"
$PublishDir = "src\Theoria.Desktop\bin\Release\net8.0-windows\publish"
$ReleaseDir = "release"
$InnoSetupCompiler = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
$InnoSetupScript = "Theoria.iss"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Theoria Desktop Installer Build" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Clean previous build
Write-Host "[1/3] Cleaning previous build..." -ForegroundColor Yellow
if (Test-Path $PublishDir) {
    Remove-Item -Path $PublishDir -Recurse -Force
    Write-Host "  Removed: $PublishDir" -ForegroundColor Gray
}
if (Test-Path $ReleaseDir) {
    Remove-Item -Path $ReleaseDir -Recurse -Force
    Write-Host "  Removed: $ReleaseDir" -ForegroundColor Gray
}
Write-Host "  Clean complete" -ForegroundColor Green
Write-Host ""

# Step 2: Publish the application
Write-Host "[2/3] Publishing application..." -ForegroundColor Yellow
dotnet publish $ProjectPath `
    --configuration Release `
    --runtime win-x64 `
    --self-contained false `
    --output $PublishDir

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "  Publish complete" -ForegroundColor Green
Write-Host ""

# Step 3: Compile installer with Inno Setup
Write-Host "[3/3] Compiling installer..." -ForegroundColor Yellow
& $InnoSetupCompiler $InnoSetupScript

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Installer compilation failed!" -ForegroundColor Red
    exit 1
}
Write-Host "  Created: release\TheoriaSetup.exe" -ForegroundColor Gray
Write-Host "  Installer complete" -ForegroundColor Green
Write-Host ""

# Success message
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Build Successful!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor White
Write-Host "  1. Go to GitHub Releases" -ForegroundColor White
Write-Host "  2. Create a new release" -ForegroundColor White
Write-Host "  3. Upload TheoriaSetup.exe" -ForegroundColor White
Write-Host "  4. Update download URL if needed" -ForegroundColor White
Write-Host ""
