using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using effecurved.Models;

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
        /// Unrolls a face into 2D curves (outer + inner loops for openings).
        /// </summary>
        public FaceUnrollResult UnrollFace(Face face, XYZ insertionPoint = null)
        {
            Log.Debug("Starting face unroll operation");
            insertionPoint = insertionPoint ?? XYZ.Zero;
            var result = new FaceUnrollResult();
            try
            {
                if (IsCylindricalFace(face, out XYZ axis, out XYZ origin, out double radius))
                {
                    Log.Debug($"Processing cylindrical face with radius: {radius}");
                    result = UnrollCylindricalFaceWithOpenings(face, axis, origin, radius, insertionPoint);
                }
                else if (IsConicalFace(face, out axis, out origin, out double halfAngle))
                {
                    Log.Debug($"Processing conical face with half angle: {halfAngle}");
                    result.OuterCurves = UnrollConicalFace(face, axis, origin, halfAngle, insertionPoint);
                }
                else if (IsPlanarFace(face, out XYZ normal))
                {
                    Log.Debug("Processing planar face");
                    result.OuterCurves = UnrollPlanarFace(face, normal, insertionPoint);
                }
                else if (IsRuledSurface(face))
                {
                    Log.Debug("Processing ruled surface");
                    result.OuterCurves = UnrollRuledSurface(face, insertionPoint);
                }
                else
                {
                    // Spline/ellipse/revolved/Hermite: thử unroll theo boundary tessellation
                    Log.Debug("Face not cylindrical/conical/planar/ruled; trying boundary tessellation (spline/ellipse)");
                    result.OuterCurves = UnrollByBoundaryTessellation(face, insertionPoint);
                    if (result.OuterCurves == null || result.OuterCurves.Count < 3)
                    {
                        Log.Warning("Face type not supported for unrolling");
                        throw new NotSupportedException("This face type is not supported for unrolling.");
                    }
                }
                Log.Debug($"Unroll completed: outer={result.OuterCurves?.Count ?? 0}, inner loops={result.InnerLoops?.Count ?? 0}");
                return result;
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
                    
                    // Convert to 2D coordinates: X = arc length, Y = height (flipped so not rotated 180°)
                    double x = radius * angle;
                    double y = height;
                    XYZ point2D = new XYZ(x + insertionPoint.X, insertionPoint.Y - y, 0);
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
            // Y flipped so 3D top → 2D top (no 180° rotation)
            XYZ p0 = new XYZ(ox, oy, z);
            XYZ p1 = new XYZ(ox + width, oy, z);
            XYZ p2 = new XYZ(ox + width, oy - height, z);
            XYZ p3 = new XYZ(ox, oy - height, z);
            
            curves2D.Add(Line.CreateBound(p0, p1));
            curves2D.Add(Line.CreateBound(p1, p2));
            curves2D.Add(Line.CreateBound(p2, p3));
            curves2D.Add(Line.CreateBound(p3, p0));
            
            Log.Debug($"Unroll (exact): width={width}, height={height} — no tessellation");
            return curves2D;
        }

        /// <summary>
        /// Unrolls cylindrical face with outer + inner loops (openings).
        /// When outer has 4 edges use exact rectangle; when outer has more than 4 edges (edit profile, cutting blocks) tessellate to preserve complex shape.
        /// </summary>
        private FaceUnrollResult UnrollCylindricalFaceWithOpenings(Face face, XYZ axis, XYZ origin, double radius, XYZ insertionPoint)
        {
            var result = new FaceUnrollResult();
            EdgeArrayArray edgeLoops = face.EdgeLoops;
            if (edgeLoops.Size == 0) return result;
            EdgeArray largestLoop = null;
            int maxEdges = 0;
            foreach (EdgeArray edgeLoop in edgeLoops)
            {
                if (edgeLoop.Size > maxEdges) { maxEdges = edgeLoop.Size; largestLoop = edgeLoop; }
            }
            if (largestLoop == null) return result;

            // Complex profile (edit profile, cutting blocks): outer has more than 4 edges → tessellate outer
            if (largestLoop.Size > 4)
            {
                Log.Debug($"Complex cylindrical profile: {largestLoop.Size} edges, using tessellation for outer boundary");
                var outer = UnrollCylindricalEdgeLoopTo2D(largestLoop, axis, origin, radius, insertionPoint);
                if (outer == null || outer.Count < 3)
                    throw new InvalidOperationException("Could not form a valid closed boundary for the complex profile. The edge loop may have gaps or non-manifold geometry.");
                result.OuterCurves = outer;
            }
            else
            {
                result.OuterCurves = UnrollCylindricalFaceExact(face, axis, origin, radius, insertionPoint);
            }

            foreach (EdgeArray loop in edgeLoops)
            {
                if (loop == largestLoop) continue;
                var inner = UnrollCylindricalEdgeLoopTo2D(loop, axis, origin, radius, insertionPoint);
                if (inner != null && inner.Count >= 3) result.InnerLoops.Add(inner);
            }
            if (result.InnerLoops.Count > 0)
                Log.Debug($"Unroll: {result.InnerLoops.Count} inner loop(s) (openings)");
            return result;
        }

        /// <summary>Project 3D point on cylinder to 2D unrolled coords. Y flipped so orientation matches expected (no 180° rotation).</summary>
        private XYZ ProjectToCylinder2D(XYZ p, XYZ origin, XYZ axis, double radius, XYZ insertionPoint)
        {
            XYZ v = p - origin;
            double height = v.DotProduct(axis);
            XYZ radial = v - (height * axis);
            double angle = CalculateAngleFromAxis(radial, axis, XYZ.BasisX);
            return new XYZ(radius * angle + insertionPoint.X, insertionPoint.Y - height, 0);
        }

        private List<Curve> UnrollCylindricalEdgeLoopTo2D(EdgeArray edgeArray, XYZ axis, XYZ origin, double radius, XYZ insertionPoint)
        {
            List<Edge> sorted = SortEdgesIntoLoop(edgeArray);
            if (sorted.Count != edgeArray.Size)
            {
                Log.Warning($"Edge loop sort incomplete: {sorted.Count}/{edgeArray.Size} edges in order; skipping this boundary");
                return null;
            }
            List<XYZ> points2D = new List<XYZ>();
            XYZ lastPoint = null;
            const double tol = 0.1;
            for (int ei = 0; ei < sorted.Count; ei++)
            {
                Curve curve3D = sorted[ei].AsCurve();
                IList<XYZ> tess = curve3D.Tessellate();
                if (tess == null || tess.Count == 0) continue;
                bool reverse = (lastPoint != null && tess.Count > 0 && lastPoint.DistanceTo(tess[tess.Count - 1]) <= tol);
                int start = (reverse ? tess.Count - 1 : (ei == 0 ? 0 : 1));
                int end = (reverse ? 0 : tess.Count);
                int step = (reverse ? -1 : 1);
                for (int i = start; (reverse && i >= end) || (!reverse && i < end); i += step)
                {
                    XYZ pt = ProjectToCylinder2D(tess[i], origin, axis, radius, insertionPoint);
                    points2D.Add(pt);
                }
                lastPoint = tess[reverse ? 0 : tess.Count - 1];
            }
            // Không mirror: điểm 2 (gốc) ở trái, điểm 1 (cuối) ở phải — khớp như ảnh 2
            return CreateCurvesFromPoints(points2D);
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
        /// Unrolls a ruled surface (and spline/ellipse-style boundaries). Edges are sorted into a continuous loop so the 2D boundary is valid for FilledRegion.
        /// </summary>
        private List<Curve> UnrollRuledSurface(Face face, XYZ insertionPoint)
        {
            List<Curve> curves2D = new List<Curve>();
            EdgeArrayArray edgeLoops = face.EdgeLoops;
            if (edgeLoops.Size == 0)
            {
                Log.Warning("No edge loops found on ruled surface");
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
            if (largestLoop == null)
            {
                Log.Warning("No valid edge loop found on ruled surface");
                return curves2D;
            }

            List<Edge> sorted = SortEdgesIntoLoop(largestLoop);
            if (sorted.Count != largestLoop.Size)
                Log.Warning($"Ruled surface: edge sort incomplete ({sorted.Count}/{largestLoop.Size}); boundary may be invalid");
            const double tol = 0.1;
            List<XYZ> points2D = new List<XYZ>();
            double lengthAtStartOfEdge = 0;
            XYZ previousPoint3D = null;
            for (int ei = 0; ei < sorted.Count; ei++)
            {
                Curve curve3D = sorted[ei].AsCurve();
                double edgeLength = curve3D.Length;
                IList<XYZ> tess = curve3D.Tessellate();
                if (tess == null || tess.Count == 0) continue;
                bool reverse = (previousPoint3D != null && tess.Count > 0 && previousPoint3D.DistanceTo(tess[tess.Count - 1]) <= tol);
                int start = reverse ? tess.Count - 1 : (ei == 0 ? 0 : 1);
                int end = reverse ? 0 : tess.Count;
                int step = reverse ? -1 : 1;
                // Thu thập điểm trên edge theo thứ tự (Q₀..Qₘ)
                var Q = new List<XYZ>();
                for (int i = start; (reverse && i >= end) || (!reverse && i < end); i += step)
                    Q.Add(tess[i]);
                if (Q.Count == 0) continue;
                // Chord cumulative trong edge: c₀=0, cᵢ=Σ|QⱼQⱼ₊₁|, chuẩn hóa tᵢ=cᵢ/cₘ → s(Pᵢ)=lengthBeforeEdge + tᵢ×edge.Length
                var chordCumulative = new List<double> { 0 };
                for (int j = 0; j < Q.Count - 1; j++)
                    chordCumulative.Add(chordCumulative[chordCumulative.Count - 1] + Q[j].DistanceTo(Q[j + 1]));
                double cTotal = chordCumulative[chordCumulative.Count - 1];
                for (int j = 0; j < Q.Count; j++)
                {
                    double ti = (cTotal > 0) ? (chordCumulative[j] / cTotal) : 0;
                    double developedLength = lengthAtStartOfEdge + ti * edgeLength;
                    XYZ p3 = Q[j];
                    points2D.Add(new XYZ(developedLength + insertionPoint.X, insertionPoint.Y - p3.Z, 0));
                }
                lengthAtStartOfEdge += edgeLength;
                previousPoint3D = tess[reverse ? 0 : tess.Count - 1];
            }
            // Không mirror: điểm 2 (gốc) ở trái (x=0), điểm 1 (cuối) ở phải (x=totalLength) để khớp như ảnh 2
            curves2D = CreateCurvesFromPoints(points2D);
            Log.Debug($"Created {curves2D.Count} curves for ruled surface (developed length = {lengthAtStartOfEdge:F1})");
            return curves2D;
        }

        /// <summary>
        /// Unroll face by tessellating the outer boundary: X = developed length along path, Y = -Z for orientation.
        /// Used for spline/ellipse/revolved/Hermite faces that are not cylindrical/conical/planar/ruled.
        /// </summary>
        private List<Curve> UnrollByBoundaryTessellation(Face face, XYZ insertionPoint)
        {
            List<Curve> curves2D = new List<Curve>();
            EdgeArrayArray edgeLoops = face.EdgeLoops;
            if (edgeLoops == null || edgeLoops.Size == 0)
            {
                Log.Warning("No edge loops for boundary tessellation");
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
            List<Edge> sorted = SortEdgesIntoLoop(largestLoop);
            if (sorted.Count != largestLoop.Size)
                Log.Warning($"Boundary tessellation: edge sort incomplete ({sorted.Count}/{largestLoop.Size})");
            const double tol = 0.1;
            List<XYZ> points2D = new List<XYZ>();
            double lengthAtStartOfEdge = 0;
            XYZ previousPoint3D = null;
            for (int ei = 0; ei < sorted.Count; ei++)
            {
                Curve curve3D = sorted[ei].AsCurve();
                double edgeLength = curve3D.Length;
                IList<XYZ> tess = curve3D.Tessellate();
                if (tess == null || tess.Count == 0) continue;
                bool reverse = (previousPoint3D != null && tess.Count > 0 && previousPoint3D.DistanceTo(tess[tess.Count - 1]) <= tol);
                int start = reverse ? tess.Count - 1 : (ei == 0 ? 0 : 1);
                int end = reverse ? 0 : tess.Count;
                int step = reverse ? -1 : 1;
                var Q = new List<XYZ>();
                for (int i = start; (reverse && i >= end) || (!reverse && i < end); i += step)
                    Q.Add(tess[i]);
                if (Q.Count == 0) continue;
                var chordCumulative = new List<double> { 0 };
                for (int j = 0; j < Q.Count - 1; j++)
                    chordCumulative.Add(chordCumulative[chordCumulative.Count - 1] + Q[j].DistanceTo(Q[j + 1]));
                double cTotal = chordCumulative[chordCumulative.Count - 1];
                for (int j = 0; j < Q.Count; j++)
                {
                    double ti = (cTotal > 0) ? (chordCumulative[j] / cTotal) : 0;
                    double developedLength = lengthAtStartOfEdge + ti * edgeLength;
                    XYZ p3 = Q[j];
                    points2D.Add(new XYZ(developedLength + insertionPoint.X, insertionPoint.Y - p3.Z, 0));
                }
                lengthAtStartOfEdge += edgeLength;
                previousPoint3D = tess[reverse ? 0 : tess.Count - 1];
            }
            curves2D = CreateCurvesFromPoints(points2D);
            Log.Debug($"Created {curves2D.Count} curves from boundary tessellation (developed length = {lengthAtStartOfEdge:F1})");
            return curves2D;
        }

        /// <summary>
        /// Sorts edges from an EdgeArray into a continuous loop using graph traversal.
        /// Merges endpoints within tolerance so complex/trimmed profiles form a single cycle.
        /// </summary>
        private List<Edge> SortEdgesIntoLoop(EdgeArray edgeArray)
        {
            List<Edge> edges = new List<Edge>();
            foreach (Edge edge in edgeArray)
                edges.Add(edge);
            if (edges.Count == 0) return edges;

            // Try increasing vertex merge tolerances (Revit units, often feet)
            double[] vertexTolerances = { 1e-6, 0.001, 0.01, 0.1, 0.5 };
            foreach (double vtol in vertexTolerances)
            {
                List<Edge> result = SortEdgesIntoLoopWithTolerance(edges, vtol);
                if (result != null && result.Count == edges.Count)
                {
                    if (vtol > 0.01) Log.Debug($"SortEdgesIntoLoop used vertex tolerance {vtol}");
                    return result;
                }
            }

            // Fallback: greedy chain without appending disconnected edges (avoids invalid self-intersecting loop)
            List<Edge> sorted = new List<Edge>();
            List<Edge> remaining = new List<Edge>(edges);
            sorted.Add(remaining[0]);
            remaining.RemoveAt(0);
            double[] tolList = { 0.001, 0.01, 0.1, 0.5, 1.0, 2.0 };
            while (remaining.Count > 0)
            {
                Curve lastC = sorted[sorted.Count - 1].AsCurve();
                XYZ lastEnd = lastC.GetEndPoint(1);
                bool found = false;
                foreach (double tol in tolList)
                {
                    for (int i = 0; i < remaining.Count; i++)
                    {
                        Curve c = remaining[i].AsCurve();
                        if (lastEnd.DistanceTo(c.GetEndPoint(0)) <= tol || lastEnd.DistanceTo(c.GetEndPoint(1)) <= tol)
                        {
                            sorted.Add(remaining[i]);
                            remaining.RemoveAt(i);
                            found = true;
                            break;
                        }
                    }
                    if (found) break;
                }
                if (!found)
                {
                    Log.Warning($"Could not find connecting edge, {remaining.Count} edges remaining; returning partial loop (no FilledRegion for this boundary)");
                    return sorted; // do not append remaining — partial loop is still invalid but avoids wrong geometry
                }
            }
            return sorted;
        }

        /// <summary>
        /// Builds a single closed loop from edges by merging vertices within vtol and traversing the cycle.
        /// Returns null if the graph is not a single cycle (e.g. degree != 2 or multiple components).
        /// </summary>
        private List<Edge> SortEdgesIntoLoopWithTolerance(List<Edge> edges, double vtol)
        {
            int n = edges.Count;
            List<XYZ> vertices = new List<XYZ>();

            int VertexIndex(XYZ p)
            {
                for (int i = 0; i < vertices.Count; i++)
                    if (p.DistanceTo(vertices[i]) <= vtol) return i;
                vertices.Add(p);
                return vertices.Count - 1;
            }

            var validIndices = new List<int>();
            var edgeVertices = new List<(int v0, int v1)>();
            for (int i = 0; i < n; i++)
            {
                Curve c = edges[i].AsCurve();
                int v0 = VertexIndex(c.GetEndPoint(0));
                int v1 = VertexIndex(c.GetEndPoint(1));
                if (v0 == v1) continue;
                validIndices.Add(i);
                edgeVertices.Add((v0, v1));
            }
            int m = validIndices.Count;
            if (m != n) return null;

            // Adjacency: for each vertex, list of (local edge index 0..m-1, otherVertex)
            var adj = new Dictionary<int, List<(int edgeIdx, int other)>>();
            for (int i = 0; i < m; i++)
            {
                int v0 = edgeVertices[i].v0, v1 = edgeVertices[i].v1;
                if (!adj.ContainsKey(v0)) adj[v0] = new List<(int, int)>();
                if (!adj.ContainsKey(v1)) adj[v1] = new List<(int, int)>();
                adj[v0].Add((i, v1));
                adj[v1].Add((i, v0));
            }

            foreach (var kv in adj)
                if (kv.Value.Count != 2) return null;

            var sorted = new List<Edge>();
            int currentVertex = edgeVertices[0].v1;
            var used = new HashSet<int> { 0 };
            sorted.Add(edges[validIndices[0]]);

            while (sorted.Count < m)
            {
                int nextEdge = -1;
                int nextVertex = -1;
                foreach (var (edgeIdx, other) in adj[currentVertex])
                {
                    if (used.Contains(edgeIdx)) continue;
                    nextEdge = edgeIdx;
                    nextVertex = other;
                    break;
                }
                if (nextEdge < 0) return null;
                used.Add(nextEdge);
                sorted.Add(edges[validIndices[nextEdge]]);
                currentVertex = nextVertex;
            }

            if (currentVertex != edgeVertices[0].v0) return null;
            return sorted;
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
                        
                        // Check if curve connects to last point (tolerance 0.1 ft)
                        double tol = 0.1;
                        if (lastPoint.DistanceTo(startPoint) <= tol)
                        {
                            sortedCurves.Add(candidate);
                            remainingCurves.RemoveAt(i);
                            found = true;
                            break;
                        }
                        else if (lastPoint.DistanceTo(endPoint) <= tol)
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
                
                double gap = firstPoint.DistanceTo(finalPoint);
                if (gap > 0.1)
                {
                    Log.Warning($"Loop not perfectly closed, gap: {gap}");
                }
                if (gap > 0.001 && gap < 0.1)
                {
                    sortedCurves.Add(Line.CreateBound(finalPoint, firstPoint));
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
        /// Bước 1: Luôn vẽ DetailCurves từ kết quả unroll (outer + inner) để user thấy hình dù FilledRegion có lỗi hay không.
        /// </summary>
        public void DrawDetailCurvesFromResult(Document doc, ViewDrafting draftingView, FaceUnrollResult result)
        {
            if (result?.OuterCurves == null || result.OuterCurves.Count < 3) return;
            List<Curve> flatOuter = FlattenToZ0IfNeeded(result.OuterCurves);
            int drawn = 0;
            foreach (Curve c in flatOuter)
            {
                if (c == null || c.Length < 0.001) continue;
                try
                {
                    doc.Create.NewDetailCurve(draftingView, c);
                    drawn++;
                }
                catch (Exception e) { Log.Warning(e, "DetailCurve outer"); }
            }
            if (result.InnerLoops != null)
            {
                foreach (List<Curve> innerCurves in result.InnerLoops)
                {
                    if (innerCurves == null || innerCurves.Count < 3) continue;
                    foreach (Curve c in FlattenToZ0IfNeeded(innerCurves))
                    {
                        if (c == null || c.Length < 0.001) continue;
                        try { doc.Create.NewDetailCurve(draftingView, c); drawn++; } catch (Exception e) { Log.Warning(e, "DetailCurve inner"); }
                    }
                }
            }
            Log.Information($"Drew {drawn} detail curves (outer + inner)");
        }

        /// <summary>
        /// Creates one FilledRegion (outer + inner voids). Priority: 1) full region with voids; only if that fails, fallback to outer-only + detail lines.
        /// Never keeps detail lines when a complete FilledRegion (with voids) can be created.
        /// </summary>
        public FilledRegion CreateFilledRegion(Document doc, ViewDrafting draftingView, FaceUnrollResult result, ElementId fillRegionTypeId = null)
        {
            if (result?.OuterCurves == null || result.OuterCurves.Count < 3)
                throw new ArgumentException("FaceUnrollResult must have at least 3 outer curves.");
            fillRegionTypeId = fillRegionTypeId ?? GetFirstFilledRegionType(doc);
            if (fillRegionTypeId == null)
                throw new InvalidOperationException("No filled region types found in the document");

            List<Curve> flatOuter = FlattenToZ0IfNeeded(result.OuterCurves);
            CurveLoop outerLoop = CreateCurveLoop(flatOuter);
            if (!outerLoop.IsCounterclockwise(XYZ.BasisZ)) outerLoop.Flip();

            // Ưu tiên cao nhất: 1 FilledRegion hoàn chỉnh (outer + void)
            List<CurveLoop> curveLoops = new List<CurveLoop> { outerLoop };
            if (result.InnerLoops != null)
            {
                foreach (List<Curve> innerCurves in result.InnerLoops)
                {
                    if (innerCurves == null || innerCurves.Count < 3) continue;
                    try
                    {
                        CurveLoop innerLoop = CreateCurveLoop(FlattenToZ0IfNeeded(innerCurves));
                        if (innerLoop.IsOpen()) { Log.Warning("Skipping open inner loop (opening)"); continue; }
                        if (innerLoop.IsCounterclockwise(XYZ.BasisZ)) innerLoop.Flip();
                        curveLoops.Add(innerLoop);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Skipping invalid inner loop");
                    }
                }
            }

            try
            {
                return FilledRegion.Create(doc, fillRegionTypeId, draftingView.Id, curveLoops);
            }
            catch (Exception ex)
            {
                // Chỉ outer (ruled/spline): nếu FilledRegion thất bại, vẽ boundary bằng DetailCurve để user thấy hình
                if (curveLoops.Count == 1)
                {
                    Log.Warning(ex, "FilledRegion with outer boundary failed; drawing boundary as detail lines");
                    foreach (Curve c in flatOuter)
                    {
                        if (c == null || c.Length < 0.001) continue;
                        try { doc.Create.NewDetailCurve(draftingView, c); } catch { }
                    }
                    throw;
                }
                // Có inner loops → thử qua DetailCurve để lấy 1 region có void

                Log.Warning("FilledRegion with inner loops failed; retrying via detail-curve geometry to get one complete region with voids");
                FilledRegion regionOuterOnly = FilledRegion.Create(doc, fillRegionTypeId, draftingView.Id, new List<CurveLoop> { outerLoop });
                var detailCurvesByLoop = new List<List<CurveElement>>();
                if (result.InnerLoops != null)
                {
                    foreach (List<Curve> innerCurves in result.InnerLoops)
                    {
                        if (innerCurves == null || innerCurves.Count < 3) continue;
                        List<Curve> flatInner = FlattenToZ0IfNeeded(innerCurves);
                        var oneLoop = new List<CurveElement>();
                        foreach (Curve c in flatInner)
                        {
                            if (c == null || c.Length < 0.001) continue;
                            try
                            {
                                var dc = doc.Create.NewDetailCurve(draftingView, c) as CurveElement;
                                if (dc != null) oneLoop.Add(dc);
                            }
                            catch (Exception e) { Log.Warning(e, "DetailCurve for inner void"); }
                        }
                        if (oneLoop.Count >= 3) detailCurvesByLoop.Add(oneLoop);
                    }
                }

                var innerLoopsFromDetail = new List<CurveLoop>();
                var savedCurvesPerLoop = new List<List<Curve>>();
                foreach (var dcList in detailCurvesByLoop)
                {
                    var curvesFromDetail = new List<Curve>();
                    foreach (CurveElement ce in dcList)
                    {
                        Curve geom = ce?.GeometryCurve;
                        if (geom != null && geom.Length >= 0.001)
                            curvesFromDetail.Add(Line.CreateBound(geom.GetEndPoint(0), geom.GetEndPoint(1)));
                    }
                    savedCurvesPerLoop.Add(curvesFromDetail);
                    if (curvesFromDetail.Count < 3) continue;
                    try
                    {
                        CurveLoop loop = CreateCurveLoop(curvesFromDetail);
                        if (loop != null && !loop.IsOpen())
                        {
                            if (loop.IsCounterclockwise(XYZ.BasisZ)) loop.Flip();
                            innerLoopsFromDetail.Add(loop);
                        }
                    }
                    catch (Exception e) { Log.Warning(e, "CurveLoop from detail curves"); }
                }

                if (innerLoopsFromDetail.Count > 0)
                {
                    doc.Delete(regionOuterOnly.Id);
                    foreach (var dcList in detailCurvesByLoop)
                        foreach (CurveElement ce in dcList) doc.Delete(ce.Id);
                    var withVoids = new List<CurveLoop> { outerLoop };
                    withVoids.AddRange(innerLoopsFromDetail);
                    try
                    {
                        // Thành công: 1 FilledRegion có void, không giữ detail line
                        return FilledRegion.Create(doc, fillRegionTypeId, draftingView.Id, withVoids);
                    }
                    catch (Exception e)
                    {
                        Log.Warning(e, "FilledRegion with voids from detail curves failed; fallback: outer only + detail lines");
                        regionOuterOnly = FilledRegion.Create(doc, fillRegionTypeId, draftingView.Id, new List<CurveLoop> { outerLoop });
                        foreach (var curves in savedCurvesPerLoop)
                            foreach (Curve c in curves)
                            { try { if (c != null && c.Length >= 0.001) doc.Create.NewDetailCurve(draftingView, c); } catch { } }
                        return regionOuterOnly;
                    }
                }
                return regionOuterOnly;
            }
        }

        private List<Curve> FlattenToZ0IfNeeded(List<Curve> curves)
        {
            if (curves == null || curves.Count == 0) return curves;
            double minZ = double.MaxValue, maxZ = double.MinValue;
            foreach (Curve c in curves)
            {
                minZ = Math.Min(minZ, Math.Min(c.GetEndPoint(0).Z, c.GetEndPoint(1).Z));
                maxZ = Math.Max(maxZ, Math.Max(c.GetEndPoint(0).Z, c.GetEndPoint(1).Z));
            }
            if (Math.Abs(maxZ - minZ) <= 0.001 && Math.Abs(minZ) <= 0.001) return curves;
            var flat = new List<Curve>();
            foreach (Curve c in curves)
            {
                XYZ p1 = c.GetEndPoint(0), p2 = c.GetEndPoint(1);
                flat.Add(Line.CreateBound(new XYZ(p1.X, p1.Y, 0), new XYZ(p2.X, p2.Y, 0)));
            }
            return flat;
        }

        /// <summary>
        /// Creates a filled region in a drafting view from curves (outer only).
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
