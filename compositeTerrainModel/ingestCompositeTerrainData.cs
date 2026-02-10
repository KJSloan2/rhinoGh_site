#region Usings
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel;
using System.Collections;
#endregion

public class Script_Instance : GH_ScriptInstance
{
  private static readonly CultureInfo INV = CultureInfo.InvariantCulture;

  // WGS-84 meters per degree (high-accuracy), then convert to feet
  private static void FeetPerDegree(double latDeg, out double feetPerDegLon, out double feetPerDegLat)
  {
    double phi = Math.PI * latDeg / 180.0;
    double m_per_deg_lat = 111132.92 - 559.82 * Math.Cos(2 * phi) + 1.175 * Math.Cos(4 * phi);
    double m_per_deg_lon = 111412.84 * Math.Cos(phi) - 93.5 * Math.Cos(3 * phi) + 0.118 * Math.Cos(5 * phi);
    const double FT_PER_M = 3.280839895;
    feetPerDegLat = m_per_deg_lat * FT_PER_M;
    feetPerDegLon = m_per_deg_lon * FT_PER_M;
  }

  private void RunScript(
		object x,
		object y,
		List<object> zz,
		List<object> za,
		ref object a,
		ref object b,
		ref object c,
		ref object d,
		ref object e,
		ref object f,
		ref object g,
		ref object h,
		ref object i,
		ref object j,
		ref object k,
		ref object l,
		ref object m,
		ref object n,
		ref object o)
  {
    string fileName = x as string;
    var headerIndex = new Dictionary<string, int>();

    double scaleX = 1.0, scaleY = 1.0;

    if (zz is IList list && list.Count >= 2)                    // covers double[], float[], List<double>, List<float>, object[]
    {
        scaleX = Convert.ToDouble(list[0], INV);
        scaleY = Convert.ToDouble(list[1], INV);
    };

    double adjX = 1.0, adjY = 1.0;
    if (za is IList zaList && zaList.Count >= 2)                    // covers double[], float[], List<double>, List<float>, object[]
    {
        adjX = Convert.ToDouble(zaList[0], INV);
        adjY = Convert.ToDouble(zaList[1], INV);
    };

    bool run = (bool) y;

    var points = new List<Point3d>();
    var pointsTransformed = new List<Point3d>();
    var ddPoints = new List<Point3d>();
    var lstf_serc = new List<double>();
    var lstf_flag = new List<string>();
    var elv_rel = new List<double>();
    var lstf_mean = new List<double>();
    var ndvi_mean = new List<double>();
    var ndvi_flag = new List<string>();
    var ndmi_mean = new List<double>();
    var idx_row = new List<int>();
    var idx_col = new List<int>();
    var dd_geoid = new List<int>();
    var mc_geoid = new List<int>();

    var poolCoordsX = new List<double>();
    var poolCoordsY = new List<double>();

    var dom_dir_elv = new List<double>();
    var dom_dir = new List<string>();

    var bbRect = new Rectangle3d();

    if (!run || string.IsNullOrEmpty(fileName))
    { a=points; b=lstf_serc; c=lstf_flag; 
    d=elv_rel; e=lstf_mean; f=ndvi_mean; 
    g=ndvi_flag; h=ndmi_mean; i=idx_row; 
    j=idx_col; k=dom_dir; l=dom_dir_elv;
    m=dd_geoid; n=mc_geoid; o=bbRect; return; }
    
    string dirPath = @"C:\Users\Kjslo\Documents\local_dev\dll_p\output\aoi_filtered";

    string fullPath = Path.Combine(dirPath, fileName);

    if (!File.Exists(fullPath))
    {
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "File not found: " + fullPath);
      a=points; b=lstf_serc; c=lstf_flag; 
      d=elv_rel; e=lstf_mean; f=ndvi_mean; 
      g=ndvi_flag; h=ndmi_mean; i=idx_row; 
      j=idx_col; k=dom_dir; l=dom_dir_elv; 
      m=dd_geoid; n=mc_geoid; o=bbRect; return;
    }

