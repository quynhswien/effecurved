# Build Instructions for effecurved

## Prerequisites

### Development Environment
- **Visual Studio 2022** or later (recommended)
- **Rider 2023** or later (alternative)
- **.NET SDK 8.0** (for Revit 2025+)
- **.NET Framework 4.8 Developer Pack** (for Revit 2022-2024)
- **Autodesk Revit** (any version 2022-2026)

### Required Tools
- Git (for version control)
- NuGet Package Manager
- Nice3point.Revit.Build.Tasks (installed via NuGet)

---

## Project Configuration

### Multi-Targeting Configuration
The project supports multiple Revit versions through build configurations:

| Configuration | Revit Version | Target Framework |
|--------------|---------------|------------------|
| Debug R22    | Revit 2022    | .NET Framework 4.8 |
| Debug R23    | Revit 2023    | .NET Framework 4.8 |
| Debug R24    | Revit 2024    | .NET Framework 4.8 |
| Debug R25    | Revit 2025    | .NET 8.0 |
| Debug R26    | Revit 2026    | .NET 8.0 |
| Release R22  | Revit 2022    | .NET Framework 4.8 |
| Release R23  | Revit 2023    | .NET Framework 4.8 |
| Release R24  | Revit 2024    | .NET Framework 4.8 |
| Release R25  | Revit 2025    | .NET 8.0 |
| Release R26  | Revit 2026    | .NET 8.0 |

---

## Building the Project

### Using Visual Studio

1. **Open the solution**
   ```
   Double-click: effecurved.sln
   ```

2. **Select build configuration**
   - Use the dropdown at the top to select configuration
   - Example: "Debug R25" for Revit 2025 debug build

3. **Restore NuGet packages**
   ```
   Right-click solution → Restore NuGet Packages
   ```

4. **Build the project**
   ```
   Press F6 or Build → Build Solution
   ```

### Using Command Line (dotnet CLI)

#### Build Single Configuration
```bash
# Debug build for Revit 2025
dotnet build -c "Debug R25"

# Release build for Revit 2026
dotnet build -c "Release R26"
```

#### Build All Configurations
```bash
# Debug builds
dotnet build -c "Debug R22"
dotnet build -c "Debug R23"
dotnet build -c "Debug R24"
dotnet build -c "Debug R25"
dotnet build -c "Debug R26"

# Release builds
dotnet build -c "Release R22"
dotnet build -c "Release R23"
dotnet build -c "Release R24"
dotnet build -c "Release R25"
dotnet build -c "Release R26"
```

#### Build Script (PowerShell)
```powershell
# Save as build-all.ps1
$configurations = @("R22", "R23", "R24", "R25", "R26")
$modes = @("Debug", "Release")

foreach ($mode in $modes) {
    foreach ($config in $configurations) {
        Write-Host "Building $mode $config..." -ForegroundColor Cyan
        dotnet build -c "$mode $config"
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Build failed for $mode $config" -ForegroundColor Red
            exit 1
        }
    }
}
Write-Host "All builds completed successfully!" -ForegroundColor Green
```

Run with:
```powershell
.\build-all.ps1
```

---

## Build Output

### Output Locations
Build output is placed in version-specific folders:

```
bin/
├── Debug R22/
│   └── effecurved.dll
├── Debug R23/
│   └── effecurved.dll
├── Debug R25/
│   └── effecurved.dll
├── Release R22/
│   └── effecurved.dll
└── ...
```

### Generated Files
Each build produces:
- `effecurved.dll` - Main add-in assembly
- `effecurved.addin` - Revit add-in manifest
- `*.pdb` - Debug symbols (Debug builds only)
- Dependency DLLs (NuGet packages)

---

## Deployment

### Manual Deployment

1. **Locate build output**
   ```
   Navigate to: bin\Release R25\
   ```

2. **Copy files to Revit addins folder**
   ```
   Target: %APPDATA%\Autodesk\Revit\Addins\2025\
   
   Copy:
   - effecurved.dll
   - effecurved.addin
   - All dependency DLLs (except Revit assemblies)
   ```

3. **Verify .addin file paths**
   Ensure paths in `effecurved.addin` match deployment location

### Automatic Deployment (Nice3point.Revit.Build.Tasks)

The project uses Nice3point.Revit.Build.Tasks which can automatically deploy to Revit:

```xml
<PropertyGroup>
    <PublishAddinFiles>true</PublishAddinFiles>
</PropertyGroup>
```

This automatically copies files to:
```
%APPDATA%\Autodesk\Revit\Addins\[VERSION]\effecurved\
```

---

## Testing the Build

### Verification Checklist

After building, verify:

- [ ] Build completed without errors
- [ ] Build completed without warnings (or acceptable warnings only)
- [ ] DLL file generated in output folder
- [ ] .addin file generated
- [ ] All dependencies present
- [ ] File sizes reasonable (not 0 bytes)

### Smoke Test

1. **Copy build to Revit addins folder**
2. **Start Revit**
3. **Check for EFFE ribbon tab**
4. **Click "Unroll Curved Surface" button**
5. **Verify dialog opens without errors**

### Full Test

Follow the test checklist in `IMPLEMENTATION_SUMMARY.md`

---

## Troubleshooting Build Issues

### Common Issues

