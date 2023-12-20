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
        public List<Line> Links = new List<Line>();
        public Rectangle3d BoundaryRectangle = new Rectangle3d();
        public ModelSettings ModelSettings = new ModelSettings();

        public List<Node2> Node2s = new List<Node2>();
        public List<NodeType> NodeTypes = new List<NodeType>();

        public List<Node2> BoundaryPoints;


        
        public ModelHelper Helper
        {
            get
            {
                return new ModelHelper { Settings = ModelSettings };
            }
        }

        Grasshopper.Kernel.Geometry.Delaunay.Connectivity connectivity;
        List<Grasshopper.Kernel.Geometry.Delaunay.Face> faces;
        List<Grasshopper.Kernel.Geometry.Voronoi.Cell2> cells;

        

        public void Preprocessing()
        {
            int id = 0;
            for (int i = 0; i < Buildings.Count; i++)
            {
                Polyline pl;
                Buildings[i].TryGetPolyline(out pl);
                pl = Helper.OffsetInwards(pl);
                List<Point3d> pts = Helper.DividePolyline(pl);
                pts.ForEach(p => Node2s.Add(new Node2(p.X, p.Y, id)));
                pts.ForEach(p => NodeTypes.Add(NodeType.Building));
                id++;
            }

            for (int i = 0; i < Links.Count; i++)
            {
                Line line = Links[i];
                List<Point3d> pts = Helper.DivideLine(line, (int)Math.Ceiling(line.Length / ModelSettings.FootprintDivideInterval), false, false);
                pts.ForEach(p => Node2s.Add(new Node2(p.X, p.Y, id)));
                pts.ForEach(p => NodeTypes.Add(NodeType.Link));
                id++;
            }

            BoundaryPoints = new List<Node2> {
                    new Node2(BoundaryRectangle.Corner(0).X, BoundaryRectangle.Corner(0).Y, id),
                    new Node2(BoundaryRectangle.Corner(1).X, BoundaryRectangle.Corner(1).Y, id + 1),
                    new Node2(BoundaryRectangle.Corner(2).X, BoundaryRectangle.Corner(2).Y, id + 2),
                    new Node2(BoundaryRectangle.Corner(3).X, BoundaryRectangle.Corner(3).Y, id + 3)
            };
            id += 4;


            //BoundaryPoints.ForEach(p => Node2s.Add(p));
            //BoundaryPoints.ForEach(p => NodeTypes.Add(NodeType.BoundaryRectangle));

        }

        public void Solve()
        {
            Node2List node2List = new Node2List();
            Node2s.ForEach(n => node2List.Append(n));
            connectivity = Grasshopper.Kernel.Geometry.Delaunay.Solver.Solve_Connectivity(node2List, ModelSettings.JitterAmount, true);
            faces = Grasshopper.Kernel.Geometry.Delaunay.Solver.Solve_Faces(node2List, ModelSettings.JitterAmount);
            cells = Grasshopper.Kernel.Geometry.Voronoi.Solver.Solve_Connectivity(node2List, connectivity, BoundaryPoints);

        }   

        public void PostProcessing()
        {

        }

        public void GeometryResults(out List<Point3d> points, out List<Curve> triangles, out List<Curve> voronoiCells)
        {
            List<Point3d> pts = new List<Point3d>();
            List<Curve> ts = new List<Curve>();
            List<Curve> cs = new List<Curve>();

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

            

            //cells.ForEach(c => cs.Add(HandleCell(c).ToNurbsCurve()));
            /*
            for (int i = 0; i < cells.Count; i++)
            {
                cs.Add(HandleCell(cells[i], i).ToNurbsCurve());
            }
            */
            points = pts;
            triangles = ts;
            //voronoiCells = cs;
            cells.ForEach(c => cs.Add(c.ToPolyline().ToNurbsCurve()));
            voronoiCells = cs;
        }

        public void TestOut(out List<int> con1, out List<int> con2)
        {
            List<int> c1 = new List<int>();
            List<int> c2 = new List<int>();
            for (int i = 0; i < connectivity.Count; i++)
            {
                c1.Add(connectivity.GetConnections(i).Count);
            }
            cells.ForEach(c => c2.Add(c.C.Count));
            con1 = c1;
            con2 = c2;
        }

        public void TestOutVoronoi(out List<Curve> voronoi, out List<Curve> regions)
        {
            List<Curve> vs = new List<Curve>();
            List<int> cs = new List<int>();
            List<Curve> rg = new List<Curve>();

            List<Point3d> nextTriCP = new List<Point3d>();
            List<Point3d> rgP = new List<Point3d>();
            Node2 nn;
            Node2 thisNode;
            Node2 nextNode;
            Node2 prevNode;



            for (int i = 0; i < connectivity.Count; i++)
            {
                cs = connectivity.GetConnections(i);
                
                nextTriCP = new List<Point3d>();
                rgP = new List<Point3d>();

                nn = Node2s[i];
                cs.Sort((x, y) => AngleCompare(nn, Node2s[x], Node2s[y]));

                for (int j = 0; j < cs.Count; j++)
                {
                    int next = j + 1;
                    int prev = j - 1;
                    if (j == cs.Count - 1)
                    {
                        next = 0;
                    }
                    if (j == 0)
                    {
                        prev = cs.Count - 1;
                    }
                    nextNode = Node2s[cs[next]];
                    thisNode = Node2s[cs[j]];
                    prevNode = Node2s[cs[prev]];
                    nextTriCP.Add(CircumCenter(nn, thisNode, prevNode));
                    //CircumCenter(nn, thisNode, prevNode)
                    //new Point3d(thisNode.x, thisNode.y, 0)

                    if (NodeTypes[i] == NodeType.Building)
                    {
                        if (NodeTypes[cs[j]] == NodeType.Building)
                        {
                            if (NodeTypes[cs[prev]] == NodeType.Link)
                            {
                                rgP.Add(CircumCenter(nn, thisNode, prevNode));
                            }
                            rgP.Add(CircumCenter(nn, thisNode, nextNode));

                        } else if (NodeTypes[cs[j]] == NodeType.Link)
                        {
                            rgP.Add(new Point3d(thisNode.x, thisNode.y, 0));
                        }
                    }





                }
                //nextTriCP.Sort((x, y) => AngleCompare(nn, new Node2(x.X, x.Y), new Node2(y.X, y.Y)));
                nextTriCP.Add(nextTriCP[0]);
                
                
                vs.Add((new Polyline(nextTriCP)).ToNurbsCurve());
                rg.Add((new Polyline(rgP)).ToNurbsCurve());

            }

            voronoi = vs;
            regions = rg;

        }

        int AngleCompare(Node2 origin, Node2 A, Node2 B)
        {
            double ax = A.x - origin.x;
            double ay = A.y - origin.y;
            double bx = B.x - origin.x;
            double by = B.y - origin.y;
            double aq = Quadrant(ax, ay);
            double bq = Quadrant(bx, by);

            if (aq != bq)
            {
                return aq.CompareTo(bq);
            } else
            {
                if (aq == 1.0 || aq == 3.0)
                {
                    return (ay / ax).CompareTo(by / bx);
                } else
                {
                    return (ay / ax).CompareTo(by / bx);
                }
            }
        }

        double Quadrant(double x, double y)
        {
            if (x == 0 && y > 0)
            {
                return 1.5;
            }
            if (x == 0 && y < 0)
            {
                return 3.5;
            }
            if (x > 0 && y == 0)
            {
                return 0.5;
            }
            if (x < 0 && y == 0)
            {
                return 2.5;
            }

            if (x > 0 && y > 0)
            {
                return 1.0;
            }
            if (x < 0 && y > 0)
            {
                return 2.0;
            }
            if (x < 0 && y < 0)
            {
                return 3.0;
            } else
            {
                return 4.0;
            }


        }

        Point3d CircumCenter(Node2 A, Node2 B, Node2 C)
        {

            // Calculate midpoints of sides
            Point3d midAB = new Point3d((A.x + B.x) / 2.0, (A.y + B.y) / 2.0, 0);
            Point3d midBC = new Point3d((B.x + C.x) / 2.0, (B.y + C.y) / 2.0, 0);

            // Calculate slopes of perpendicular bisectors
            double slopeAB = -1 / ((B.y - A.y) / (B.x - A.x));
            double slopeBC = -1 / ((C.y - B.y) / (C.x - B.x));

            // Calculate y-intercepts of perpendicular bisectors
            double yInterceptAB = midAB.Y - slopeAB * midAB.X;
            double yInterceptBC = midBC.Y - slopeBC * midBC.X;

            // Calculate circumcenter coordinates
            double circumcenterX = (yInterceptBC - yInterceptAB) / (slopeAB - slopeBC);
            double circumcenterY = slopeAB * circumcenterX + yInterceptAB;

            return new Point3d(circumcenterX, circumcenterY, 0);
        }

        Polyline HandleCell(Grasshopper.Kernel.Geometry.Voronoi.Cell2 cell, int cid)
        {
            List<Node2> node2s = cell.C;
            Node2 center = cell.M;

            int centerId = node2s.FindIndex(n => n.Distance(center) <= RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
            centerId = cid;

            Polyline pl = cell.ToPolyline();
            Polyline resultPolyline;
            List<Line> segments = new List<Line>(pl.GetSegments());
            List<Line> resultSegments = new List<Line>();

            int previous;
            int next;
            for (int i = 0; i < segments.Count; i++)
            {
                previous = i - 1;
                next = i + 1;
                if (i == segments.Count - 1)
                {
                    next = 0;
                } else if (i == 0)
                {
                    previous = segments.Count - 1;
                }
                
                if (NodeTypes[connectivity.GetConnections(centerId)[i]] != NodeType.Link)
                {
                    resultSegments.Add(segments[i]);
                } else
                {
                    if (NodeTypes[connectivity.GetConnections(centerId)[previous]] == NodeType.Link)
                    {
                        resultSegments.Add(new Line(new Point3d(node2s[previous].x, node2s[previous].y, 0), new Point3d(node2s[i].x, node2s[i].y, 0)));
                    } else
                    {
                        resultSegments.Add(new Line(segments[previous].To, new Point3d(node2s[i].x, node2s[i].y, 0)));
                    }
                    var c = connectivity.GetConnections(centerId);
                    if (NodeTypes[connectivity.GetConnections(centerId)[next]] == NodeType.Link)
                    {
                        resultSegments.Add(new Line(new Point3d(node2s[i].x, node2s[i].y, 0), new Point3d(node2s[next].x, node2s[next].y, 0)));
                    }
                    else
                    { 
                        resultSegments.Add(new Line(new Point3d(node2s[i].x, node2s[i].y, 0), segments[next].From));
                    }
                }
            }
            List<NurbsCurve> resultCurveSegments = new List<NurbsCurve>();
            resultSegments.ForEach(s => resultCurveSegments.Add(s.ToNurbsCurve()));
            Curve.JoinCurves(resultCurveSegments)[0].TryGetPolyline(out resultPolyline);
            return resultPolyline;


        }

    }
}
