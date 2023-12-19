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
    public class Model
    {
        public List<Curve> Buildings = new List<Curve>();
        public List<Line> Boundaries = new List<Line>();
        public Rectangle3d BoundaryRectangle = new Rectangle3d();
        public ModelSettings ModelSettings = new ModelSettings();

        public List<Node2> Node2s = new List<Node2>();
        public List<NodeType> NodeTypes = new List<NodeType>();



        
        public ModelHelper Helper
        {
            get
            {
                return new ModelHelper { Settings = ModelSettings };
            }
        }

        Grasshopper.Kernel.Geometry.Delaunay.Connectivity connectivity;
        List<Grasshopper.Kernel.Geometry.Delaunay.Face> faces;
        

        public void Preprocessing()
        {
            for (int i = 0; i < Buildings.Count; i++)
            {
                Polyline pl;
                Buildings[i].TryGetPolyline(out pl);
                pl = Helper.OffsetInwards(pl);
                List<Point3d> pts = Helper.DividePolyline(pl);
                pts.ForEach(p => Node2s.Add(new Node2(p.X, p.Y, i)));
                pts.ForEach(p => NodeTypes.Add(NodeType.Building));
            }

            for (int i = 0; i < Boundaries.Count; i++)
            {
                Line line = Boundaries[i];
                List<Point3d> pts = Helper.DivideLine(line, (int)Math.Ceiling(line.Length / ModelSettings.FootprintDivideInterval), false, false);
                pts.ForEach(p => Node2s.Add(new Node2(p.X, p.Y, i)));
                pts.ForEach(p => NodeTypes.Add(NodeType.Boundary));
            }

        }

        public void Solve()
        {
            Node2List node2List = new Node2List(Node2s);
            connectivity = Grasshopper.Kernel.Geometry.Delaunay.Solver.Solve_Connectivity(node2List, ModelSettings.JitterAmount, false);
            faces = Grasshopper.Kernel.Geometry.Delaunay.Solver.Solve_Faces(node2List, ModelSettings.JitterAmount);
        }

        public void PostProcessing()
        {

        }

        public void GeometryResults(out List<Point3d> points, out List<Curve> triangles)
        {
            List<Point3d> pts = new List<Point3d>();
            List<Curve> ts = new List<Curve>();

            Node2s.ForEach(n => pts.Add(new Point3d(n.x, n.y, 0)));
            faces.ForEach(f => ts.Add(
                (
                new Polyline(
                    new List<Point3d> {
                        new Point3d(
                            Node2s[f.A].x, Node2s[f.A].y, 0
                            ),
                        new Point3d(
                            Node2s[f.B].x, Node2s[f.B].y, 0
                            ),
                        new Point3d(
                            Node2s[f.C].x, Node2s[f.C].y, 0
                            ),
                        new Point3d(
                            Node2s[f.A].x, Node2s[f.A].y, 0
                            )
                    }
                    )
                ).ToNurbsCurve()
                )
            );

            points = pts;
            triangles = ts;
        }

    }
}
