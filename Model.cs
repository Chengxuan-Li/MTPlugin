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
        // input
        public List<Curve> Buildings = new List<Curve>();
        public List<Line> Links = new List<Line>();
        public Rectangle3d BoundaryRectangle = new Rectangle3d();
        public ModelSettings ModelSettings = new ModelSettings();

        // intermediate
        public List<Node2> Node2s = new List<Node2>();
        public List<NodeType> NodeTypes = new List<NodeType>();
        public List<Node2> BoundaryPoints;

        // output
        public List<Point3d> NodePoints = new List<Point3d>();
        public List<string> NodeTypesString = new List<string>();
        public List<int> NodeParentIds = new List<int>();

        public List<Polyline> Triangles = new List<Polyline>();
        public List<Polyline> TraditionalVoronoiCells = new List<Polyline>();
        public List<Polyline> GuidedVoronoiCells = new List<Polyline>();
        public List<Polyline> TraditionalRegions = new List<Polyline>();
        public List<Polyline> GuidedRegions = new List<Polyline>();
        public List<Polyline> ExtendedGuidedRegions = new List<Polyline>();
        public List<int> BuildingIds = new List<int>();


        // properties
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
        List<Region> regions = new List<Region>();


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
                regions.Add(new Region() { Id = i, RegionType = NodeType.Building });
            }

            for (int i = 0; i < Links.Count; i++)
            {
                Line line = Links[i];
                List<Point3d> pts = Helper.DivideLine(line, (int)Math.Ceiling(line.Length / ModelSettings.LinkDiscretizationInterval), false, false);
                pts.ForEach(p => Node2s.Add(new Node2(p.X, p.Y, i)));
                pts.ForEach(p => NodeTypes.Add(NodeType.Link));
            }

            BoundaryPoints = new List<Node2> {
                    new Node2(BoundaryRectangle.Corner(0).X, BoundaryRectangle.Corner(0).Y, 0),
                    new Node2(BoundaryRectangle.Corner(1).X, BoundaryRectangle.Corner(1).Y, 1),
                    new Node2(BoundaryRectangle.Corner(2).X, BoundaryRectangle.Corner(2).Y, 2),
                    new Node2(BoundaryRectangle.Corner(3).X, BoundaryRectangle.Corner(3).Y, 3)
            };

            BoundaryPoints.ForEach(n => Node2s.Add(n));
            BoundaryPoints.ForEach(n => NodeTypes.Add(NodeType.BoundaryRectangle));


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

        public void InitializeResults()
        {

            NodePoints = new List<Point3d>(); // done
            NodeTypesString = new List<string>(); // done, tests needed
            NodeParentIds = new List<int>(); // done, tests needed

            Triangles = new List<Polyline>(); // done

            TraditionalVoronoiCells = new List<Polyline>(); // done
            GuidedVoronoiCells = new List<Polyline>(); // done

            TraditionalRegions = new List<Polyline>(); // TODO
            GuidedRegions = new List<Polyline>(); // done

            BuildingIds = new List<int>(); // done
        }

        public void PostProcessing()
        {
            Node2s.ForEach(n => NodePoints.Add(new Point3d(n.x, n.y, 0)));

            NodeTypes.ForEach(nt => NodeTypesString.Add(nt.ToString()));
            Node2s.ForEach(n => NodeParentIds.Add(n.tag));




            // this is extra content for 20240101

            List<int> LLLFaces = new List<int>();

            for (int i = 0; i < faces.Count; i++)
            {
                if (NodeTypes[faces[i].A] == NodeType.Link && NodeTypes[faces[i].B] == NodeType.Link && NodeTypes[faces[i].C] == NodeType.Link)
                {
                    LLLFaces.Add(i);
                }
            }

            int iterations = 0;
            int maxIterations = 15;
            List<int> opsFaces = new List<int>();
            List<int> opsA = new List<int>();
            List<int> opsB = new List<int>();
            List<int> opsC = new List<int>();

            int iterationsWithoutImprovement = 0;

            while (iterations < maxIterations)
            {

                int j = 0;
                while (j < LLLFaces.Count)
                {
                    opsFaces = new List<int>();
                    opsA = new List<int>();
                    opsB = new List<int>();
                    opsC = new List<int>();
                    if (tryAlternateLLLFace(LLLFaces[j], ref opsFaces, ref opsA, ref opsB, ref opsC))
                    {
                        j++;
                    } else
                    {
                        j++;
                    }
                    for (int k = 0; k < opsFaces.Count; k++)
                    {
                        faces[opsFaces[k]].Set(opsA[k], opsB[k], opsC[k]);
                    }
                }


                int LLLCount = LLLFaces.Count;
                LLLFaces.RemoveAll(f => NodeTypes[faces[f].A] == NodeType.Building || (NodeTypes[faces[f].B] == NodeType.Building || NodeTypes[faces[f].C] == NodeType.Building));

                if (LLLCount == LLLFaces.Count)
                {
                    iterationsWithoutImprovement++;
                    if (iterationsWithoutImprovement >= 0)
                    {
                        break;
                    }
                } else
                {
                    iterationsWithoutImprovement = 0;
                }

                iterations++;
            }

            Node2List node2List = new Node2List();

            Node2s.ForEach(n => node2List.Append(n));
            connectivity.SolveConnectivity(node2List, faces, true);
            //faces = Grasshopper.Kernel.Geometry.Delaunay.Solver.Solve_Faces(node2List, ModelSettings.JitterAmount);
            //cells = Grasshopper.Kernel.Geometry.Voronoi.Solver.Solve_Connectivity(node2List, connectivity, BoundaryPoints);

            // end of extra content for 20240101


            faces.ForEach(f => Triangles.Add(
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
                )
            );

            cells.ForEach(c => TraditionalVoronoiCells.Add(c.ToPolyline()));

            List<int> currentNodeConnections = new List<int>();
            List<Point3d> currentGuidedVoronoiCellPoints = new List<Point3d>();

            Node2 baseNode;
            Node2 currentNode;
            Node2 nextNode;
            Node2 previousNode;



            for (int currentNodeIndex = 0; currentNodeIndex < connectivity.Count; currentNodeIndex++)
            {
                if (NodeTypes[currentNodeIndex] == NodeType.Building)
                {
                    currentNodeConnections = connectivity.GetConnections(currentNodeIndex);

                    currentGuidedVoronoiCellPoints = new List<Point3d>();


                    baseNode = Node2s[currentNodeIndex];
                    currentNodeConnections.Sort((x, y) => angleCompare(baseNode, Node2s[x], Node2s[y]));

                    for (int currentConnectionIndex = 0; currentConnectionIndex < currentNodeConnections.Count; currentConnectionIndex++)
                    {
                        int nextConnectionIndex = currentConnectionIndex + 1;
                        int previousConnectionIndex = currentConnectionIndex - 1;
                        if (currentConnectionIndex == currentNodeConnections.Count - 1)
                        {
                            nextConnectionIndex = 0;
                        }
                        if (currentConnectionIndex == 0)
                        {
                            previousConnectionIndex = currentNodeConnections.Count - 1;
                        }
                        nextNode = Node2s[currentNodeConnections[nextConnectionIndex]];
                        currentNode = Node2s[currentNodeConnections[currentConnectionIndex]];
                        previousNode = Node2s[currentNodeConnections[previousConnectionIndex]];

                        if (NodeTypes[currentNodeIndex] == NodeType.Building)
                        {
                            if (NodeTypes[currentNodeConnections[currentConnectionIndex]] == NodeType.Building)
                            {
                                if (ModelSettings.EnableAdaptiveNetwork)
                                {
                                    if (NodeTypes[currentNodeConnections[previousConnectionIndex]] == NodeType.Link)
                                    {
                                        currentGuidedVoronoiCellPoints.Add(adjustedBBBCenter(baseNode, currentNode, previousNode, false));
                                    }
                                    // this adds the center to the BBB cell
                                    //currentGuidedVoronoiCellPoints.Add(circumCenter(baseNode, currentNode, nextNode));

                                    // only one from the above and the below should be active

                                    // this adds the adjusted BBB center to the BBB cell - test
                                    if (NodeTypes[currentNodeConnections[nextConnectionIndex]] == NodeType.Link)
                                    {
                                        currentGuidedVoronoiCellPoints.Add(adjustedBBBCenter(baseNode, currentNode, nextNode, false));
                                    }
                                    else
                                    {
                                        currentGuidedVoronoiCellPoints.Add(adjustedBBBCenter(baseNode, currentNode, nextNode, true));
                                    }
                                } else
                                {
                                    if (NodeTypes[currentNodeConnections[previousConnectionIndex]] == NodeType.Link)
                                    {
                                        currentGuidedVoronoiCellPoints.Add(circumCenter(baseNode, currentNode, previousNode));
                                    }
                                    // this adds the center to the BBB cell
                                    currentGuidedVoronoiCellPoints.Add(circumCenter(baseNode, currentNode, nextNode));

                                }

                            }
                            else if (NodeTypes[currentNodeConnections[currentConnectionIndex]] == NodeType.Link)
                            {
                                currentGuidedVoronoiCellPoints.Add(new Point3d(currentNode.x, currentNode.y, 0));
                            }
                        }
                    }

                    if (currentGuidedVoronoiCellPoints.Count > 2)
                    {
                        currentGuidedVoronoiCellPoints.Add(currentGuidedVoronoiCellPoints[0]);
                    }

                    GuidedVoronoiCells.Add(new Polyline(currentGuidedVoronoiCellPoints));

                    regions[Node2s[currentNodeIndex].tag].AddPolyline(new Polyline(currentGuidedVoronoiCellPoints));

                }
            }




            Polyline polyline;
            regions.ForEach(r => GuidedRegions.Add(
                r.Compute(out polyline) ? polyline : null
                ));

            foreach (Region region in regions)
            {
                if (region.Status)
                {
                    BuildingIds.Add(region.Id);
                }
            }

        }

        public void ProcessRemainingTriangles()
        {
            ExtendedGuidedRegions = new List<Polyline>();


            List<int> remainingFaceIds = new List<int>();
            for (int i = 0; i < faces.Count; i++)
            {
                var face = faces[i];
                if (
                    ((NodeTypes[face.A] == NodeType.Link ? 1 : 0) +
                    (NodeTypes[face.B] == NodeType.Link ? 1 : 0) +
                    (NodeTypes[face.C] == NodeType.Link ? 1 : 0)) == 3
                    )
                {
                    remainingFaceIds.Add(i);
                }
            }

            List<int> idBelongings = Enumerable.Repeat(-1, remainingFaceIds.Count).ToList();
            int numStep = 0;
            int stepLimit = 2;//test
            List<int> nonAdjacentNodeIds;
            List<int> adjacentFaceIds;
            List<int> orders;
            while (idBelongings.FindIndex(i => i == -1) != -1)
            {
                for(int j = 0; j < remainingFaceIds.Count; j++)
                {
                    if (idBelongings[j] == -1)
                    {
                        int id = remainingFaceIds[j];
                        orders = OrderedByEdgeLength(id);

                        adjacentFaceIds = FindAdjacentFaces(id, out nonAdjacentNodeIds);

                        if (nonAdjacentNodeIds[orders[0]] != -1 && NodeTypes[nonAdjacentNodeIds[orders[0]]] == NodeType.Building)
                        {
                            idBelongings[j] = Node2s[nonAdjacentNodeIds[orders[0]]].tag;
                        }
                        else if (nonAdjacentNodeIds[orders[1]] != -1 && NodeTypes[nonAdjacentNodeIds[orders[1]]] == NodeType.Building)
                        {
                            idBelongings[j] = Node2s[nonAdjacentNodeIds[orders[1]]].tag;
                        }
                        else if (nonAdjacentNodeIds[orders[2]] != -1 && NodeTypes[nonAdjacentNodeIds[orders[2]]] == NodeType.Building)
                        {
                            idBelongings[j] = Node2s[nonAdjacentNodeIds[orders[2]]].tag;
                        }
                    }
                }

                numStep++;
                if (numStep >= stepLimit)
                {
                    break;
                }
            }

            for (int i = 0; i < remainingFaceIds.Count; i++)
            {
                if (idBelongings[i] != -1)
                {
                    regions[idBelongings[i]].AddPolyline(Triangles[remainingFaceIds[i]]);
                }
            }

            Polyline polyline;
            regions.ForEach(r => ExtendedGuidedRegions.Add(
                r.Compute(out polyline) ? polyline : null
                ));
        }

        List<int> OrderedByEdgeLength(int id)
        {
            var face = faces[id];
            double a = Node2s[face.B].Distance(Node2s[face.C]);
            double b = Node2s[face.A].Distance(Node2s[face.C]);
            double c = Node2s[face.A].Distance(Node2s[face.B]);
            if (a > b)
            {
                if (b > c)
                {
                    return new List<int> { 0, 1, 2};
                } else if (a > c)
                {
                    return new List<int> { 0, 2, 1 };
                } else
                {
                    return new List<int> { 2, 0, 1 };
                }
            } else
            {
                if (a > c)
                {
                    return new List<int> { 1, 0, 2 };
                } else if (b > c)
                {
                    return new List<int> { 1, 2, 0 };
                } else
                {
                    return new List<int> { 2, 1, 0 };
                }
            }    
        }

        List<int> FindFaceIdBy2Points(int a, int b, out List<int> otherPointList)
        {
            List<int> result = new List<int>();
            otherPointList = new List<int>();
            for (int i = 0; i < faces.Count; i++)
            {
                var face = faces[i];
                if (face.A == a && face.B == b)
                {
                    result.Add(i);
                    otherPointList.Add(face.C);
                } else if (face.B == a && face.A == b)
                {
                    result.Add(i);
                    otherPointList.Add(face.C);
                } else if (face.A == a && face.C == b)
                {
                    result.Add(i);
                    otherPointList.Add(face.B);
                } else if (face.C == a && face.A == b)
                {
                    result.Add(i);
                    otherPointList.Add(face.B);
                } else if (face.B == a && face.C == b)
                {
                    result.Add(i);
                    otherPointList.Add(face.A);
                } else if (face.C == a && face.B == b)
                {
                    result.Add(i);
                    otherPointList.Add(face.A);
                }
            }
            return result;
        }

        bool PointInFace(int pointId, int faceId)
        {
            var face = faces[faceId];
            return ((face.A == pointId ? 1 : 0) + (face.B == pointId ? 1 : 0) + (face.C == pointId ? 1 : 0)) == 1;
        }

        /// <summary>
        /// Find the id of adjacent faces of the given face
        /// </summary>
        /// <param name="faceId">id of the given face</param>
        /// <param name="nonAdjacentNodeIds">list of ids of the opposite nodes to the respectful edge of the given face, arranged in BC, AC, AB order</param>
        /// <returns>list of ids of the adjacent face via the respectful edge of the given face, arranged in BC, AC, AB order</returns>
        List<int> FindAdjacentFaces(int faceId, out List<int> nonAdjacentNodeIds)
        {
            List<int> result = new List<int>();
            nonAdjacentNodeIds = new List<int>();
            var face = faces[faceId];
            List<int> otherNodeList;
            var adjacenciesBC = FindFaceIdBy2Points(face.B, face.C, out otherNodeList);
            if (adjacenciesBC.Count == 2)
            {
                if (PointInFace(face.A, adjacenciesBC[0]))
                {
                    result.Add(adjacenciesBC[1]);
                    nonAdjacentNodeIds.Add(otherNodeList[1]);
                } else
                {
                    result.Add(adjacenciesBC[0]);
                    nonAdjacentNodeIds.Add(otherNodeList[0]);
                }
            } else
            {
                result.Add(-1);
                nonAdjacentNodeIds.Add(-1);
            }
            var adjacenciesAC = FindFaceIdBy2Points(face.A, face.C, out otherNodeList);
            if (adjacenciesAC.Count == 2)
            {
                if (PointInFace(face.B, adjacenciesAC[0]))
                {
                    result.Add(adjacenciesAC[1]);
                    nonAdjacentNodeIds.Add(otherNodeList[1]);
                }
                else
                {
                    result.Add(adjacenciesAC[0]);
                    nonAdjacentNodeIds.Add(otherNodeList[0]);
                }
            } else
            {
                result.Add(-1);
                nonAdjacentNodeIds.Add(-1);
            }
            var adjacenciesAB = FindFaceIdBy2Points(face.A, face.B, out otherNodeList);
            if (adjacenciesAB.Count == 2)
            {
                if (PointInFace(face.C, adjacenciesAB[0]))
                {
                    result.Add(adjacenciesAB[1]);
                    nonAdjacentNodeIds.Add(otherNodeList[1]);
                }
                else
                {
                    result.Add(adjacenciesAB[0]);
                    nonAdjacentNodeIds.Add(otherNodeList[0]);
                }
            } else
            {
                result.Add(-1);
                nonAdjacentNodeIds.Add(-1);
            }
            return result;
        }


        /*
        public void TestOutVoronoi(out List<Curve> voronoi, out List<Curve> regionsOutline, out List<Curve> combinedOutline, out List<int> rgpid)
        {
            List<Curve> vs = new List<Curve>();
            List<int> cs = new List<int>();
            List<Curve> rg = new List<Curve>();
            List<Curve> cb = new List<Curve>();
            List<int> ri = new List<int>();

            List<Point3d> nextTriCP = new List<Point3d>();
            List<Point3d> rgP = new List<Point3d>();
            List<int> rgPId = new List<int>();
            Node2 nn;
            Node2 thisNode;
            Node2 nextNode;
            Node2 prevNode;



            for (int i = 0; i < connectivity.Count; i++)
            {
                cs = connectivity.GetConnections(i);
                
                nextTriCP = new List<Point3d>();
                rgP = new List<Point3d>();
                rgPId = new List<int>();

                nn = Node2s[i];
                cs.Sort((x, y) => angleCompare(nn, Node2s[x], Node2s[y]));

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
                    nextTriCP.Add(circumCenter(nn, thisNode, prevNode));
                    //CircumCenter(nn, thisNode, prevNode)
                    //new Point3d(thisNode.x, thisNode.y, 0)

                    if (NodeTypes[i] == NodeType.Building)
                    {
                        if (NodeTypes[cs[j]] == NodeType.Building)
                        {
                            if (NodeTypes[cs[prev]] == NodeType.Link)
                            {
                                rgP.Add(circumCenter(nn, thisNode, prevNode));
                            }
                            rgP.Add(circumCenter(nn, thisNode, nextNode));

                        } else if (NodeTypes[cs[j]] == NodeType.Link)
                        {
                            rgP.Add(new Point3d(thisNode.x, thisNode.y, 0));
                        }
                    }
                }
                //nextTriCP.Sort((x, y) => AngleCompare(nn, new Node2(x.X, x.Y), new Node2(y.X, y.Y)));
                nextTriCP.Add(nextTriCP[0]);
                if (rgP.Count > 0)
                {
                    rgP.Add(rgP[0]);
                }
                vs.Add((new Polyline(nextTriCP)).ToNurbsCurve());
                rg.Add((new Polyline(rgP)).ToNurbsCurve());
                if (NodeTypes[i] == NodeType.Building)
                {
                    regions[Node2s[i].tag].Add(rgP);
                    regions[Node2s[i].tag].AddPolyline((new Polyline(rgP)).ToNurbsCurve());
                }
                ri.Add(Node2s[i].tag);
            }

            voronoi = vs;
            regionsOutline = rg;


            Curve crv;
            regions.ForEach(r => cb.Add(
                r.Compute(out crv) ? crv : null
                ));
            
            combinedOutline = cb;



            rgpid = ri;


        }*/



        internal class Region
        {
            public int Id;
            public NodeType RegionType;
            public bool Status = false;
            //List<Point3d> pts = new List<Point3d>();
            //List<int> ptsId = new List<int>();
            //List<List<Point3d>> waitlist = new List<List<Point3d>>();
            //List<List<int>> waitlistIds = new List<List<int>>();


            List<Polyline> polylines = new List<Polyline>();

            List<Curve> curves
            {
                get
                {
                    List<Curve> crvs = new List<Curve>();
                    polylines.ForEach(p => crvs.Add(p.ToNurbsCurve()));
                    return crvs;
                }
            }

            /*
            public List<Point3d> OutlinePts
            {
                get
                {
                    List<Point3d> op = new List<Point3d>();
                    ptsId.ForEach(pi => op.Add(pts[pi]));
                    op.Add(op[0]);
                    return op;
                }
            }
            */

            public void AddPolyline(Polyline polyline)
            {
                Status = false;
                polylines.Add(polyline);
            }

            public bool Compute(out Polyline polyline)
            {
                Status = false;
                polyline = null;
                if (polylines.Count == 0)
                {
                    return false;
                }

                var crvs = Curve.CreateBooleanUnion(curves, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                if (crvs.Length > 0)
                {
                    if (crvs[0].TryGetPolyline(out polyline))
                    {
                        polylines = new List<Polyline> { polyline };
                        Status = true;
                        return true;
                    }
                    return false;
                } else
                {
                    return false;
                }
            }

            /*
            public void Add(List<Point3d> ps)
            {
                List<Point3d> wlp = new List<Point3d>();
                List<int> wlpId = new List<int>();
                ps.ForEach(p => wlp.Add(p));
                wlpId.AddRange(Enumerable.Range(0, ps.Count));
                waitlist.Add(wlp);
                waitlistIds.Add(wlpId);

            }

            public Curve AltJoinRegion()
            {
                List<Curve> crvs = new List<Curve>();
                waitlist.ForEach(w => crvs.Add(
                    new Polyline(w).ToNurbsCurve()
                    ));
                var c = Curve.CreateBooleanUnion(crvs, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                if (c.Length > 0)
                {
                    return c[0];
                } else
                {
                    return null;
                }
                
            }


            public void ProcessWaitlist()
            {
                List<int> jr;
                List<int> psId = new List<int>();

                for (int i = 0; i < waitlist.Count; i++)
                {
                    for (int j = 0; j < waitlist[i].Count; j++)
                    {
                        int pos = pts.FindIndex(pt => pt.DistanceTo(waitlist[i][j]) < RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                        if (pos < 0)
                        {
                            waitlistIds[i][j] = pts.Count;
                            pts.Add(waitlist[i][j]);
                        } else
                        {
                            waitlistIds[i][j] = pos;
                        }
                    }
                }

                //给region join的结果加上成功与否的判断，条件来自于共享点数>2，然后选择能够join的优先join
                ptsId = new List<int>();
                waitlistIds[0].ForEach(i => ptsId.Add(i));
                waitlistIds.RemoveAt(0);

                int wi = 0;
                while (waitlistIds.Count > 0)
                {
                    if (numCommon(ptsId, waitlistIds[wi]) >= 2)
                    {
                        if (numCommon(ptsId, waitlistIds[wi]) == waitlistIds[wi].Count)
                        {
                            waitlistIds.RemoveAt(0);
                            wi = 0;
                        } else if (numCommon(ptsId, waitlistIds[wi]) == ptsId.Count)
                        {
                            ptsId = new List<int>();
                            waitlistIds[wi].ForEach(r => ptsId.Add(r));
                            waitlistIds.RemoveAt(0);
                            wi = 0;
                        } 
                        
                        if (waitlistIds.Count == 0)
                        {
                            break;
                        }

                        jr = new List<int>();
                        joinRegion(ptsId, waitlistIds[wi], out jr);
                        ptsId = new List<int>();
                        jr.ForEach(r => ptsId.Add(r));
                        waitlistIds.RemoveAt(0);
                        wi = 0;

                    } else
                    {
                        wi++;
                        if (wi >= waitlistIds.Count)
                        {
                            break;
                        }
                    }
                }
            }


            int numCommon(List<int> regionA, List<int> regionB)
            {
                int num = 0;
                regionA.ForEach(a =>
                num += (regionB.FindIndex(b => b == a) >= 0 ? 1 : 0)
                    );
                return num;
            }

            void joinRegion(List<int> regionA, List<int> regionB, out List<int> joinedRegion)
            {
                List<int> jr = new List<int>();
                List<int> ra = new List<int>();
                List<int> rb = new List<int>();
                List<int> posAinB = new List<int>();
                List<int> posBinA = new List<int>();

                regionA.ForEach(a => ra.Add(a));
                ra.RemoveAt(ra.Count - 1);
                regionB.ForEach(b => rb.Add(b));
                rb.RemoveAt(rb.Count - 1);

                if (numCommon(ra, rb) == ra.Count)
                {
                    joinedRegion = regionB;
                    return;
                } else if (numCommon(ra, rb) == rb.Count)
                {
                    joinedRegion = regionA;
                    return;
                }


                while (rb.FindIndex(b => b == ra[0]) != -1)
                {
                    ra.Add(ra[0]);
                    ra.RemoveAt(0);
                }

                while (ra.FindIndex(a => a == rb[0]) != -1)
                {
                    rb.Add(rb[0]);
                    rb.RemoveAt(0);
                }

                ra.ForEach(a => posBinA.Add(rb.FindIndex(b => b == a)));
                rb.ForEach(b => posAinB.Add(ra.FindIndex(a => a == b)));

                var fA = posBinA.FindAll(e => e >= 0);
                var fB = posAinB.FindAll(e => e >= 0);

                if (fA[0] > fA[1])
                {
                    jr.AddRange(ra.GetRange(0, fB[fB.Count - 1]));
                    var sg1 = rb.GetRange(fA[0], rb.Count - fA[0]);
                    jr.AddRange(sg1);
                    var sg2 = rb.GetRange(0, fA[fA.Count - 1]);
                    jr.AddRange(sg2);
                    jr.AddRange(ra.GetRange(fB[0], ra.Count - fB[0]));

                }
                else
                {
                    jr.AddRange(ra.GetRange(0, fB[0] + 1));
                    var sg1 = rb.GetRange(0, fA[0]);
                    sg1.Reverse();
                    jr.AddRange(sg1);
                    if (rb.Count - fA[fA.Count - 1] >= 2)
                    {
                        var sg2 = rb.GetRange(fA[fA.Count - 1] + 1, rb.Count - fA[fA.Count - 1] - 1);
                        sg2.Reverse();
                        jr.AddRange(sg2);
                    }
                    jr.AddRange(ra.GetRange(fB[fB.Count - 1], ra.Count - fB[fB.Count - 1]));
                }

                joinedRegion = jr;
                joinedRegion.Add(joinedRegion[0]);
            }
            */
        }

        int angleCompare(Node2 origin, Node2 A, Node2 B)
        {
            double ax = A.x - origin.x;
            double ay = A.y - origin.y;
            double bx = B.x - origin.x;
            double by = B.y - origin.y;
            double aq = quadrant(ax, ay);
            double bq = quadrant(bx, by);

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

        double quadrant(double x, double y)
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

        Point3d circumCenter(Node2 A, Node2 B, Node2 C)
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

        Point3d adjustedBBBCenter(Node2 A, Node2 B, Node2 C, bool Simplify)
        {
            if (Math.Max(A.Distance(B), Math.Max(B.Distance(C), A.Distance(C))) >= 1.5 * ModelSettings.FootprintDiscretizationInterval)
            {
                Simplify = false;
            }
            if (Simplify)
            {
                if (A.tag == B.tag && B.tag == C.tag)
                {
                    return circumCenter(A, B, C);
                }
                if (A.tag == B.tag && A.Distance(B) <= 1.5 * ModelSettings.FootprintDiscretizationInterval)
                {
                    return new Point3d(
                        A.x / 4 + B.x / 4 + C.x / 2,
                        A.y / 4 + B.y / 4 + C.y / 2,
                        0
                        );
                }
                else if (A.tag == C.tag && A.Distance(C) <= 1.5 * ModelSettings.FootprintDiscretizationInterval)
                {
                    return new Point3d(
                        A.x / 4 + B.x / 2 + C.x / 4,
                        A.y / 4 + B.y / 2 + C.y / 4,
                        0
                        );
                }
                else if (B.tag == C.tag && B.Distance(C) <= 1.5 * ModelSettings.FootprintDiscretizationInterval)
                {
                    return new Point3d(
                        A.x / 2 + B.x / 4 + C.x / 4,
                        A.y / 2 + B.y / 4 + C.y / 4,
                        0
                        );
                }
                else
                {
                    return circumCenter(A, B, C);
                    /*
                    return new Point3d(
                        A.x / 3 + B.x / 3 + C.x / 3,
                        A.y / 3 + B.y / 3 + C.y / 3,
                        0
                        );
                    */
                }
            } else
            {
                return circumCenter(A, B, C);
            }
 

        }

        bool tryAlternateLLLFace(int faceId, ref List<int> opsFaces, ref List<int> opsA, ref List<int> opsB, ref List<int> opsC)
        {

            List<int> nonAdjacentNodeIds;
            List<int> adjacentFaces = FindAdjacentFaces(faceId, out nonAdjacentNodeIds);
            int adjacentFaceId = -1;
            int A = -1;
            int B = -1;
            int C = -1;
            int D = -1;

            List<double> edgeLengths = new List<double>
            {
                Node2s[faces[faceId].B].Distance(Node2s[faces[faceId].C]),
                Node2s[faces[faceId].A].Distance(Node2s[faces[faceId].C]),
                Node2s[faces[faceId].A].Distance(Node2s[faces[faceId].B]),
            };

            double argumentationRatio = 1.05;

            if (nonAdjacentNodeIds[0] != -1 && edgeLengths[0] * argumentationRatio >= edgeLengths[1] && edgeLengths[0] * argumentationRatio >= edgeLengths[2])
            {
                A = faces[faceId].A;
                B = faces[faceId].B;
                C = faces[faceId].C;
                D = nonAdjacentNodeIds[0];
                adjacentFaceId = adjacentFaces[0];
            } else if (nonAdjacentNodeIds[1] != -1 && edgeLengths[1] * argumentationRatio >= edgeLengths[0] && edgeLengths[1] * argumentationRatio >= edgeLengths[2])
            {
                A = faces[faceId].B;
                B = faces[faceId].C;
                C = faces[faceId].A;
                D = nonAdjacentNodeIds[1];
                adjacentFaceId = adjacentFaces[1];
            } else if (nonAdjacentNodeIds[2] != -1 && edgeLengths[2] * argumentationRatio >= edgeLengths[0] && edgeLengths[2] * argumentationRatio >= edgeLengths[1])
            {
                A = faces[faceId].C;
                B = faces[faceId].A;
                C = faces[faceId].B;
                D = nonAdjacentNodeIds[2];
                adjacentFaceId = adjacentFaces[2];
            } else
            {
                return false;
            }
            opsFaces.Add(faceId);
            opsA.Add(A);
            opsB.Add(C);
            opsC.Add(D);

            opsFaces.Add(adjacentFaceId);
            opsA.Add(A);
            opsB.Add(B);//this one is wrong
            opsC.Add(D);
            return true;

            /*
            int tempB;
            int tempC;
            double maxEdgeLength = -1.0;
            


            
            for (int i = 0; i < nonAdjacentNodeIds.Count; i++)
            {
                if (nonAdjacentNodeIds[i] != -1 && NodeTypes[nonAdjacentNodeIds[i]] == NodeType.Building)
                {
                    double currentEdgeLength;
                    if (faces[adjacentFaces[i]].A == nonAdjacentNodeIds[i])
                    {
                        currentEdgeLength = Node2s[faces[adjacentFaces[i]].B].Distance(Node2s[faces[adjacentFaces[i]].C]);
                        tempB = faces[adjacentFaces[i]].B;
                        tempC = faces[adjacentFaces[i]].C;
                    } else if (faces[adjacentFaces[i]].B == nonAdjacentNodeIds[i])
                    {
                        currentEdgeLength = Node2s[faces[adjacentFaces[i]].A].Distance(Node2s[faces[adjacentFaces[i]].C]);
                        tempB = faces[adjacentFaces[i]].C;
                        tempC = faces[adjacentFaces[i]].A;

                    } else
                    {
                        currentEdgeLength = Node2s[faces[adjacentFaces[i]].A].Distance(Node2s[faces[adjacentFaces[i]].B]);
                        tempB = faces[adjacentFaces[i]].B;
                        tempC = faces[adjacentFaces[i]].A;
                    }
                    if (currentEdgeLength > maxEdgeLength)
                    {
                        maxEdgeLength = currentEdgeLength;
                        B = tempB;
                        C = tempC;
                        D = nonAdjacentNodeIds[i];
                        adjacentFaceId = adjacentFaces[i];
                    }
                }
            }
            

            if (maxEdgeLength < 0)
            {
                return false;
            } else
            {
                A = ((B == faces[faceId].A || B == faces[faceId].B) && (C == faces[faceId].A || C == faces[faceId].B))
                    ? faces[faceId].C
                    : ((B == faces[faceId].C || B == faces[faceId].A) && (C == faces[faceId].C || C == faces[faceId].A))
                    ? faces[faceId].B
                    : faces[faceId].A;
      
                opsFaces.Add(faceId);
                opsA.Add(A);
                opsB.Add(C);
                opsC.Add(D);

                opsFaces.Add(adjacentFaceId);
                opsA.Add(A);
                opsB.Add(B);//this one is wrong
                opsC.Add(D);
                return true;

           
            }
            */


        }

        bool analyseAdjacentTriangles(int a, int b, out int A, out int B, out int C, out int D, out int idA, out int idB)
        {
            List<int> otherPoints;
            List<int> faceIds;
            A = -1;
            B = -1;
            C = -1;
            D = -1;
            idA = -1;
            idB = -1;
            faceIds = FindFaceIdBy2Points(a, b, out otherPoints);
            if (otherPoints.Count < 2)
            {
                return false;
            }
            if (NodeTypes[otherPoints[0]] == NodeType.Building || NodeTypes[otherPoints[1]] == NodeType.Building)
            {

                A = a;
                B = otherPoints[0];
                C = b;
                D = otherPoints[1];
                idA = faceIds[0];
                idB = faceIds[1];
                return true;
            }
            return false;
        }


        /*
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
        */
    }
}
