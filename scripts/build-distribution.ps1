# SupportAssistant Build and Packaging Script
# This script builds the SupportAssistant application for distribution

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("win-x64", "win-arm64", "all")]
    [string]$Runtime = "all",
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipTests,
    
    [Parameter(Mandatory=$false)]
    [switch]$CreateMSIX,
    
    [Parameter(Mandatory=$false)]
    [switch]$CreatePortable,
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "dist"
)

# Script configuration
$ScriptPath = Split-Path -Parent $PSScriptRoot
$SolutionPath = Join-Path $ScriptPath "SupportAssistant.sln"
$ProjectPath = Join-Path $ScriptPath "src\SupportAssistant\SupportAssistant.csproj"
$DistPath = Join-Path $ScriptPath $OutputPath

Write-Host "🚀 SupportAssistant Build and Packaging Script" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Green
Write-Host "Runtime: $Runtime" -ForegroundColor Green
Write-Host "Output Path: $DistPath" -ForegroundColor Green
Write-Host ""

# Clean and prepare output directory
if (Test-Path $DistPath) {
    Write-Host "🧹 Cleaning output directory..." -ForegroundColor Yellow
    Remove-Item $DistPath -Recurse -Force
}
New-Item -ItemType Directory -Path $DistPath -Force | Out-Null

# Restore dependencies
Write-Host "📦 Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore $SolutionPath
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to restore NuGet packages"
    exit 1
}

# Run tests if not skipped
if (-not $SkipTests) {
    Write-Host "🧪 Running tests..." -ForegroundColor Yellow
    dotnet test $SolutionPath --configuration $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Tests failed"
        exit 1
    }
} else {
    Write-Host "⏭️ Skipping tests..." -ForegroundColor Yellow
}

# Determine target runtimes
$runtimes = @()
if ($Runtime -eq "all") {
    $runtimes = @("win-x64", "win-arm64")
} else {
    $runtimes = @($Runtime)
}

# Build for each runtime
foreach ($rid in $runtimes) {
    Write-Host "🔨 Building for $rid..." -ForegroundColor Yellow
    
    $publishPath = Join-Path $DistPath $rid
    
    # Self-contained single-file publish
    dotnet publish $ProjectPath `
        --configuration $Configuration `
        --runtime $rid `
        --self-contained true `
        --output $publishPath `
        --no-restore `
        /p:PublishSingleFile=true `
        /p:PublishTrimmed=false `
        /p:IsTrimmable=false `
        /p:IncludeNativeLibrariesForSelfExtract=true `
        /p:PublishReadyToRun=true
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to publish for $rid"
        exit 1
    }
    
    # Create portable ZIP package if requested
    if ($CreatePortable) {
        Write-Host "📦 Creating portable package for $rid..." -ForegroundColor Yellow
        $zipPath = Join-Path $DistPath "SupportAssistant-$rid-portable.zip"
        Compress-Archive -Path "$publishPath\*" -DestinationPath $zipPath -Force
        Write-Host "✅ Portable package created: $zipPath" -ForegroundColor Green
    }
    
    Write-Host "✅ Build completed for $rid" -ForegroundColor Green
}

# Create MSIX package if requested
if ($CreateMSIX) {
    Write-Host "📦 Creating MSIX package..." -ForegroundColor Yellow
    
    # Build MSIX package (requires Windows App SDK)
    $msixOutput = Join-Path $DistPath "msix"
    New-Item -ItemType Directory -Path $msixOutput -Force | Out-Null
    
    dotnet publish $ProjectPath `
        --configuration $Configuration `
        --runtime win-x64 `
        --self-contained true `
        --output $msixOutput `
        --no-restore `
        /p:WindowsPackageType=MSIX `
        /p:GenerateAppInstallerFile=true
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ MSIX package created in: $msixOutput" -ForegroundColor Green
    } else {
        Write-Warning "MSIX package creation failed (may require Windows App SDK)"
    }
}

# Display summary
Write-Host ""
Write-Host "🎉 Build Summary" -ForegroundColor Cyan
Write-Host "===============" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor White
Write-Host "Runtimes built: $($runtimes -join ', ')" -ForegroundColor White
Write-Host "Output directory: $DistPath" -ForegroundColor White

if (Test-Path $DistPath) {
    $items = Get-ChildItem $DistPath -Recurse | Measure-Object -Property Length -Sum
    $totalSize = [math]::Round($items.Sum / 1MB, 2)
    Write-Host "Total package size: $totalSize MB" -ForegroundColor White
}

Write-Host ""
Write-Host "📁 Distribution files:" -ForegroundColor Yellow
Get-ChildItem $DistPath -Recurse -File | ForEach-Object {
    $size = [math]::Round($_.Length / 1MB, 2)
    Write-Host "  $($_.FullName.Replace($DistPath, '').TrimStart('\')) ($size MB)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "✅ Build and packaging completed successfully!" -ForegroundColor Green
