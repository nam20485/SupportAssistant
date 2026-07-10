# SupportAssistant Distribution and Packaging Guide

This document describes how to build and package the SupportAssistant application for distribution to end users.

## Overview

SupportAssistant supports multiple distribution formats:
- **Self-contained executable** (recommended for most users)
- **Portable ZIP packages** (for users who prefer portable apps)
- **MSIX packages** (for Windows Store or enterprise distribution)

## Prerequisites

- **.NET 10 SDK** (see `global.json`; projects target `net10.0`)
- **`NUGET_AUTH_TOKEN`** — GitHub PAT with `read:packages` for the private `intel-agency` feed
  (`InferenceEngine.Core`). Required for `dotnet restore` (see `nuget.config`).
- **Windows 10/11** (for MSIX packaging)
- **PowerShell 5.1** or **PowerShell Core 7+** (for advanced build scripts)

## Quick Build (Simple)

For a quick build of the application for Windows x64:

```cmd
# Windows Command Prompt
scripts\build-simple.bat
```

This will:
1. Restore NuGet packages
2. Run tests
3. Build a self-contained executable for Windows x64
4. Output files to `dist\win-x64\`

## Advanced Build (PowerShell)

For more control over the build process:

```powershell
# Build for all supported platforms
.\scripts\build-distribution.ps1 -Runtime all -CreatePortable

# Build for specific platform with MSIX
.\scripts\build-distribution.ps1 -Runtime win-x64 -CreateMSIX -CreatePortable

# Skip tests for faster builds
.\scripts\build-distribution.ps1 -SkipTests -CreatePortable

# Debug build
.\scripts\build-distribution.ps1 -Configuration Debug
```

### PowerShell Script Parameters

| Parameter | Description | Default |
|-----------|-------------|---------|
| `-Configuration` | Build configuration (Debug/Release) | Release |
| `-Runtime` | Target runtime (win-x64/win-arm64/all) | all |
| `-SkipTests` | Skip running unit tests | false |
| `-CreateMSIX` | Create MSIX package | false |
| `-CreatePortable` | Create ZIP packages | false |
| `-OutputPath` | Output directory | dist |

## Manual Build Commands

If you prefer to run the build commands manually:

### Self-Contained Single File

```bash
# Windows x64
dotnet publish src/SupportAssistant/SupportAssistant.csproj \
  --configuration Release \
  --runtime win-x64 \
  --self-contained true \
  --output dist/win-x64 \
  /p:PublishSingleFile=true \
  /p:PublishTrimmed=false \
  /p:IncludeNativeLibrariesForSelfExtract=true \
  /p:PublishReadyToRun=true

# Windows ARM64
dotnet publish src/SupportAssistant/SupportAssistant.csproj \
  --configuration Release \
  --runtime win-arm64 \
  --self-contained true \
  --output dist/win-arm64 \
  /p:PublishSingleFile=true \
  /p:PublishTrimmed=false \
  /p:IncludeNativeLibrariesForSelfExtract=true \
  /p:PublishReadyToRun=true
```

### MSIX Package

```bash
dotnet publish src/SupportAssistant/SupportAssistant.csproj \
  --configuration Release \
  --runtime win-x64 \
  --self-contained true \
  --output dist/msix \
  /p:WindowsPackageType=MSIX \
  /p:GenerateAppInstallerFile=true
```

## Distribution Formats

### 1. Self-Contained Executable

**Best for**: Most end users, simple distribution

- **File**: `SupportAssistant.exe`
- **Size**: ~100-150 MB (includes .NET runtime)
- **Requirements**: Windows 10/11, no additional software needed
- **Installation**: Copy and run

**Advantages**:
- No dependencies required
- Single file deployment
- Optimized for performance (ReadyToRun)
- Trimming is **disabled** (`PublishTrimmed=false`) for ReactiveUI compatibility

### 2. Portable ZIP Package

**Best for**: Users who prefer portable applications

- **File**: `SupportAssistant-1.0.0-win-x64-portable.zip`
- **Contents**: Self-contained executable + documentation
- **Requirements**: Windows 10/11
- **Installation**: Extract and run

**Advantages**:
- No installation required
- Can run from removable media
- Easy to backup and distribute

### 3. MSIX Package

**Best for**: Enterprise deployment, Windows Store distribution

- **File**: `SupportAssistant.msix`
- **Installation**: Double-click or deploy via Group Policy
- **Requirements**: Windows 10 1809+ or Windows 11
- **Features**: Automatic updates, sandboxed execution

**Advantages**:
- Integrated with Windows package management
- Automatic updates support
- Clean installation/uninstallation
- Enhanced security through sandboxing

## Build Optimization

### Trimming (disabled)
- **`PublishTrimmed=false`** — required for ReactiveUI / Avalonia.ReactiveUI compatibility.
  Do not enable trimming for Release publishes.

### Ahead-of-Time Compilation
- **PublishReadyToRun**: Pre-compiles for faster startup
- **TieredCompilation**: Optimizes hot paths during runtime (`Directory.Build.props`)

### Single File Deployment
- **PublishSingleFile**: Bundles into one executable (Release / publish)
- **IncludeNativeLibrariesForSelfExtract**: Includes ONNX Runtime native libraries

## Dependencies

Versions below match the project files at time of writing; prefer the `.csproj` /
`Directory.Build.props` pins if they diverge.

### Core Framework
- **.NET 10** runtime (self-contained publish)
- **Avalonia UI 11.3.18**
- **ReactiveUI 20.4.1** (pinned for Avalonia.ReactiveUI compatibility)

### AI and Machine Learning
- **InferenceEngine.Core 1.1.29** (private GitHub Packages feed)
- **Microsoft.ML.OnnxRuntime** (see `OnnxRuntimeVersion` in `Directory.Build.props`; DirectML /
  ROCm come transitively via InferenceEngine on the relevant RIDs)
- **Microsoft.ML.Tokenizers 2.0.0**

### Utilities
- **CommunityToolkit.Mvvm 8.4.2**
- **Microsoft.Extensions.DependencyInjection 10.0.9**

## File Structure

After building, the distribution will have this structure:

```
dist/
├── win-x64/
│   ├── SupportAssistant.exe          # Main application
│   ├── appsettings.json              # Configuration (if any)
│   └── [Other required files]
├── win-arm64/
│   └── [Same structure for ARM64]
└── msix/
    ├── SupportAssistant.msix         # MSIX package
    └── SupportAssistant.msixbundle   # Multi-architecture bundle
