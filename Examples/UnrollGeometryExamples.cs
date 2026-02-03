using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using effecurved.Services;
using effecurved.Models;

namespace effecurved.Examples
{
    /// <summary>
    /// Example code demonstrating how to use the Unroll Curved Surface functionality
    /// These examples show programmatic usage without the UI
    /// </summary>
    public class UnrollGeometryExamples
    {
        /// <summary>
        /// Example 1: Simple face unrolling
        /// Unrolls a single cylindrical face and creates a filled region
        /// </summary>
        public static void Example1_UnrollSingleFace(Document doc, Face face)
        {
            // Create a new drafting view
            ViewDrafting draftingView = CreateDraftingView(doc, "Unrolled Cylinder");

            using (Transaction trans = new Transaction(doc, "Unroll Single Face"))
            {
                trans.Start();

                try
                {
                    // Unroll the face at origin
                    List<Curve> curves2D = GeometryUnrollService.Instance.UnrollFace(
                        face,
                        insertionPoint: XYZ.Zero);

                    Log.Information($"Generated {curves2D.Count} curves from unrolling");

                    // Create filled region
                    FilledRegion region = GeometryUnrollService.Instance.CreateFilledRegion(
                        doc,
                        draftingView,
                        curves2D);

                    trans.Commit();
                    Log.Information("Successfully created filled region");
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    Log.Error(ex, "Failed to unroll face");
                    throw;
                }
            }
        }

        /// <summary>
        /// Example 2: Unroll multiple faces with spacing
        /// Processes multiple faces and spaces them horizontally
        /// </summary>
        public static void Example2_UnrollMultipleFaces(Document doc, List<Face> faces)
        {
            ViewDrafting draftingView = CreateDraftingView(doc, "Multiple Unrolled Surfaces");
            double spacing = 10.0; // 10 feet spacing between faces

            using (Transaction trans = new Transaction(doc, "Unroll Multiple Faces"))
            {
                trans.Start();

                try
                {
                    XYZ currentInsertionPoint = XYZ.Zero;

                    foreach (Face face in faces)
                    {
                        try
                        {
                            // Unroll face at current position
                            List<Curve> curves2D = GeometryUnrollService.Instance.UnrollFace(
                                face,
                                currentInsertionPoint);

                            // Create filled region for this face
                            GeometryUnrollService.Instance.CreateFilledRegion(
                                doc,
                                draftingView,
                                curves2D);

                            // Move insertion point for next face
                            currentInsertionPoint = currentInsertionPoint + new XYZ(spacing, 0, 0);

                            Log.Information($"Successfully unrolled face {faces.IndexOf(face) + 1}");
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, $"Failed to unroll face {faces.IndexOf(face) + 1}, skipping");
                            // Continue with next face even if one fails
                        }
                    }

                    trans.Commit();
                    Log.Information($"Successfully processed {faces.Count} faces");
                }
                catch (Exception ex)
                {
                    trans.RollBack();
                    Log.Error(ex, "Failed to process multiple faces");
                    throw;
                }
            }
        }

        /// <summary>
        /// Example 3: Get face from element and unroll
        /// Shows how to extract faces from an element
        /// </summary>
        public static void Example3_UnrollFromElement(Document doc, Element element)
        {
            List<Face> faces = GetFacesFromElement(element);

            if (!faces.Any())
            {
                Log.Warning("No faces found on element");
                return;
            }

            Log.Information($"Found {faces.Count} faces on element");

            // Unroll all cylindrical faces
            List<Face> cylindricalFaces = faces
                .Where(f => f is CylindricalFace)
                .ToList();

            if (cylindricalFaces.Any())
            {
                Example2_UnrollMultipleFaces(doc, cylindricalFaces);
            }
        }

        /// <summary>
        /// Example 4: Interactive face selection and unrolling
        /// Shows complete workflow with user selection
        /// </summary>
        public static void Example4_InteractiveUnroll(UIDocument uidoc)
        {
            Document doc = uidoc.Document;

            try
            {
                // Let user select faces
                Selection selection = uidoc.Selection;
                IList<Reference> references = selection.PickObjects(
                    ObjectType.Face,
                    "Select faces to unroll");

                // Extract faces from references
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

                if (!faces.Any())
                {
                    TaskDialog.Show("No Faces", "No valid faces were selected.");
                    return;
                }

                // Process the faces
                Example2_UnrollMultipleFaces(doc, faces);

                // Show success message
                TaskDialog.Show(
                    "Success",
                    $"Successfully unrolled {faces.Count} face(s) to drafting view.");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // User cancelled - do nothing
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in interactive unroll");
                TaskDialog.Show("Error", $"Failed to unroll geometry: {ex.Message}");
            }
        }

