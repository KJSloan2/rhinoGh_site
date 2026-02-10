// Grasshopper Script Instance
#region Usings
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
#endregion

public class Script_Instance : GH_ScriptInstance
{
  private void RunScript(
		Mesh M,
		DataTree<Brep> S,
		ref object TerrainFaces,
		ref object Intersections)
    {
        if (M == null || !M.IsValid || S == null)
        {
            TerrainFaces = null;
            Intersections = null;
            return;
        }

        double tol = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

        // --------------------------------------
        // Step 1 — Mesh → face Breps
        // --------------------------------------
        var faceBreps = new List<Brep>();

        M.Faces.ConvertQuadsToTriangles();

        for (int i = 0; i < M.Faces.Count; i++)
        {
            var f = M.Faces[i];

            var pts = new List<Point3d>
            {
                M.Vertices[f.A],
                M.Vertices[f.B],
                M.Vertices[f.C],
                M.Vertices[f.A]
            };

            var pl = new Polyline(pts);
            if (!pl.IsValid || !pl.IsClosed) continue;

            var breps = Brep.CreatePlanarBreps(pl.ToNurbsCurve(), tol);
            if (breps != null && breps.Length > 0)
                faceBreps.Add(breps[0]);
        }

        TerrainFaces = faceBreps;

        // --------------------------------------
        // Step 2 — Intersect slicer Breps
        // --------------------------------------
        var outTree = new DataTree<Curve>();

        for (int b = 0; b < S.BranchCount; b++)
        {
            var path = S.Path(b);
            var slicerBranch = S.Branch(b);

            foreach (var slicer in slicerBranch)
            {
                if (slicer == null || !slicer.IsValid)
                    continue;

                foreach (var face in faceBreps)
                {
                    Curve[] curves;
                    Point3d[] pts;

                    bool hit = Rhino.Geometry.Intersect.Intersection.BrepBrep(
                        slicer,
                        face,
                        tol,
                        out curves,
                        out pts
                    );

                    if (!hit || curves == null) continue;

                    foreach (var crv in curves)
                        if (crv != null && crv.IsValid)
                            outTree.Add(crv, path);
                }
            }
        }

        Intersections = outTree;
    }
}