```

## Version Management

Version information is managed centrally in `Directory.Build.props`:

```xml
<PropertyGroup>
  <VersionMajor>1</VersionMajor>
  <VersionMinor>0</VersionMinor>
  <VersionPatch>0</VersionPatch>
  <Version>$(VersionMajor).$(VersionMinor).$(VersionPatch)</Version>
</PropertyGroup>
```

To update the version:
1. Edit the version numbers in `Directory.Build.props`
2. Update the version in `Package.appxmanifest` (for MSIX)
3. Rebuild the application

## Automated CI/CD

The GitHub Actions workflow in `.github/workflows/build-and-package.yml` automatically:

1. **Builds and tests** on every push/PR
2. **Creates packages** for releases
3. **Uploads artifacts** for download
4. **Attaches packages** to GitHub releases

To trigger a release build:
1. Create a git tag: `git tag v1.0.0`
2. Push the tag: `git push origin v1.0.0`
3. Create a GitHub release from the tag

## Troubleshooting

### Common Issues

**Restore fails on InferenceEngine.Core**:
- Set `NUGET_AUTH_TOKEN` to a GitHub PAT with `read:packages` for the `intel-agency` org
- The default CI `GITHUB_TOKEN` cannot read that feed (different owner)

**MSIX packaging fails**:
- Ensure Windows App SDK is installed
- Verify the Package.appxmanifest is valid
- Check that all required capabilities are declared
- Full MSIX still requires a Windows TFM + signing; CI treats MSIX as best-effort

**Large file size**:
- Do **not** enable `PublishTrimmed` (breaks ReactiveUI)
- Consider framework-dependent deployment only for internal/dev builds

### Performance Optimization

**Startup time**:
- Use `PublishReadyToRun=true` for ReadyToRun compilation
- Tiered compilation is enabled in `Directory.Build.props`

**Memory usage**:
- Profile hot paths; trimming is not an option while ReactiveUI is in use

## Security Considerations

### Code Signing

For production distribution, consider code signing:

```powershell
# Sign the executable (requires certificate)
signtool sign /f certificate.pfx /p password /t http://timestamp.comodoca.com /fd sha256 SupportAssistant.exe
```

### MSIX Signing

MSIX packages must be signed for distribution:

```powershell
# Create and install test certificate (development only)
New-SelfSignedCertificate -Type Custom -Subject "CN=Test Certificate" -KeyUsage DigitalSignature -FriendlyName "Test Certificate" -CertStoreLocation "Cert:\CurrentUser\My"

# Sign MSIX package
SignTool sign /fd SHA256 /a /f certificate.pfx /p password SupportAssistant.msix
```

## Distribution Checklist

Before releasing:

- [ ] **Version numbers** updated in all files
- [ ] **Tests pass** on all target platforms
- [ ] **Code signed** (for production)
- [ ] **MSIX signed** and tested
- [ ] **Documentation** updated
- [ ] **Release notes** prepared
- [ ] **Antivirus scan** completed
- [ ] **Installation tested** on clean Windows systems

## Support and Maintenance

### Update Strategy

- **Minor updates**: Distribute via MSIX auto-update or new ZIP files
- **Major updates**: Full redistribution with migration guidance
- **Security updates**: Immediate release with security advisories

### Compatibility

- **Minimum Windows version**: Windows 10 1809 (build 17763)
- **Recommended**: Windows 11 with latest updates
- **Architecture support**: x64 and ARM64
- **Hardware requirements**: 4GB RAM, 1GB storage, DirectML-capable GPU (optional)

This packaging system ensures that SupportAssistant can be distributed efficiently while maintaining security, performance, and compatibility across different deployment scenarios.
