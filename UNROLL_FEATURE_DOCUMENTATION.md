# Unroll Curved Surface Feature Documentation

## Overview

The **Unroll Curved Surface** feature allows users to flatten 3D curved geometry into 2D representations in Revit drafting views. This is particularly useful for fabrication, documentation, and creating flat patterns for curved surfaces.

## Feature Location

The command is accessible from the Revit ribbon under the **EFFE** tab in the **Commands** panel.

## User Workflow

### Step 1: Launch the Command
Click the **Unroll Curved Surface** button in the EFFE ribbon panel.

### Step 2: Select Geometry
Click the **Select Geometry** button in the dialog and choose one of the following:
- **Faces** (preferred method): Select one or multiple curved faces from your model
- **Edges** (fallback): If face selection is cancelled, you can select edge curves instead

**Supported Geometry Types:**
- Cylindrical faces (pipes, columns, etc.)
- Conical faces (tapered elements)
- Planar faces (flat surfaces)
- Ruled surfaces (surfaces that can be formed by straight lines)

**Unsupported Geometry:**
- Double-curved (freeform) surfaces
- Complex NURBS surfaces with non-developable curvature

### Step 3: Choose Target Drafting View
Select how you want to place the unrolled geometry:

**Option A: Create New Drafting View**
- Select "Create New Drafting View"
- Enter a custom name (default: "Unrolled_Surface_01")
- The tool will automatically generate a unique name if one with that name already exists

**Option B: Use Existing Drafting View**
- Select "Use Existing Drafting View"
- Choose from the dropdown list of available drafting views
- The unrolled geometry will be placed in the selected view

### Step 4: Process
Click the **Process** button to execute the unrolling operation.

The tool will:
1. Analyze the selected geometry
2. Unroll/flatten the 3D curves into 2D
3. Create or use the specified drafting view
4. Generate a filled region representing the flattened surface
5. Switch the active view to show the result

## Technical Implementation

### Architecture

The feature follows the MVVM pattern and consists of:

1. **UnrollCurvedSurfaceCommand.cs** - Entry point command that inherits from BaseCommand
2. **UnrollCurvedSurfaceViewModel.cs** - Handles UI logic and Revit API calls via ExternalEvent
3. **UnrollCurvedSurfaceView.xaml** - WPF user interface
4. **GeometryUnrollService.cs** - Core geometry processing logic

### Geometry Processing

#### Cylindrical Faces
For cylindrical surfaces, the algorithm:
1. Identifies the cylinder axis, origin, and radius
2. Projects 3D points onto the cylinder
3. Calculates angle and height for each point
4. Maps to 2D coordinates: X = radius × angle, Y = height

#### Conical Faces
For conical surfaces:
1. Identifies the cone axis, origin, and half-angle
2. Calculates slant height for each point
3. Applies angle scaling for proper cone development
4. Maps to 2D using polar-to-Cartesian conversion

#### Planar Faces
For planar surfaces:
1. Creates a projection transform
2. Projects geometry onto the XY plane
3. Maintains original proportions

#### Ruled Surfaces
For ruled surfaces:
1. Tessellates edge curves
2. Accumulates arc length for X-axis
3. Uses Z-coordinate for Y-axis (simplified approach)

### Key Features

- **Multi-face support**: Process multiple faces in a single operation
- **Automatic spacing**: Multiple faces are automatically spaced horizontally
- **Error handling**: Gracefully handles unsupported geometry types
- **Statistics tracking**: Records usage and errors for analysis
- **Transaction management**: All Revit modifications are wrapped in transactions

## Code Examples

### Using the GeometryUnrollService Programmatically

```csharp
// Example: Unroll a face and create a filled region
Face face = GetSomeFace(); // Your method to get a face
ViewDrafting draftingView = GetDraftingView(); // Your drafting view

using (Transaction trans = new Transaction(doc, "Unroll Face"))
{
    trans.Start();
    
    // Unroll the face
    List<Curve> curves2D = GeometryUnrollService.Instance.UnrollFace(
        face, 
        insertionPoint: XYZ.Zero);
    
    // Create filled region
    FilledRegion region = GeometryUnrollService.Instance.CreateFilledRegion(
        doc, 
        draftingView, 
        curves2D);
    
    trans.Commit();
}
```

### Selecting Faces in Revit

```csharp
// Select faces from user
Selection selection = uidoc.Selection;
var references = selection.PickObjects(ObjectType.Face, "Select faces");

List<Face> faces = new List<Face>();
foreach (Reference reference in references)
{
    Element element = doc.GetElement(reference.ElementId);
    GeometryObject geoObj = element.GetGeometryObjectFromReference(reference);
    
    if (geoObj is Face face)
    {
        faces.Add(face);
    }
}
```

## Troubleshooting

### "Face type not supported"
This error occurs when trying to unroll complex freeform surfaces. Try selecting simpler cylindrical or ruled surfaces instead.

### "No filled region types found"
Ensure your Revit template has at least one filled region type defined. You can create one in the Revit UI under Annotate > Region > Filled Region.

### Geometry appears distorted
This may occur with complex ruled surfaces. The algorithm uses a simplified tessellation approach. For best results, use cylindrical or conical faces.

### Multiple faces overlap
When processing multiple faces, they are automatically spaced 10 feet apart horizontally. You can manually adjust their position in the drafting view after creation.

## Limitations

1. **Non-developable surfaces**: The tool cannot perfectly unroll surfaces with Gaussian curvature (e.g., spheres, toroids)
2. **Approximation**: Complex surfaces are tessellated and approximated
3. **No dimensioning**: Automatic dimension creation is not included in version 1.0
4. **Fill pattern only**: The result is a filled region; line work can be added manually

## Future Enhancements

Potential improvements for future versions:
- Support for double-curved surface approximation
- Automatic dimension generation
- Line style customization
- Seam line indication
- Export to DXF/DWG
- Material waste calculation
- Automatic nesting of multiple pieces

## API Compatibility

The feature is compatible with Revit 2022 and higher. The code uses conditional compilation to handle API differences:
- Revit 2022-2024: .NET Framework 4.8
- Revit 2025-2026: .NET 8.0

## Performance Considerations

- Processing time increases with geometry complexity
- Large numbers of faces (>10) may take several seconds
- Tessellation density affects accuracy vs. performance
- Drafting views with many filled regions may render slowly

## Support

For issues, questions, or feature requests, please contact the development team or file an issue in the project repository.

## Version History

### Version 1.0.0 (Initial Release)
- Face selection and unrolling
- Cylindrical face support
- Conical face support
- Planar face support
- Ruled surface support
- Drafting view creation
- Filled region generation
- Multi-face processing
- Statistics tracking