        /// <summary>
        /// Example 5: Unroll with custom settings (future enhancement placeholder)
        /// Demonstrates how UnrollSettings model could be used
        /// </summary>
        public static void Example5_UnrollWithSettings(Document doc, Face face, UnrollSettings settings)
        {
            ViewDrafting draftingView = CreateDraftingView(doc, "Custom Unroll");

            using (Transaction trans = new Transaction(doc, "Unroll With Settings"))
            {
                trans.Start();

                try
                {
                    // Unroll with custom insertion point from settings
                    List<Curve> curves2D = GeometryUnrollService.Instance.UnrollFace(
                        face,
                        settings.InsertionPoint);

                    // Create filled region with custom type if specified
                    FilledRegion region = GeometryUnrollService.Instance.CreateFilledRegion(
                        doc,
                        draftingView,
                        curves2D,
                        settings.FillRegionTypeId);

                    trans.Commit();

                    // Future: Add dimensions, detail lines, etc. based on settings
                    if (settings.AddDimensions)
                    {
                        // TODO: Add dimension creation logic
                    }

                    if (settings.CreateDetailLines)
                    {
                        // TODO: Add detail line creation logic
                    }
                }
                catch (Exception)
                {
                    trans.RollBack();
                    throw;
                }
            }
        }

        /// <summary>
        /// Example 6: Validate faces before unrolling
        /// Shows how to check if faces can be unrolled
        /// </summary>
        public static List<FaceInfo> Example6_ValidateFaces(Document doc, List<Face> faces)
        {
            List<FaceInfo> faceInfos = new List<FaceInfo>();

            foreach (Face face in faces)
            {
                FaceInfo info = new FaceInfo
                {
                    Face = face,
                    Area = face.Area
                };

                // Classify face type
                if (face is CylindricalFace)
                {
                    info.GeometryType = UnrollableGeometryType.Cylindrical;
                    info.IsUnrollable = true;
                }
                else if (face is ConicalFace)
                {
                    info.GeometryType = UnrollableGeometryType.Conical;
                    info.IsUnrollable = true;
                }
                else if (face is PlanarFace)
                {
                    info.GeometryType = UnrollableGeometryType.Planar;
                    info.IsUnrollable = true;
                }
                else if (face is RuledFace)
                {
                    info.GeometryType = UnrollableGeometryType.RuledSurface;
                    info.IsUnrollable = true;
                }
                else
                {
                    info.GeometryType = UnrollableGeometryType.Unknown;
                    info.IsUnrollable = false;
                    info.NotUnrollableReason = "Unsupported face type (freeform or complex NURBS)";
                }

                faceInfos.Add(info);
            }

            return faceInfos;
        }

        #region Helper Methods

        /// <summary>
        /// Helper: Creates a new drafting view
        /// </summary>
        private static ViewDrafting CreateDraftingView(Document doc, string viewName)
        {
            // Get drafting view family type
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            ViewFamilyType viewFamilyType = collector
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.Drafting);

            if (viewFamilyType == null)
            {
                throw new InvalidOperationException("No drafting view family type found");
            }

            // Create view
            ViewDrafting draftingView = ViewDrafting.Create(doc, viewFamilyType.Id);

            // Generate unique name
            string uniqueName = GenerateUniqueViewName(doc, viewName);
            draftingView.Name = uniqueName;

            return draftingView;
        }

        /// <summary>
        /// Helper: Generates a unique view name
        /// </summary>
        private static string GenerateUniqueViewName(Document doc, string baseName)
        {
            string viewName = baseName;
            int counter = 1;

            while (ViewExists(doc, viewName))
            {
                viewName = $"{baseName}_{counter:D2}";
                counter++;
            }

            return viewName;
        }

        /// <summary>
        /// Helper: Checks if a view exists
        /// </summary>
        private static bool ViewExists(Document doc, string name)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            return collector
                .OfClass(typeof(View))
                .Cast<View>()
                .Any(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Helper: Extracts all faces from an element
        /// </summary>
        private static List<Face> GetFacesFromElement(Element element)
        {
            List<Face> faces = new List<Face>();

            Options options = new Options
            {
                ComputeReferences = true,
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement geometryElement = element.get_Geometry(options);

            if (geometryElement == null)
                return faces;

            foreach (GeometryObject geoObj in geometryElement)
            {
                if (geoObj is Solid solid)
                {
                    foreach (Face face in solid.Faces)
                    {
                        faces.Add(face);
                    }
                }
                else if (geoObj is GeometryInstance instance)
                {
                    GeometryElement instGeometry = instance.GetInstanceGeometry();
                    foreach (GeometryObject instObj in instGeometry)
                    {
                        if (instObj is Solid instSolid)
                        {
                            foreach (Face face in instSolid.Faces)
                            {
                                faces.Add(face);
                            }
                        }
                    }
                }
            }

            return faces;
        }

        #endregion
    }
}
