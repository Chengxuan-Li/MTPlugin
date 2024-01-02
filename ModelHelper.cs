using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Geometry;
using Rhino.Geometry;
using Rhino;
using Grasshopper;

namespace MTPlugin
{
    public class ModelHelper
    {
        public ModelSettings Settings = new ModelSettings();

        public int AddPt(List<Point3d> pts, Point3d testPt)
        {
            for (int i = 0; i < pts.Count; i++)
            {
                if (IsPointCoincident(pts[i], testPt))
                {
                    return i;
                }
            }
            pts.Add(testPt);
            return pts.Count - 1;
        }

        public bool IsPointCoincident(Point3d ptA, Point3d ptB)
        {
            if (ptA.DistanceTo(ptB) < Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        // 1218 this seems to be working
        public Polyline OffsetInwards(Polyline polyline)
        {
            var offsetResult = polyline.ToPolylineCurve().Offset(Rhino.Geometry.Plane.WorldXY, Settings.OffsetDistance, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, CurveOffsetCornerStyle.Sharp);
            if (polyline.ToNurbsCurve().Contains(offsetResult[0].PointAt(0.5), Rhino.Geometry.Plane.WorldXY, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance) != PointContainment.Inside)
            {
                offsetResult = polyline.ToPolylineCurve().Offset(Rhino.Geometry.Plane.WorldXY, -Settings.OffsetDistance, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, CurveOffsetCornerStyle.Sharp);
            }
            Polyline pl;
            offsetResult[0].TryGetPolyline(out pl);
            return pl;
        }

        public List<Point3d> DividePolyline(Polyline polyline)
        {
            List<Point3d> pts = new List<Point3d>();

            var segments = polyline.GetSegments();
            foreach (Line line in segments)
            {
                pts.AddRange(DivideLine(line, (int)Math.Ceiling(line.Length / Settings.FootprintDiscretizationInterval), true, false));
            }
            return pts;

        }


        public List<Point3d> DivideLine(Line line, double interval)
        {
            return DivideLine(line, (int)Math.Ceiling(line.Length / interval), true, false);
        }

        public List<Point3d> DivideLine(Line line)
        {
            return DivideLine(line, (int)Math.Ceiling(line.Length / Settings.FootprintDiscretizationInterval), true, false);
        }

        public List<Point3d> DivideLine(Line line, int number, bool start, bool end)
        {
            List<Point3d> pts = new List<Point3d>();
            Vector3d vec = new Vector3d(-line.From + line.To);
            vec.Unitize();
            for (int i = 0; i < number; i++)
            {
                pts.Add(line.From + vec * line.Length / number * i);
            }
            if (end)
            {
                pts.Add(line.To);
            }
            if (start)
            {
                return pts;
            }
            else
            {
                pts.RemoveAt(0);
                return pts;
            }
        }


    }
}
