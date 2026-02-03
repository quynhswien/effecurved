using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace effecurved.Services
{
    /// <summary>
    /// Service for unrolling/flattening curved geometry into 2D representations
    /// </summary>
    public class GeometryUnrollService
    {
        private static GeometryUnrollService _instance;
        public static GeometryUnrollService Instance => _instance ??= new GeometryUnrollService();

        private GeometryUnrollService() { }

        /// <summary>
        /// Unrolls a face into 2D curves suitable for drafting view
        /// </summary>
        /// <param name="face">The face to unroll</param>
        /// <param name="insertionPoint">Optional insertion point for the unrolled geometry</param>
        /// <returns>List of 2D curves representing the unrolled face</returns>
        public List<Curve> UnrollFace(Face face, XYZ insertionPoint = null)
        {
            Log.Debug("Starting face unroll operation");
            
            insertionPoint = insertionPoint ?? XYZ.Zero;
            List<Curve> result2DCurves = new List<Curve>();

            try
            {
                // Check face type and apply appropriate unrolling strategy
                if (IsCylindricalFace(face, out XYZ axis, out XYZ origin, out double radius))
                {
                    Log.Debug($"Processing cylindrical face with radius: {radius}");
                    result2DCurves = UnrollCylindricalFaceExact(face, axis, origin, radius, insertionPoint);
                }
                else if (IsConicalFace(face, out axis, out origin, out double halfAngle))
                {
                    Log.Debug($"Processing conical face with half angle: {halfAngle}");
                    result2DCurves = UnrollConicalFace(face, axis, origin, halfAngle, insertionPoint);
                }
                else if (IsPlanarFace(face, out XYZ normal))
                {
                    Log.Debug("Processing planar face");
                    result2DCurves = UnrollPlanarFace(face, normal, insertionPoint);
                }
                else if (IsRuledSurface(face))
                {
                    Log.Debug("Processing ruled surface");
                    result2DCurves = UnrollRuledSurface(face, insertionPoint);
                }
                else
                {
                    Log.Warning("Face type not supported for unrolling");
                    throw new NotSupportedException("This face type is not supported for unrolling. Only cylindrical, conical, planar, and ruled surfaces are currently supported.");
                }

                Log.Debug($"Unroll operation completed with {result2DCurves.Count} curves");
                return result2DCurves;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error unrolling face");
                throw;
            }
        }

        /// <summary>
        /// Checks if a face is cylindrical
        /// </summary>
        private bool IsCylindricalFace(Face face, out XYZ axis, out XYZ origin, out double radius)
        {
            axis = null;
            origin = null;
            radius = 0;

            if (face is CylindricalFace cylFace)
            {
                axis = cylFace.Axis.Normalize();
                origin = cylFace.Origin;
                
                // Calculate radius from a point on the surface
                BoundingBoxUV bbox = cylFace.GetBoundingBox();
                UV midUV = new UV((bbox.Min.U + bbox.Max.U) / 2, (bbox.Min.V + bbox.Max.V) / 2);
                XYZ pointOnSurface = cylFace.Evaluate(midUV);
                
                // Calculate distance from point to axis (this is the radius)
                XYZ vectorToPoint = pointOnSurface - origin;
                XYZ radialComponent = vectorToPoint - (vectorToPoint.DotProduct(axis) * axis);
                radius = radialComponent.GetLength();
                
                return true;
            }

            // Try to detect cylindrical surface from RevitFace
            if (face is RevolvedFace revolvedFace)
            {
                // Check if it's a full or partial cylinder
                BoundingBoxUV bbox = face.GetBoundingBox();
                // Additional checks could be added here
                return false; // For now, return false for revolved faces
            }

            return false;
        }

        /// <summary>
        /// Checks if a face is conical
        /// </summary>
        private bool IsConicalFace(Face face, out XYZ axis, out XYZ origin, out double halfAngle)
        {
            axis = null;
            origin = null;
            halfAngle = 0;

            if (face is ConicalFace conicalFace)
            {
                axis = conicalFace.Axis.Normalize();
                origin = conicalFace.Origin;
                
                // Calculate half angle from the geometry
                // Get two points on the surface at different heights
                BoundingBoxUV bbox = conicalFace.GetBoundingBox();
                UV uv1 = new UV((bbox.Min.U + bbox.Max.U) / 2, bbox.Min.V);
                UV uv2 = new UV((bbox.Min.U + bbox.Max.U) / 2, bbox.Max.V);
                
                XYZ point1 = conicalFace.Evaluate(uv1);
                XYZ point2 = conicalFace.Evaluate(uv2);
                
                // Calculate half angle
                XYZ vector1 = (point1 - origin).Normalize();
                double angle1 = Math.Acos(Math.Abs(vector1.DotProduct(axis)));
                XYZ vector2 = (point2 - origin).Normalize();
                double angle2 = Math.Acos(Math.Abs(vector2.DotProduct(axis)));
                
                halfAngle = Math.Max(angle1, angle2);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if a face is planar
        /// </summary>
        private bool IsPlanarFace(Face face, out XYZ normal)
        {
            normal = null;

            if (face is PlanarFace planarFace)
            {
                normal = planarFace.FaceNormal;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if a face is a ruled surface
        /// </summary>
        private bool IsRuledSurface(Face face)
        {
            if (face is RuledFace)
            {
                return true;
            }

            // Additional heuristic checks could be added
            return false;
        }

        /// <summary>
        /// Unrolls a cylindrical face
        /// </summary>
        private List<Curve> UnrollCylindricalFace(Face face, XYZ axis, XYZ origin, double radius, XYZ insertionPoint)
        {
            List<Curve> curves2D = new List<Curve>();
            
            // Get edge loops from the face - usually only use the first (outer) loop
            EdgeArrayArray edgeLoops = face.EdgeLoops;
            
            if (edgeLoops.Size == 0)
            {
                Log.Warning("No edge loops found on face");
                return curves2D;
            }
            
            // Process only the largest loop (outer boundary)
            EdgeArray largestLoop = null;
            int maxEdges = 0;
            
            foreach (EdgeArray edgeLoop in edgeLoops)
            {
                if (edgeLoop.Size > maxEdges)
                {
                    maxEdges = edgeLoop.Size;
                    largestLoop = edgeLoop;
                }
            }
            
            if (largestLoop == null)
            {
                Log.Warning("No valid edge loop found");
                return curves2D;
            }
            
            // Sort edges to form continuous boundary
            List<Edge> sortedEdges = SortEdgesIntoLoop(largestLoop);
            Log.Debug($"Sorted {sortedEdges.Count} edges into continuous loop");
            
            List<XYZ> points2D = new List<XYZ>();
            
            // Process each edge in sorted order
            for (int edgeIndex = 0; edgeIndex < sortedEdges.Count; edgeIndex++)
            {
                Edge edge = sortedEdges[edgeIndex];
                Curve curve3D = edge.AsCurve();
                // Tessellate the curve for accurate unrolling
                IList<XYZ> tessellatedPoints = curve3D.Tessellate();
                
                // Skip first point of subsequent edges to avoid duplicates
                int startIndex = (edgeIndex == 0) ? 0 : 1;
                
                for (int i = startIndex; i < tessellatedPoints.Count; i++)
                {
                    XYZ point3D = tessellatedPoints[i];
                    
                    // Project point onto cylinder axis to get height
                    XYZ vectorToPoint = point3D - origin;
                    double height = vectorToPoint.DotProduct(axis);
                    
                    // Calculate angle around cylinder
                    XYZ radialVector = vectorToPoint - (height * axis);
                    double angle = CalculateAngleFromAxis(radialVector, axis, XYZ.BasisX);
                    
                    // Convert to 2D coordinates
                    // X = arc length = radius * angle
                    // Y = height along axis
                    double x = radius * angle;
                    double y = height;
                    
                    // CRITICAL: Force Z = 0 for drafting view (must be planar)
                    XYZ point2D = new XYZ(x + insertionPoint.X, y + insertionPoint.Y, 0);
                    points2D.Add(point2D);
                }
            }
            
            // Create 2D curves from points - this will create a single closed loop
            curves2D = CreateCurvesFromPoints(points2D);
            
            Log.Debug($"Created {curves2D.Count} curves for cylindrical face");
            return curves2D;
        }

        /// <summary>
        /// Unrolls a cylindrical face using EXACT edge lengths (no tessellation).
        /// Boundary = 4 lines: two horizontal (arc lengths) and two vertical (heights).
        /// Dimensions match Revit's reported length/height exactly.
        /// </summary>
        private List<Curve> UnrollCylindricalFaceExact(Face face, XYZ axis, XYZ origin, double radius, XYZ insertionPoint)
        {
            List<Curve> curves2D = new List<Curve>();
            
            EdgeArrayArray edgeLoops = face.EdgeLoops;
            if (edgeLoops.Size == 0)
            {
                Log.Warning("No edge loops found on face");
                return curves2D;
            }
            
            EdgeArray largestLoop = null;
            int maxEdges = 0;
            foreach (EdgeArray edgeLoop in edgeLoops)
            {
                if (edgeLoop.Size > maxEdges)
                {
                    maxEdges = edgeLoop.Size;
                    largestLoop = edgeLoop;
                }
            }
            if (largestLoop == null) return curves2D;
            
            List<Edge> sortedEdges = SortEdgesIntoLoop(largestLoop);
            
            // For each edge: get EXACT length via curve.Length and classify as "along axis" (→ vertical in 2D) or "arc" (→ horizontal in 2D)
            List<double> lengths = new List<double>();
            List<bool> isAlongAxis = new List<bool>();
            
            foreach (Edge edge in sortedEdges)
            {
                Curve c = edge.AsCurve();
                double len = c.Length; // Exact length from Revit (no tessellation error)
                lengths.Add(len);
                
                XYZ tangent = (c.GetEndPoint(1) - c.GetEndPoint(0)).Normalize();
                double dotWithAxis = Math.Abs(tangent.DotProduct(axis.Normalize()));
                isAlongAxis.Add(dotWithAxis > 0.9);
            }
            
            // Unrolled shape: rectangle (or trapezoid). Use first arc length = width, first axis length = height.
            double width = 0;
            double height = 0;
            for (int i = 0; i < lengths.Count; i++)
            {
                if (isAlongAxis[i]) { height = lengths[i]; break; }
            }
            for (int i = 0; i < lengths.Count; i++)
            {
                if (!isAlongAxis[i]) { width = lengths[i]; break; }
            }
            // If we have two arcs / two axis edges, same width/height for both pairs; rectangle is correct.
            // If arcs differ (trapezoid), we use first arc and first axis for a rectangle; optional: build trapezoid from all 4.
            double ox = insertionPoint.X;
            double oy = insertionPoint.Y;
            double z = 0;
            
            XYZ p0 = new XYZ(ox, oy, z);
            XYZ p1 = new XYZ(ox + width, oy, z);
            XYZ p2 = new XYZ(ox + width, oy + height, z);
            XYZ p3 = new XYZ(ox, oy + height, z);
            
            curves2D.Add(Line.CreateBound(p0, p1));
            curves2D.Add(Line.CreateBound(p1, p2));
            curves2D.Add(Line.CreateBound(p2, p3));
            curves2D.Add(Line.CreateBound(p3, p0));
            
            Log.Debug($"Unroll (exact): width={width} (arc length), height={height} (axis length) — no tessellation");
            return curves2D;
        }

        /// <summary>
        /// Unrolls a conical face
        /// </summary>
        private List<Curve> UnrollConicalFace(Face face, XYZ axis, XYZ origin, double halfAngle, XYZ insertionPoint)
        {
            List<Curve> curves2D = new List<Curve>();
            
            EdgeArrayArray edgeLoops = face.EdgeLoops;
            
            if (edgeLoops.Size == 0)
            {
                Log.Warning("No edge loops found on conical face");
                return curves2D;
            }
            
            // Find largest loop
            EdgeArray largestLoop = null;
            int maxEdges = 0;
            
            foreach (EdgeArray edgeLoop in edgeLoops)
            {
                if (edgeLoop.Size > maxEdges)
                {
                    maxEdges = edgeLoop.Size;
                    largestLoop = edgeLoop;
                }
            }
            
            if (largestLoop == null)
            {
                Log.Warning("No valid edge loop found on conical face");
                return curves2D;
            }
            
            List<XYZ> points2D = new List<XYZ>();
            
            foreach (Edge edge in largestLoop)
            {
                Curve curve3D = edge.AsCurve();
                IList<XYZ> tessellatedPoints = curve3D.Tessellate();
                
                foreach (XYZ point3D in tessellatedPoints)
                {
                    // Calculate cone unrolling
                    XYZ vectorToPoint = point3D - origin;
                    double height = vectorToPoint.DotProduct(axis);
                    
                    // Slant height on the cone surface
                    double slantHeight = height / Math.Cos(halfAngle);
                    
                    // Angle around cone
                    XYZ radialVector = vectorToPoint - (height * axis);
                    double angle = CalculateAngleFromAxis(radialVector, axis, XYZ.BasisX);
                    
                    // Scale angle for cone unrolling
                    double unrolledAngle = angle * Math.Sin(halfAngle);
                    
                    // Convert to 2D polar coordinates then to Cartesian
                    double x = slantHeight * Math.Cos(unrolledAngle);
                    double y = slantHeight * Math.Sin(unrolledAngle);
                    
                    // CRITICAL: Force Z = 0 for drafting view (must be planar)
                    XYZ point2D = new XYZ(x + insertionPoint.X, y + insertionPoint.Y, 0);
                    points2D.Add(point2D);
                }
            }
            
            curves2D = CreateCurvesFromPoints(points2D);
            Log.Debug($"Created {curves2D.Count} curves for conical face");
            
            return curves2D;
        }

        /// <summary>
        /// Unrolls a planar face (simply projects to 2D)
        /// </summary>
        private List<Curve> UnrollPlanarFace(Face face, XYZ normal, XYZ insertionPoint)
        {
            List<Curve> curves2D = new List<Curve>();
            
            // Create a transform to project to XY plane
            Transform transform = CreateProjectionTransform(normal);
            
            EdgeArrayArray edgeLoops = face.EdgeLoops;
            
            if (edgeLoops.Size == 0)
            {
                Log.Warning("No edge loops found on planar face");
                return curves2D;
            }
            
            // Find largest loop
            EdgeArray largestLoop = null;
            int maxEdges = 0;
            
            foreach (EdgeArray edgeLoop in edgeLoops)
            {
                if (edgeLoop.Size > maxEdges)
                {
                    maxEdges = edgeLoop.Size;
                    largestLoop = edgeLoop;
                }
            }
            
            if (largestLoop == null)
            {
                Log.Warning("No valid edge loop found on planar face");
                return curves2D;
            }
            
            List<XYZ> points2D = new List<XYZ>();
            
            foreach (Edge edge in largestLoop)
            {
                Curve curve3D = edge.AsCurve();
                Curve transformedCurve = curve3D.CreateTransformed(transform);
                
                // Flatten Z coordinate and offset by insertion point
                // CRITICAL: Force Z = 0 for drafting view (must be planar)
                XYZ start = new XYZ(
                    transformedCurve.GetEndPoint(0).X + insertionPoint.X,
                    transformedCurve.GetEndPoint(0).Y + insertionPoint.Y,
                    0);
                XYZ end = new XYZ(
                    transformedCurve.GetEndPoint(1).X + insertionPoint.X,
                    transformedCurve.GetEndPoint(1).Y + insertionPoint.Y,
                    0);
                
                points2D.Add(start);
                points2D.Add(end);
            }
            
            curves2D = CreateCurvesFromPoints(points2D);
            Log.Debug($"Created {curves2D.Count} curves for planar face");
            
            return curves2D;
        }

        /// <summary>
        /// Unrolls a ruled surface
        /// </summary>
        private List<Curve> UnrollRuledSurface(Face face, XYZ insertionPoint)
        {
            List<Curve> curves2D = new List<Curve>();
            
            // Get edge loops - use only the largest (outer) loop
            EdgeArrayArray edgeLoops = face.EdgeLoops;
            
            if (edgeLoops.Size == 0)
            {
                Log.Warning("No edge loops found on ruled surface");
                return curves2D;
            }
            
            // Find largest loop
            EdgeArray largestLoop = null;
            int maxEdges = 0;
            
            foreach (EdgeArray edgeLoop in edgeLoops)
            {
                if (edgeLoop.Size > maxEdges)
                {
                    maxEdges = edgeLoop.Size;
                    largestLoop = edgeLoop;
                }
            }
            
            if (largestLoop == null)
            {
                Log.Warning("No valid edge loop found on ruled surface");
                return curves2D;
            }
            
            // Simplified approach: tessellate and approximate unrolling
            List<XYZ> points2D = new List<XYZ>();
            double accumulatedLength = 0;
            XYZ previousPoint = null;
            
            foreach (Edge edge in largestLoop)
            {
                Curve curve3D = edge.AsCurve();
                IList<XYZ> tessellatedPoints = curve3D.Tessellate();
                
                for (int i = 0; i < tessellatedPoints.Count; i++)
                {
                    if (previousPoint != null)
                    {
                        // Calculate length between consecutive points
                        accumulatedLength += tessellatedPoints[i].DistanceTo(previousPoint);
                    }
                    previousPoint = tessellatedPoints[i];
                    
                    // Simple 2D projection: X = accumulated length, Y = Z coordinate
                    double x = accumulatedLength;
                    double y = tessellatedPoints[i].Z;
                    
                    // CRITICAL: Force Z = 0 for drafting view (must be planar)
                    XYZ point2D = new XYZ(x + insertionPoint.X, y + insertionPoint.Y, 0);
                    points2D.Add(point2D);
                }
            }
            
            curves2D = CreateCurvesFromPoints(points2D);
            Log.Debug($"Created {curves2D.Count} curves for ruled surface");
            
            return curves2D;
        }

        /// <summary>
        /// Sorts edges from an EdgeArray into a continuous loop
        /// </summary>
        private List<Edge> SortEdgesIntoLoop(EdgeArray edgeArray)
        {
            List<Edge> sortedEdges = new List<Edge>();
            List<Edge> remainingEdges = new List<Edge>();
            
            // Convert EdgeArray to List
            foreach (Edge edge in edgeArray)
            {
                remainingEdges.Add(edge);
            }
            
            if (remainingEdges.Count == 0)
                return sortedEdges;
            
            // Start with first edge
            sortedEdges.Add(remainingEdges[0]);
            remainingEdges.RemoveAt(0);
            
            // Find connecting edges
            while (remainingEdges.Count > 0)
            {
                Edge lastEdge = sortedEdges[sortedEdges.Count - 1];
                Curve lastCurve = lastEdge.AsCurve();
                XYZ lastEndPoint = lastCurve.GetEndPoint(1);
                
                bool found = false;
                for (int i = 0; i < remainingEdges.Count; i++)
                {
                    Edge candidate = remainingEdges[i];
                    Curve candidateCurve = candidate.AsCurve();
                    XYZ candidateStart = candidateCurve.GetEndPoint(0);
                    XYZ candidateEnd = candidateCurve.GetEndPoint(1);
                    
                    // Check if candidate connects to last edge
                    if (lastEndPoint.IsAlmostEqualTo(candidateStart, 0.01) || 
                        lastEndPoint.IsAlmostEqualTo(candidateEnd, 0.01))
                    {
                        sortedEdges.Add(candidate);
                        remainingEdges.RemoveAt(i);
                        found = true;
                        break;
                    }
                }
                
                if (!found)
                {
                    Log.Warning($"Could not find connecting edge, {remainingEdges.Count} edges remaining");
                    // Add remaining edges anyway to avoid infinite loop
                    sortedEdges.AddRange(remainingEdges);
                    break;
                }
            }
            
            return sortedEdges;
        }

        /// <summary>
        /// Calculates angle of a vector from a reference axis in a perpendicular plane
        /// </summary>
        private double CalculateAngleFromAxis(XYZ vector, XYZ axis, XYZ referenceDirection)
        {
            if (vector.IsAlmostEqualTo(XYZ.Zero))
                return 0;
            
            // Project reference direction onto plane perpendicular to axis
            XYZ projectedRef = (referenceDirection - referenceDirection.DotProduct(axis) * axis).Normalize();
            
            // Calculate angle using atan2 for full circle
            double dotProduct = vector.Normalize().DotProduct(projectedRef);
            XYZ crossProduct = vector.Normalize().CrossProduct(projectedRef);
            double crossDot = crossProduct.DotProduct(axis);
            
            double angle = Math.Atan2(crossDot, dotProduct);
            if (angle < 0) angle += 2 * Math.PI;
            
            return angle;
        }

        /// <summary>
        /// Creates a projection transform to map 3D geometry to 2D
        /// </summary>
        private Transform CreateProjectionTransform(XYZ normal)
        {
            // Create coordinate system with Z aligned to normal
            XYZ zAxis = normal.Normalize();
            XYZ xAxis = zAxis.IsAlmostEqualTo(XYZ.BasisZ) || zAxis.IsAlmostEqualTo(-XYZ.BasisZ)
                ? XYZ.BasisX
                : XYZ.BasisZ.CrossProduct(zAxis).Normalize();
            XYZ yAxis = zAxis.CrossProduct(xAxis).Normalize();
            
            Transform transform = Transform.Identity;
            transform.BasisX = xAxis;
            transform.BasisY = yAxis;
            transform.BasisZ = zAxis;
            
            return transform;
        }

        /// <summary>
        /// Creates line segments from a list of points
        /// </summary>
        private List<Curve> CreateCurvesFromPoints(List<XYZ> points)
        {
            List<Curve> curves = new List<Curve>();
            
            if (points.Count < 3)
            {
                Log.Warning($"Not enough points to create curves: {points.Count}");
                return curves;
            }
            
            // Remove duplicate consecutive points
            List<XYZ> cleanPoints = new List<XYZ>();
            cleanPoints.Add(points[0]);
            
            for (int i = 1; i < points.Count; i++)
            {
                if (!points[i].IsAlmostEqualTo(cleanPoints[cleanPoints.Count - 1], 0.001))
                {
                    cleanPoints.Add(points[i]);
                }
            }
            
            // Close the loop if not already closed
            if (!cleanPoints[0].IsAlmostEqualTo(cleanPoints[cleanPoints.Count - 1], 0.001))
            {
                cleanPoints.Add(cleanPoints[0]);
            }
            else if (cleanPoints.Count > 1 && cleanPoints[0].IsAlmostEqualTo(cleanPoints[cleanPoints.Count - 1], 0.001))
            {
                // Remove duplicate closing point
                cleanPoints.RemoveAt(cleanPoints.Count - 1);
            }
            
            if (cleanPoints.Count < 3)
            {
                Log.Warning($"Not enough clean points to create curves: {cleanPoints.Count}");
                return curves;
            }
            
            // Create curves ensuring continuity
            for (int i = 0; i < cleanPoints.Count; i++)
            {
                int nextIndex = (i + 1) % cleanPoints.Count;
                XYZ p1 = cleanPoints[i];
                XYZ p2 = cleanPoints[nextIndex];
                
                double distance = p1.DistanceTo(p2);
                if (distance > 0.001) // Minimum length threshold
                {
                    try
                    {
                        curves.Add(Line.CreateBound(p1, p2));
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, $"Failed to create line from point {i} to {nextIndex}, distance: {distance}");
                    }
                }
            }
            
            Log.Debug($"Created {curves.Count} curves from {cleanPoints.Count} points");
            return curves;
        }

        /// <summary>
        /// Creates a CurveLoop from a list of curves, ensuring continuity
        /// </summary>
        public CurveLoop CreateCurveLoop(List<Curve> curves)
        {
            try
            {
                if (curves == null || curves.Count < 3)
                {
                    throw new InvalidOperationException($"Need at least 3 curves to create a loop, got {curves?.Count ?? 0}");
                }
                
                // Sort curves to ensure continuity
                List<Curve> sortedCurves = new List<Curve>();
                List<Curve> remainingCurves = new List<Curve>(curves);
                
                // Start with first curve
                sortedCurves.Add(remainingCurves[0]);
                remainingCurves.RemoveAt(0);
                
                // Find connected curves
                while (remainingCurves.Count > 0)
                {
                    Curve lastCurve = sortedCurves[sortedCurves.Count - 1];
                    XYZ lastPoint = lastCurve.GetEndPoint(1);
                    
                    bool found = false;
                    for (int i = 0; i < remainingCurves.Count; i++)
                    {
                        Curve candidate = remainingCurves[i];
                        XYZ startPoint = candidate.GetEndPoint(0);
                        XYZ endPoint = candidate.GetEndPoint(1);
                        
                        // Check if curve connects to last point
                        if (lastPoint.IsAlmostEqualTo(startPoint, 0.01))
                        {
                            sortedCurves.Add(candidate);
                            remainingCurves.RemoveAt(i);
                            found = true;
                            break;
                        }
                        // Check if reversed curve connects
                        else if (lastPoint.IsAlmostEqualTo(endPoint, 0.01))
                        {
                            // Create reversed curve
                            Line reversedLine = Line.CreateBound(endPoint, startPoint);
                            sortedCurves.Add(reversedLine);
                            remainingCurves.RemoveAt(i);
                            found = true;
                            break;
                        }
                    }
                    
                    if (!found)
                    {
                        Log.Warning($"Could not find connecting curve, {remainingCurves.Count} curves remaining");
                        break;
                    }
                }
                
                // Verify loop is closed
                XYZ firstPoint = sortedCurves[0].GetEndPoint(0);
                XYZ finalPoint = sortedCurves[sortedCurves.Count - 1].GetEndPoint(1);
                
                if (!firstPoint.IsAlmostEqualTo(finalPoint, 0.01))
                {
                    Log.Warning($"Loop not perfectly closed, gap: {firstPoint.DistanceTo(finalPoint)}");
                    // Try to close the gap if small enough
                    if (firstPoint.DistanceTo(finalPoint) < 0.1)
                    {
                        sortedCurves.Add(Line.CreateBound(finalPoint, firstPoint));
                    }
                }
                
                // Create curve loop
                CurveLoop curveLoop = new CurveLoop();
                foreach (Curve curve in sortedCurves)
                {
                    curveLoop.Append(curve);
                }
                
                Log.Debug($"Successfully created curve loop with {sortedCurves.Count} curves");
                return curveLoop;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating curve loop");
                throw;
            }
        }

        /// <summary>
        /// Creates a filled region in a drafting view from curves
        /// </summary>
        public FilledRegion CreateFilledRegion(Document doc, ViewDrafting draftingView, List<Curve> curves, ElementId fillRegionTypeId = null)
        {
            try
            {
                // Get a fill region type if not provided
                if (fillRegionTypeId == null)
                {
                    fillRegionTypeId = GetFirstFilledRegionType(doc);
                    if (fillRegionTypeId == null)
                    {
                        throw new InvalidOperationException("No filled region types found in the document");
                    }
                }

                // DEBUG: Check if all curves are planar (Z = 0)
                double minZ = double.MaxValue;
                double maxZ = double.MinValue;
                foreach (Curve curve in curves)
                {
                    double z1 = curve.GetEndPoint(0).Z;
                    double z2 = curve.GetEndPoint(1).Z;
                    minZ = Math.Min(minZ, Math.Min(z1, z2));
                    maxZ = Math.Max(maxZ, Math.Max(z1, z2));
                }
                Log.Debug($"Curve Z range: min={minZ}, max={maxZ}, diff={maxZ - minZ}");
                
                // If curves are not at Z=0, create new curves at Z=0
                if (Math.Abs(maxZ - minZ) > 0.001 || Math.Abs(minZ) > 0.001)
                {
                    Log.Warning($"Curves not planar at Z=0, flattening them. Current Z range: {minZ} to {maxZ}");
                    List<Curve> flatCurves = new List<Curve>();
                    foreach (Curve curve in curves)
                    {
                        XYZ p1 = curve.GetEndPoint(0);
                        XYZ p2 = curve.GetEndPoint(1);
                        XYZ p1Flat = new XYZ(p1.X, p1.Y, 0);
                        XYZ p2Flat = new XYZ(p2.X, p2.Y, 0);
                        flatCurves.Add(Line.CreateBound(p1Flat, p2Flat));
                    }
                    curves = flatCurves;
                    Log.Debug("Created flattened curves at Z=0");
                }

                // DEBUG: Create detail lines first to test if curves are valid
                // Create curve loop
                CurveLoop curveLoop = CreateCurveLoop(curves);
                
                // Validate curve loop
                bool isClosed = !curveLoop.IsOpen();
                Log.Debug($"Curve loop closed: {isClosed}");
                
                if (!isClosed)
                {
                    Log.Error("Curve loop is OPEN - cannot create filled region");
                    throw new InvalidOperationException("Curve loop is not closed");
                }
                
                // Check orientation
                bool isCounterclockwise = curveLoop.IsCounterclockwise(XYZ.BasisZ);
                Log.Debug($"Curve loop counterclockwise: {isCounterclockwise}");
                
                // Flip if needed
                if (!isCounterclockwise)
                {
                    Log.Warning("Flipping curve loop to counterclockwise");
                    curveLoop.Flip();
                }
                
                List<CurveLoop> curveLoops = new List<CurveLoop> { curveLoop };

                // Try to create filled region
                try
                {
                    FilledRegion filledRegion = FilledRegion.Create(doc, fillRegionTypeId, draftingView.Id, curveLoops);
                    Log.Debug($"Created filled region in view {draftingView.Name}");
                    return filledRegion;
                }
                catch (Exception)
                {
                    Log.Error($"FilledRegion.Create failed. Loop has {curves.Count} curves, isClosed={isClosed}, isCCW={isCounterclockwise}");
                    
                    // Log first and last curve for debugging
                    if (curves.Count > 0)
                    {
                        var first = curves[0];
                        var last = curves[curves.Count - 1];
                        Log.Error($"First curve: ({first.GetEndPoint(0).X:F3}, {first.GetEndPoint(0).Y:F3}) → ({first.GetEndPoint(1).X:F3}, {first.GetEndPoint(1).Y:F3})");
                        Log.Error($"Last curve: ({last.GetEndPoint(0).X:F3}, {last.GetEndPoint(0).Y:F3}) → ({last.GetEndPoint(1).X:F3}, {last.GetEndPoint(1).Y:F3})");
                    }
                    throw;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating filled region");
                throw;
            }
        }

        /// <summary>
        /// Gets the first available filled region type in the document
        /// </summary>
        private ElementId GetFirstFilledRegionType(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            FilledRegionType filledRegionType = collector
                .OfClass(typeof(FilledRegionType))
                .FirstOrDefault() as FilledRegionType;
            
            return filledRegionType?.Id;
        }
    }
}
