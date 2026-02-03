# effecurved - Revit Add-in for Curved Surface Unrolling

## Overview

**effecurved** is a Revit add-in that provides advanced tools for working with curved geometry in Autodesk Revit. The primary feature allows users to unroll (flatten) curved surfaces into 2D representations for fabrication, documentation, and design development.

## Features

### 🎯 Unroll Curved Surface
Flatten 3D curved geometry into 2D drafting views with accurate length preservation.

**Key Capabilities:**
- ✅ Unroll cylindrical, conical, and ruled surfaces
- ✅ Support for multiple face selection
- ✅ Create or use existing drafting views
- ✅ Automatic filled region generation
- ✅ Accurate length and proportion preservation

**Supported Geometry:**
- Cylindrical faces (pipes, columns, ducts)
- Conical faces (tapered elements)
- Planar faces (flat surfaces)
- Ruled surfaces (developable surfaces)

## Installation

### Prerequisites
- Autodesk Revit 2022 or higher
- Windows 10/11
- .NET Framework 4.8 (Revit 2022-2024) or .NET 8.0 (Revit 2025-2026)

### Installation Steps
1. Download the latest release
2. Copy the `.addin` file to: `%APPDATA%\Autodesk\Revit\Addins\[VERSION]\`
3. Copy the DLL and related files to the same location
4. Restart Revit
5. Look for the **EFFE** tab in the ribbon

## Quick Start

### Basic Workflow
1. **Launch**: Click "Unroll Curved Surface" in the EFFE ribbon
2. **Select**: Choose faces or edges from your model
3. **Configure**: Create new or select existing drafting view
4. **Process**: Click Process to generate the unrolled geometry

### Example: Unroll a Pipe
```
1. Select a cylindrical pipe face in your model
2. Click "Unroll Curved Surface"
3. Click "Select Geometry" and pick the pipe face
4. Choose "Create New Drafting View"
5. Name it "Pipe Pattern 01"
6. Click "Process"
7. View the flattened pipe pattern in the drafting view
```

## Documentation

- **[Quick Reference Guide](UNROLL_QUICK_REFERENCE.md)** - Fast tips and common workflows
- **[Full Documentation](UNROLL_FEATURE_DOCUMENTATION.md)** - Complete technical documentation
- **[Code Examples](Examples/UnrollGeometryExamples.cs)** - Programmatic usage examples

## Project Structure

```
effecurved/
├── Commands/              # Revit command implementations
│   ├── BaseCommand.cs
│   ├── StartupCommand.cs
│   └── UnrollCurvedSurfaceCommand.cs
├── Services/              # Business logic and utilities
│   ├── GeometryUnrollService.cs    # Core unrolling logic
│   ├── RevitUtilitiesService.cs
│   ├── LoggingService.cs
│   └── StatisticsCollectorService.cs
├── ViewModels/            # MVVM view models
│   └── UnrollCurvedSurfaceViewModel.cs
├── Views/                 # WPF user interfaces
│   ├── UnrollCurvedSurfaceView.xaml
│   └── Converters/       # Data binding converters
├── Models/                # Data models
│   ├── UnrollGeometryModels.cs
│   └── ServerApiModels.cs
├── Examples/              # Usage examples
│   └── UnrollGeometryExamples.cs
└── Resources/            # Icons and assets
    └── Icons/
```

## Architecture

### Design Patterns
- **MVVM (Model-View-ViewModel)**: Clean separation of UI and logic
- **External Event Pattern**: Proper Revit API context handling
- **Service Locator**: Singleton services for utilities
- **Command Pattern**: Extensible command architecture

### Key Components

#### 1. Command Layer
`UnrollCurvedSurfaceCommand.cs` - Entry point that inherits from `BaseCommand`
- Handles licensing checks
- Manages error handling
- Records usage statistics

#### 2. ViewModel Layer
`UnrollCurvedSurfaceViewModel.cs` - UI logic and Revit API orchestration
- Implements `IExternalEventHandler` for context management
- Manages user selections
- Coordinates between UI and services

#### 3. Service Layer
`GeometryUnrollService.cs` - Core geometry processing
- Face analysis and classification
- Unrolling algorithms for different surface types
- Filled region creation

#### 4. View Layer
`UnrollCurvedSurfaceView.xaml` - WPF user interface
- Clean, modern UI design
- Responsive feedback
- Progress indication

## Development

### Building the Project
```bash
# Debug build for Revit 2025
dotnet build -c "Debug R25"

