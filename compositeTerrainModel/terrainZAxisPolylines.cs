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
    #region Notes
    /* 
      Members:
        RhinoDoc RhinoDocument
        GH_Document GrasshopperDocument
        IGH_Component Component
        int Iteration

      Methods (Virtual & overridable):
        Print(string text)
        Print(string format, params object[] args)
        Reflect(object obj)
        Reflect(object obj, string method_name)
    */
    #endregion

    private void RunScript(
		List<int> x,
		List<int> y,
		List<Point3d> z,
		List<double> u,
		ref object a,
		ref object b,
		ref object c,
		ref object d,
		ref object e)
    {
        if (x == null || y == null || z == null || u == null) return;
        if (x.Count != y.Count || x.Count != z.Count || x.Count != u.Count) return;

        var records = x
            .Select((row, i) => new {
                row  = row,
                col  = y[i],
                pt   = z[i],
                prop = u[i]
            })
            .ToList();

        var uniqueRows = records
            .Select(r => r.row)
            .Distinct()
            .OrderBy(v => v)
            .ToList();

        var uniqueCols = records
            .Select(r => r.col)
            .Distinct()
            .OrderBy(v => v)
            .ToList();

        var polyTreeY = new DataTree<Polyline>();
        var polyTreeX = new DataTree<Polyline>();
        var propTree = new DataTree<double>();

        for (int r = 0; r < uniqueRows.Count; r++)
        {
            int rowId = uniqueRows[r];
            var path = new GH_Path(r);

            var rowRecs = records
                .Where(rec => rec.row == rowId)
                .OrderBy(rec => rec.col)
                .ToList();

            if (rowRecs.Count < 2)
                continue;

            polyTreeX.Add(
                new Polyline(rowRecs.Select(rr => rr.pt)),
                path);

            foreach (var val in rowRecs.Select(rr => rr.prop))
                propTree.Add(val, path);
        }

        for (int cIdx = 0; cIdx < uniqueCols.Count; cIdx++)
        {
            int colId = uniqueCols[cIdx];
            var path = new GH_Path(cIdx);

            var colRecs = records
                .Where(rec => rec.col == colId)
                .OrderBy(rec => rec.row)
                .ToList();

            if (colRecs.Count < 2)
                continue;

            polyTreeY.Add(
                new Polyline(colRecs.Select(cr => cr.pt)),
                path);
        }

        a = uniqueRows;
        b = records.Count;
        c = polyTreeX;
        d = polyTreeY;
        e = propTree;
    }

}