    // Toggle if your altitude column is meters
    const bool ALT_IS_METERS = true;
    const double FT_PER_M = 3.280839895;

    try
    {
      // Pass 1: read raw rows and store lon/lat/altFeet
      var rawPoints = new List<Point3d>();
      double latSum = 0.0;
      int latCount  = 0;

      using (var reader = new StreamReader(fullPath))
      {
        string headerLine = reader.ReadLine();
        //var headerLine = reader.ReadLine();
        if (headerLine == null)
            throw new Exception("CSV has no header row");
        
        var headers = headerLine.Split(',');

        for (int itterI = 0; itterI < headers.Length; itterI++)
        {
            string hi = headers[itterI].Trim();
            if (!headerIndex.ContainsKey(hi))
                headerIndex.Add(hi, itterI);
        }
        ////////////////////////////////////////
        int geoidHIdx = headerIndex["geoid"];
        int latHIdx = headerIndex["lat"];
        int lonHIdx = headerIndex["lon"];
        int lstfHIdx = headerIndex["lstf"];
        int lstfSercHIdx = headerIndex["lstf_serc"];
        int ndviHIdx = headerIndex["ndvi"];
        int ndmiHIdx = headerIndex["ndmi"];
        int elvRelHIdx = headerIndex["elv_rel"];
        int rowIdxHIdx = headerIndex["idx_row"];
        int colIdxHIdx = headerIndex["idx_col"];
        int domDirHIdx = headerIndex["dom_dir"];
        int ddGeoidHIdx = headerIndex["dd_geoid"];
        int ddElvHIdx = headerIndex["dom_dir_elv"];
        int ddPtbLatHIdx = headerIndex["dom_ptb_lat"];
        int ddPtbLonHIdx = headerIndex["dom_ptb_lon"];
        ////////////////////////////////////////
        string line;
        while ((line = reader.ReadLine()) != null)
        {
          if (string.IsNullOrWhiteSpace(line)) continue;
          var tokens = line.Split(',');
          if (tokens.Length < 18) continue;

          if (!double.TryParse(tokens[lstfHIdx], NumberStyles.Float, INV, out double lstfVal))  continue;
          if (!double.TryParse(tokens[lstfSercHIdx], NumberStyles.Float, INV, out double lstfSerc)) continue;
          //string lstfFlag = tokens[6];

          if (!double.TryParse(tokens[ndviHIdx], NumberStyles.Float, INV, out double ndviVal)) continue;
          //string ndviFlag = tokens[10];

          if (!double.TryParse(tokens[ndmiHIdx], NumberStyles.Float, INV, out double ndmiVal)) continue;

          if (!double.TryParse(tokens[latHIdx], NumberStyles.Float, INV, out double lat)) continue;
          if (!double.TryParse(tokens[lonHIdx], NumberStyles.Float, INV, out double lon)) continue;

          if (!double.TryParse(tokens[ddPtbLatHIdx], NumberStyles.Float, INV, out double ddPtbLat)) continue;
          if (!double.TryParse(tokens[ddPtbLonHIdx], NumberStyles.Float, INV, out double ddPtbLon)) continue;

          // elv_rel column (tokens[12]); keep in feet (convert if meters)
          if (!double.TryParse(tokens[elvRelHIdx], NumberStyles.Float,   INV, out double elvRel)) continue;
          
          double altFeet = ALT_IS_METERS ? elvRel * FT_PER_M : elvRel;
          //double altFeet = elvRel;

          // Build raw point (X=lon, Y=lat, Z=feet)
          //rawPoints.Add(new Point3d(lon+adjX, lat-adjY, altFeet));
          rawPoints.Add(new Point3d(lon, lat, altFeet));
          latSum += lat; latCount++;

          // Accumulate attributes (preserve order)
          lstf_serc.Add((float)lstfSerc);
          //lstf_flag.Add(lstfFlag);
          //ndvi_flag.Add(ndviFlag);
          elv_rel.Add((float)elvRel);
          lstf_mean.Add((float)lstfVal);
          ndvi_mean.Add((float)ndviVal);
          ndmi_mean.Add((float)ndmiVal);

          if (!int.TryParse(tokens[rowIdxHIdx], NumberStyles.Integer, INV, out int rIndex)) rIndex = 0;
          if (!int.TryParse(tokens[colIdxHIdx], NumberStyles.Integer, INV, out int cIndex)) cIndex = 0;
          idx_row.Add(rIndex);
          idx_col.Add(cIndex);
    
          if (!int.TryParse(tokens[ddGeoidHIdx], NumberStyles.Integer, INV, out int ddGeoid)) ddGeoid = 0;
          dd_geoid.Add(ddGeoid);

          if (!int.TryParse(tokens[geoidHIdx], NumberStyles.Integer, INV, out int geoid)) geoid = 0;
          mc_geoid.Add(geoid);
          //////////////////////////////////////////////////////////////////////////////////////
          // Debug logging
          /*if (points.Count < 5)
          {
            Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, 
              $"Row {points.Count}: tokens.Length={tokens.Length}, tokens[39]='{(tokens.Length > 39 ? tokens[39] : "N/A")}', tokens[50]='{(tokens.Length > 50 ? tokens[50] : "N/A")}'");
          }*/
          //////////////////////////////////////////////////////////////////////////////////////
          //string domDir = tokens.Length > 38 ? tokens[38] : "";
          string domDir = tokens[domDirHIdx];
          dom_dir.Add(domDir);

          float domDirElvVal = 0.0f;
          if (tokens.Length > 51)
          {
            float.TryParse(tokens[ddElvHIdx], NumberStyles.Float, INV, out domDirElvVal);
          }
          dom_dir_elv.Add(domDirElvVal);
        }
      }
      // Pass 2: non-uniform scaling (degrees -> feet), no translation (matches Python)
      if (rawPoints.Count > 0)
      {
        double refLat = (latCount > 0) ? (latSum / latCount) : rawPoints[0].Y;

        // feet per degree at reference latitude
        FeetPerDegree(refLat, out double feetPerDegLon, out double feetPerDegLat);

        // Non-uniform scale about WorldXY (Z left as-is)
        //Transform scaleXY = Transform.Scale(Plane.WorldXY, feetPerDegLon, feetPerDegLat, 1.0);
        Transform scaleXY = Transform.Scale(Plane.WorldXY, scaleX, scaleY, 1.0);
        Transform moveXY  = Transform.Translation(-adjX, -adjY, 0);
         
        foreach (var pt in rawPoints)
        {
            var p = pt;
            p.Transform(scaleXY);
            p.Transform(moveXY);

            pointsTransformed.Add(p);

            poolCoordsX.Add(p.X);
            poolCoordsY.Add(p.Y);
        }
      }

      double minCoordsX = poolCoordsX.Min();
      double minCoordsY = poolCoordsY.Min();

      double maxCoordsX = poolCoordsX.Max();
      double maxCoordsY = poolCoordsY.Max();

      var bbPtNe = new Point3d(maxCoordsX, maxCoordsY, 0);
      var bbPtSw = new Point3d(minCoordsX, minCoordsY, 0);

      Plane plane = Plane.WorldXY;

      Interval xi = new Interval(minCoordsX, maxCoordsX);
      Interval yi = new Interval(minCoordsY, maxCoordsY);

      Rectangle3d bbRect3d = new Rectangle3d(plane, xi, yi);
      bbRect = bbRect3d;

    }
    catch (Exception ex)
    {
      Component.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Error reading CSV: " + ex.Message);
    }

    // Outputs
    a = pointsTransformed;
    b = lstf_serc;
    c = lstf_flag;
    d = elv_rel;
    e = lstf_mean;
    f = ndvi_mean;
    g = ndvi_flag;
    h = ndmi_mean;
    i = idx_row;
    j = idx_col;
    k = dom_dir;
    l = dom_dir_elv;
    m = dd_geoid;
    n = mc_geoid;
    o = bbRect;
  }
}