using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace effecurved.Models
{
    /// <summary>
    /// Settings for unrolling geometry
    /// </summary>
    public class UnrollSettings
    {
        /// <summary>
        /// Insertion point for the unrolled geometry
        /// </summary>
        public XYZ InsertionPoint { get; set; } = XYZ.Zero;

        /// <summary>
        /// Whether to create detail lines in addition to filled region
        /// </summary>
        public bool CreateDetailLines { get; set; } = false;

        /// <summary>
        /// Whether to add dimensions to the unrolled geometry
        /// </summary>
        public bool AddDimensions { get; set; } = false;

        /// <summary>
        /// Spacing between multiple unrolled faces (in feet)
        /// </summary>
        public double SpacingBetweenFaces { get; set; } = 10.0;

        /// <summary>
        /// Tessellation accuracy for curved surfaces (higher = more accurate but slower)
        /// </summary>
        public int TessellationDensity { get; set; } = 50;

        /// <summary>
        /// Fill region type to use (null = use default)
        /// </summary>
        public ElementId FillRegionTypeId { get; set; } = null;

        /// <summary>
        /// Whether to group all created elements
        /// </summary>
        public bool GroupElements { get; set; } = false;
    }

    /// <summary>
    /// Result of an unroll operation
    /// </summary>
    public class UnrollResult
    {
        /// <summary>
        /// Whether the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if operation failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// The unrolled 2D curves
        /// </summary>
        public List<Curve> Curves { get; set; } = new List<Curve>();

        /// <summary>
        /// The created filled region (if any)
        /// </summary>
        public FilledRegion FilledRegion { get; set; }

        /// <summary>
        /// The drafting view containing the result
        /// </summary>
        public ViewDrafting TargetView { get; set; }

        /// <summary>
        /// Statistics about the unroll operation
        /// </summary>
        public UnrollStatistics Statistics { get; set; } = new UnrollStatistics();
    }

    /// <summary>
    /// Statistics about an unroll operation
    /// </summary>
    public class UnrollStatistics
    {
        /// <summary>
        /// Number of faces processed
        /// </summary>
        public int FaceCount { get; set; }

        /// <summary>
        /// Number of curves processed
        /// </summary>
        public int CurveCount { get; set; }

        /// <summary>
        /// Number of 2D curves generated
        /// </summary>
        public int GeneratedCurveCount { get; set; }

        /// <summary>
        /// Total area of unrolled surfaces (in square feet)
        /// </summary>
        public double TotalArea { get; set; }

        /// <summary>
        /// Total perimeter length (in feet)
        /// </summary>
        public double TotalPerimeter { get; set; }

        /// <summary>
        /// Processing time in milliseconds
        /// </summary>
        public long ProcessingTimeMs { get; set; }

        /// <summary>
        /// Types of surfaces encountered
        /// </summary>
        public List<string> SurfaceTypes { get; set; } = new List<string>();
    }

    /// <summary>
    /// Geometry type classification for unrolling
    /// </summary>
    public enum UnrollableGeometryType
    {
        /// <summary>
        /// Cylindrical surface
        /// </summary>
        Cylindrical,

        /// <summary>
        /// Conical surface
        /// </summary>
        Conical,

        /// <summary>
        /// Planar surface
        /// </summary>
        Planar,

        /// <summary>
        /// Ruled surface (can be formed by straight lines)
        /// </summary>
        RuledSurface,

        /// <summary>
        /// Extruded surface
        /// </summary>
        Extruded,

        /// <summary>
        /// Revolved surface
        /// </summary>
        Revolved,

        /// <summary>
        /// Unknown or unsupported type
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Information about a selected face for unrolling
    /// </summary>
    public class FaceInfo
    {
        /// <summary>
        /// The face reference
        /// </summary>
        public Face Face { get; set; }

        /// <summary>
        /// The element containing the face
        /// </summary>
        public Element OwnerElement { get; set; }

        /// <summary>
        /// Classified geometry type
        /// </summary>
        public UnrollableGeometryType GeometryType { get; set; }

        /// <summary>
        /// Whether this face can be unrolled
        /// </summary>
        public bool IsUnrollable { get; set; }

        /// <summary>
        /// Reason if not unrollable
        /// </summary>
        public string NotUnrollableReason { get; set; }

        /// <summary>
        /// Approximate area of the face (in square feet)
        /// </summary>
        public double Area { get; set; }

        /// <summary>
        /// Display name for the face
        /// </summary>
        public string DisplayName
        {
            get
            {
                if (OwnerElement != null)
                {
                    string elementName = OwnerElement.Name;
                    string category = OwnerElement.Category?.Name ?? "Unknown";
                    return $"{category}: {elementName} ({GeometryType})";
                }
                return $"Face ({GeometryType})";
            }
        }
    }
}