# Release build for Revit 2026
dotnet build -c "Release R26"
```

### Configuration Support
The project uses multi-targeting to support multiple Revit versions:
- Revit 2022, 2023, 2024: .NET Framework 4.8
- Revit 2025, 2026: .NET 8.0

### Adding New Features
1. Create command in `Commands/` extending `BaseCommand`
2. Create ViewModel in `ViewModels/` implementing MVVM pattern
3. Create View in `Views/` using WPF
4. Register command in `Application.cs`

## Technical Details

### Unrolling Algorithms

**Cylindrical Surfaces:**
- Maps 3D points to 2D using: `(radius × angle, height)`
- Preserves arc lengths as straight lines

**Conical Surfaces:**
- Calculates slant height
- Applies angle scaling for proper cone development
- Uses polar-to-Cartesian conversion

**Ruled Surfaces:**
- Tessellates curves
- Accumulates arc length for X-axis
- Simplified mapping for developable surfaces

### API Compatibility
Uses conditional compilation for multi-version support:
```csharp
#if REVIT2021_OR_GREATER
    // Modern API
#else
    // Legacy API
#endif
```

## Dependencies

### Core Dependencies
- `Nice3point.Revit.Api.RevitAPI` - Revit API wrapper
- `Nice3point.Revit.Extensions` - Extension methods
- `CommunityToolkit.Mvvm` - MVVM helpers
- `Newtonsoft.Json` - JSON serialization

### Build Tools
- `Nice3point.Revit.Build.Tasks` - Build automation
- `Nice3point.Revit.Toolkit` - Development utilities

## Troubleshooting

### Common Issues

**Issue**: "Face type not supported"
**Solution**: Only cylindrical, conical, planar, and ruled surfaces are supported. Complex NURBS won't work.

**Issue**: "No filled region types found"
**Solution**: Ensure your project template has at least one filled region type defined.

**Issue**: "Revit API running out of context"
**Solution**: The ExternalEvent pattern should handle this. If you see this error, report it as a bug.

## Contributing

### Code Standards
- Follow MVVM pattern for UI components
- Use XML documentation comments
- Implement proper error handling
- Add logging for debugging
- Record statistics for usage tracking

### Testing
- Test with multiple Revit versions
- Verify different geometry types
- Check edge cases (very small/large surfaces)
- Test multi-face scenarios

## Performance

### Optimization Tips
- Process faces in batches for large sets
- Use appropriate tessellation density
- Limit filled region complexity
- Consider creating separate views for many faces

### Benchmarks
Typical performance on standard hardware:
- Single cylindrical face: <1 second
- 5 complex ruled surfaces: 2-3 seconds
- 10+ faces: 5-10 seconds

## Roadmap

### Version 1.0.0 (Current) ✅
- Cylindrical face unrolling
- Conical face unrolling
- Planar face projection
- Ruled surface approximation
- Drafting view integration
- Filled region generation

### Version 1.1.0 (Planned)
- [ ] Detail line creation option
- [ ] Automatic dimensioning
- [ ] Custom line styles
- [ ] Seam line indication
- [ ] Material information transfer

### Version 2.0.0 (Future)
- [ ] Double-curved surface approximation
- [ ] Export to DXF/DWG
- [ ] Automatic nesting
- [ ] Material waste calculation
- [ ] Batch processing

## License

[Add your license information here]

## Support

- **Documentation**: See `UNROLL_FEATURE_DOCUMENTATION.md`
- **Quick Help**: See `UNROLL_QUICK_REFERENCE.md`
- **Examples**: See `Examples/UnrollGeometryExamples.cs`
- **Issues**: [Create an issue in the repository]

## Credits

Developed using:
- Autodesk Revit API
- Nice3point Revit Toolkit
- CommunityToolkit.Mvvm
- WPF (Windows Presentation Foundation)

## Version History

### 1.0.0 - Initial Release (February 2026)
- First public release
- Core unrolling functionality
- Support for cylindrical, conical, planar, and ruled surfaces
- WPF user interface
- Drafting view integration
- Statistics tracking
- Multi-version support (Revit 2022-2026)

---

**Made with ❤️ for the Revit community**
