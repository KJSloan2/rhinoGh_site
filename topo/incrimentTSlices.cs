// Grasshopper Script Instance
#region Usings
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;

using System.Linq;

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

private void RunScript(double incriment, List<double> elvRel, ref object range, ref object steps)
    {
        const double FT_PER_M = 3.280839895;

        if (incriment == null)
        {
            // if no incriment parameter givem set to 10 by default
            incriment = 10;
            return;
        }

        if (elvRel == null || elvRel.Count == 0)
        {
            range = null;
            return;
        }

        double minElvRel = elvRel.Min();
        double maxElvRel = elvRel.Max();

        var rangeOut = new List<double>
        {
            minElvRel,
            maxElvRel
        };

        var stepVals = new List<double>();

        for (double v = minElvRel; v <= maxElvRel - incriment; v += incriment)
            stepVals.Add(v*FT_PER_M);

        steps = stepVals;
        range = rangeOut;

    }
}