#### Issue: Build fails with "Target framework not found"
**Solution**: Install required SDK or Developer Pack
- .NET 8.0 SDK: https://dotnet.microsoft.com/download
- .NET Framework 4.8: Via Visual Studio Installer

#### Issue: NuGet packages not restoring
**Solution**: Clear NuGet cache and restore
```bash
dotnet nuget locals all --clear
dotnet restore
```

#### Issue: "Revit API not found" errors
**Solution**: Packages should be auto-resolved via Nice3point packages. If not:
1. Check internet connection
2. Verify NuGet sources are configured
3. Manually restore packages

#### Issue: XAML designer errors
**Solution**: These are often false positives
- Try rebuilding project
- Close and reopen XAML file
- Build should still succeed

#### Issue: Cannot debug with Revit
**Solution**: Set debug start action
1. Right-click project → Properties
2. Debug tab
3. Start external program: `C:\Program Files\Autodesk\Revit 2025\Revit.exe`

---

## Build Configuration Details

### Conditional Compilation Symbols

The build system automatically defines symbols based on Revit version:

```csharp
// Revit 2022
REVIT2022
REVIT2022_OR_GREATER

// Revit 2023
REVIT2023
REVIT2022_OR_GREATER
REVIT2023_OR_GREATER

// Revit 2024
REVIT2024
REVIT2022_OR_GREATER
REVIT2023_OR_GREATER
REVIT2024_OR_GREATER

// Revit 2025
REVIT2025
REVIT2022_OR_GREATER
REVIT2023_OR_GREATER
REVIT2024_OR_GREATER
REVIT2025_OR_GREATER
NETCORE

// Revit 2026
REVIT2026
REVIT2022_OR_GREATER
REVIT2023_OR_GREATER
REVIT2024_OR_GREATER
REVIT2025_OR_GREATER
REVIT2026_OR_GREATER
NETCORE
```

### Using Conditional Compilation

Example in code:
```csharp
#if REVIT2023_OR_GREATER
    // Use modern API
    var unitType = UnitTypeId.Millimeters;
#else
    // Use legacy API
    var unitType = DisplayUnitType.DUT_MILLIMETERS;
#endif
```

---

## Performance Optimization

### Release Build Optimizations
- Code optimization enabled
- Debug symbols minimal
- Assembly size reduced

### Debug Build Features
- Full debug symbols
- No code optimization
- Better debugging experience

---

## Continuous Integration (Optional)

### GitHub Actions Example

Create `.github/workflows/build.yml`:

```yaml
name: Build

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:
  build:
    runs-on: windows-latest
    
    strategy:
      matrix:
        revit: [R22, R23, R24, R25, R26]
        config: [Debug, Release]
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build -c "${{ matrix.config }} ${{ matrix.revit }}"
```

---

## Version Specific Notes

### Revit 2022-2024 (.NET Framework 4.8)
- Uses legacy framework
- Compatible with older Windows versions
- Some modern C# features limited

### Revit 2025-2026 (.NET 8.0)
- Modern .NET runtime
- Better performance
- Full C# 12 support
- Requires Windows 10 1607 or higher

---

## Build Validation

### Automated Validation Script (PowerShell)

```powershell
# Save as validate-build.ps1
param(
    [string]$Configuration = "Release R25"
)

Write-Host "Validating build: $Configuration" -ForegroundColor Cyan

$outputPath = "bin\$Configuration"
$dllPath = Join-Path $outputPath "effecurved.dll"

if (-not (Test-Path $dllPath)) {
    Write-Host "ERROR: DLL not found at $dllPath" -ForegroundColor Red
    exit 1
}

$dll = Get-Item $dllPath
if ($dll.Length -eq 0) {
    Write-Host "ERROR: DLL has zero size" -ForegroundColor Red
    exit 1
}

Write-Host "✓ DLL exists: $($dll.Length) bytes" -ForegroundColor Green

# Check for .addin file
$addinPath = Join-Path $outputPath "effecurved.addin"
if (Test-Path $addinPath) {
    Write-Host "✓ .addin file exists" -ForegroundColor Green
} else {
    Write-Host "WARNING: .addin file not found" -ForegroundColor Yellow
}

Write-Host "`nBuild validation passed!" -ForegroundColor Green
```

Run with:
```powershell
.\validate-build.ps1 -Configuration "Release R25"
```

---

## Next Steps After Building

1. ✅ Build completes successfully
2. ✅ Run validation script
3. ✅ Deploy to Revit addins folder
4. ✅ Test in Revit
5. ✅ Run full test suite
6. ✅ Package for distribution

---

## Distribution

### Packaging for Users

1. **Create distribution folder structure**
   ```
   effecurved-v1.0.0/
   ├── R2022/
   │   ├── effecurved.dll
   │   ├── effecurved.addin
   │   └── dependencies/
   ├── R2023/
   ├── R2024/
   ├── R2025/
   ├── R2026/
   ├── README.txt
   └── INSTALL.txt
   ```

2. **Create installer (optional)**
   - Use WiX Toolset
   - Or Inno Setup
   - Or NSIS

3. **Create release notes**
   - Include version number
   - List new features
   - Known issues
   - Installation instructions

---

## Support

For build issues or questions:
- Check troubleshooting section above
- Review project documentation
- Check Nice3point.Revit documentation
- File an issue in the repository

---

**Last Updated**: February 3, 2026  
**Build System Version**: 1.0  
**Supported Revit Versions**: 2022-2026
