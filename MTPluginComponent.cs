using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;

// In order to load the result of this wizard, you will also need to
// add the output bin/ folder of this project to the list of loaded
// folder in Grasshopper.
// You can use the _GrasshopperDeveloperSettings Rhino command for that.

namespace MTPlugin
{
    public class MTPluginComponent : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public MTPluginComponent()
          : base("MTPlugin", "MTP",
              "Morphological Tessellation component for urban morphological studies and parcellisation at the plot level.",
              "RhinoGIS", "Characterisation")
        {
        }


        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            // Use the pManager object to register your input parameters.
            // You can often supply default values when creating parameters.
            // All parameters must have the correct access type. If you want 
            // to import lists or trees of values, modify the ParamAccess flag.
            pManager.AddCurveParameter("Footprints", "F", "Building footprint", GH_ParamAccess.list);
            pManager.AddLineParameter("Links", "L", "Street networks as Lines", GH_ParamAccess.list);
            pManager.AddRectangleParameter("Boundary", "BB", "Boundary of analysis", GH_ParamAccess.item);
            pManager.AddNumberParameter("Segmentation Distance", "SD", "Distance of each segment while segmenting the footprints and links", GH_ParamAccess.item, 0.2);
            pManager.AddNumberParameter("Offset Distance", "OD", "Inward offset distance for building foorprint shrinkage", GH_ParamAccess.item, 0.2);



            // If you want to change properties of certain parameters, 
            // you can use the pManager instance to access them by index:
            //pManager[0].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            // Use the pManager object to register your output parameters.
            // Output parameters do not have default values, but they too must have the correct access type.
            pManager.AddPointParameter("Node Points", "Pt", "All nodes as points", GH_ParamAccess.list);
            pManager.AddLineParameter("Edges", "E", "All edges", GH_ParamAccess.list);
            pManager.AddCurveParameter("Triangles", "T", "All Triangles", GH_ParamAccess.list);
            pManager.AddCurveParameter("Cells", "C", "All Cells", GH_ParamAccess.list);
            pManager.AddIntegerParameter("NConnectivity", "NC", "Number of Connectivity", GH_ParamAccess.list);
            pManager.AddIntegerParameter("NCellC", "NCC", "Number of Cell Connectivity", GH_ParamAccess.list);
            pManager.AddCurveParameter("VS", "V", "test VS", GH_ParamAccess.list);
            pManager.AddCurveParameter("RGP", "R", "test RGP", GH_ParamAccess.list);

            //pManager.AddIntegerParameter("Footprint Points", "FP", "All footprint points as indices", GH_ParamAccess.tree);
            //pManager.AddIntegerParameter("Footprint Parcel Index", "FPPI", "Index of the corresponding parcel in parcels list", GH_ParamAccess.tree);
            //pManager.AddIntegerParameter("Parcel Bounds Loop", "PBL", "Parcel boundary loop points as indices", GH_ParamAccess.tree);
            //pManager.AddIntegerParameter("Footprint Block Index", "FPBI", "Index of the corresponding parcel in parcels list", GH_ParamAccess.tree);
            //pManager.AddIntegerParameter("Block Bounds Loop", "BBL", "Block boundary loop points as indices", GH_ParamAccess.tree);



            // Sometimes you want to hide a specific parameter from the Rhino preview.
            // You can use the HideParameter() method as a quick way:
            //pManager.HideParameter(0);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // First, we need to retrieve all data from the input parameters.
            // We'll start by declaring variables and assigning them starting values.
            List<Curve> buildings = new List<Curve>();
            List<Line> links = new List<Line>();
            Rectangle3d boundaryRectangle = new Rectangle3d();
            double segmentationDistance = 1.0;
            double offsetDistance = 0.2;
            // Then we need to access the input parameters individually. 
            // When data cannot be extracted from a parameter, we should abort this method.
            if (!DA.GetDataList(0, buildings)) return;
            if (!DA.GetDataList(1, links)) return;
            if (!DA.GetData(2, ref boundaryRectangle)) return;
            if (!DA.GetData(3, ref segmentationDistance)) return;
            if (!DA.GetData(4, ref offsetDistance)) return;


            ModelSettings settings = new ModelSettings()
            {
                OffsetDist = offsetDistance,
                LinkDivideInterval = segmentationDistance,
                FootprintDivideInterval = segmentationDistance,
            };

            Model model = new Model {
                Buildings = buildings,
                Links = links,
                BoundaryRectangle = boundaryRectangle,
                ModelSettings = settings
            };

            model.Preprocessing();
            model.Solve();
            model.PostProcessing();
            List<Point3d> points;
            List<Curve> triangles;
            List<Curve> voronoiCells;
            List<int> c1;
            List<int> c2;
            List<Curve> vs;
            List<Curve> rgp;
            model.GeometryResults(out points, out triangles, out voronoiCells);
            model.TestOut(out c1, out c2);
            model.TestOutVoronoi(out vs, out rgp);

            // assignment of output variables
            DA.SetDataList(0, points);
            
            DA.SetDataList(2, triangles);
            DA.SetDataList(3, voronoiCells);
            DA.SetDataList(4, c1);
            DA.SetDataList(5, c2);
            DA.SetDataList(6, vs);
            DA.SetDataList(7, rgp);

            //DA.SetDataList(7, dm.EdgeBelongingsInt);
        }

        /// <summary>
        /// The Exposure property controls where in the panel a component icon 
        /// will appear. There are seven possible locations (primary to septenary), 
        /// each of which can be combined with the GH_Exposure.obscure flag, which 
        /// ensures the component will only be visible on panel dropdowns.
        /// </summary>
        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.primary; }
        }

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add image files to your project resources and access them like this:
                //return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("f82d0c16-3822-4936-a445-598202575e90"); }
        }
    }
}
