# Changelog

All notable changes to the effecurved Revit add-in will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Planned for 1.1.0
- Detail line creation alongside filled regions
- Automatic dimension generation
- Custom line style support
- Seam line indication for cylindrical surfaces
- Material information preservation

### Planned for 2.0.0
- Double-curved surface approximation
- Export to DXF/DWG formats
- Automatic nesting of multiple pieces
- Material waste calculation
- Batch processing mode
- Configuration profiles

## [1.0.0] - 2026-02-03

### Added
- **Unroll Curved Surface Command**: Main feature for flattening 3D curved geometry to 2D
  - Cylindrical face unrolling with accurate arc length preservation
  - Conical face unrolling with proper cone development
  - Planar face projection to 2D plane
  - Ruled surface approximation and flattening
  
- **User Interface**:
  - Modern WPF dialog with MVVM architecture
  - Geometry selection tool (faces or edges)
  - Drafting view selection (existing or new)
  - Real-time status feedback
  - Processing progress indication
  
- **Core Services**:
  - `GeometryUnrollService`: Core geometry processing and unrolling algorithms
  - Face type classification (cylindrical, conical, planar, ruled)
  - Automatic filled region creation in drafting views
  - Multi-face processing with automatic spacing
  
- **Data Models**:
  - `UnrollSettings`: Configuration for unroll operations
  - `UnrollResult`: Result information and statistics
  - `UnrollStatistics`: Processing metrics and performance data
  - `FaceInfo`: Face classification and validation
  
- **Documentation**:
  - Comprehensive feature documentation
  - Quick reference guide
  - Code examples and usage patterns
  - Troubleshooting guide
  
- **Developer Features**:
  - Usage statistics collection
  - Error tracking and reporting
  - Comprehensive logging
  - License checking integration
  
- **Multi-Version Support**:
  - Revit 2022 (.NET Framework 4.8)
  - Revit 2023 (.NET Framework 4.8)
  - Revit 2024 (.NET Framework 4.8)
  - Revit 2025 (.NET 8.0)
  - Revit 2026 (.NET 8.0)

### Technical Details
- Implemented External Event pattern for proper Revit API context
- Used CommunityToolkit.Mvvm for MVVM implementation
- Added XML documentation for all public APIs
- Integrated with existing BaseCommand infrastructure
- Added comprehensive error handling and recovery

### Known Limitations
- Does not support double-curved (non-developable) surfaces like spheres or toroids
- Ruled surface unrolling uses simplified approximation
- No automatic dimension creation (manual dimensioning required)
- Output is filled region only (no detail lines)
- Maximum recommended face count: 20 (performance consideration)

### Performance
- Single face processing: <1 second
- Multiple faces (5): 2-3 seconds
- Complex geometry (10+ faces): 5-10 seconds
- Tessellation density: 50 points (configurable in models)

### Dependencies
- Nice3point.Revit.Api.RevitAPI (version-specific)
- Nice3point.Revit.Extensions (version-specific)
- Nice3point.Revit.Toolkit (version-specific)
- CommunityToolkit.Mvvm 8.x
- Newtonsoft.Json 13.0.3
- System.Security.Cryptography.ProtectedData 6.0.0

### Architecture
- Command Pattern: `UnrollCurvedSurfaceCommand`
- MVVM Pattern: `UnrollCurvedSurfaceViewModel` + `UnrollCurvedSurfaceView`
- Service Layer: `GeometryUnrollService`
- Data Models: `UnrollGeometryModels`

---

## Version Numbering

Version format: MAJOR.MINOR.PATCH

- **MAJOR**: Breaking changes or major new features
- **MINOR**: New features, backward compatible
- **PATCH**: Bug fixes, backward compatible

---

## Change Categories

- **Added**: New features
- **Changed**: Changes to existing functionality
- **Deprecated**: Soon-to-be removed features
- **Removed**: Removed features
- **Fixed**: Bug fixes
- **Security**: Security vulnerability fixes
