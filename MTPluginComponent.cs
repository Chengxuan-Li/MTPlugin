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
            pManager.AddNumberParameter("Footprint Discretization Interval", "FDI", "Interval of discretization for building footprints", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Links Discretization Interval", "LDI", "Interval of discretization for links", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Offset Distance", "OD", "Inward offset distance for building foorprint shrinkage", GH_ParamAccess.item, 1.0); 
            pManager.AddBooleanParameter("Enable Adaptive Network", "AN", "Enable Adaptive Network", GH_ParamAccess.item);


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
            pManager.AddTextParameter("Node Type", "Type", "Types of all nodes", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Node Parent Id", "PId", "Parent ids of all nodes", GH_ParamAccess.list);
            pManager.AddCurveParameter("Triangles", "T", "All Triangles", GH_ParamAccess.list);
            pManager.AddCurveParameter("TVCells", "TVC", "All Traditional Voronoi Cells", GH_ParamAccess.list);
            pManager.AddCurveParameter("GVCells", "GVC", "All Guided Voronoi Cells", GH_ParamAccess.list);
            pManager.AddCurveParameter("Traditional Regions", "TR", "All Traditional Regions", GH_ParamAccess.list);
            pManager.AddCurveParameter("Guided Regions", "GR", "All Guided Regions", GH_ParamAccess.list);
            pManager.AddCurveParameter("Extended Guided Regions", "EGR", "Extended Guided Regions", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Building Id", "BId", "Building Ids", GH_ParamAccess.list);

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
            double footprintDiscretizationInterval = 1.0;
            double linkDiscretizationInterval = 1.0;
            double offsetDistance = 0.2;
            bool enableAdaptiveNetwork = false;
            // Then we need to access the input parameters individually. 
            // When data cannot be extracted from a parameter, we should abort this method.
            if (!DA.GetDataList(0, buildings)) return;
            if (!DA.GetDataList(1, links)) return;
            if (!DA.GetData(2, ref boundaryRectangle)) return;
            if (!DA.GetData(3, ref footprintDiscretizationInterval)) return;
            if (!DA.GetData(4, ref linkDiscretizationInterval)) return;
            if (!DA.GetData(5, ref offsetDistance)) return;
            if (!DA.GetData(6, ref enableAdaptiveNetwork)) return;


            ModelSettings settings = new ModelSettings()
            {
                OffsetDistance = offsetDistance,
                LinkDiscretizationInterval = linkDiscretizationInterval,
                FootprintDiscretizationInterval = footprintDiscretizationInterval,
                EnableAdaptiveNetwork = enableAdaptiveNetwork,
            };

            Model model = new Model {
                Buildings = buildings,
                Links = links,
                BoundaryRectangle = boundaryRectangle,
                ModelSettings = settings
            };

            model.Preprocessing();
            model.InitializeResults();
            model.Solve();
            model.PostProcessing();
            model.ProcessRemainingTriangles();

            List<Point3d> nodePoints = model.NodePoints;
            List<string> nodeTypes = new List<string>();
            List<int> parentIds = new List<int>();
            List<Curve> triangles = new List<Curve>();
            List<Curve> traditionalVoronoiCells = new List<Curve>();
            List<Curve> guidedVoronoiCells = new List<Curve>();
            List<Curve> traditionalRegions = new List<Curve>();
            List<Curve> guidedRegions = new List<Curve>();
            List<Curve> extendedGuidedRegions = new List<Curve>();
            List<int> buildingIds = model.BuildingIds;

            model.Triangles.ForEach(t => triangles.Add(t.ToNurbsCurve()));
            model.TraditionalVoronoiCells.ForEach(t => traditionalVoronoiCells.Add(t.ToNurbsCurve()));
            model.GuidedVoronoiCells.ForEach(t => guidedVoronoiCells.Add(t.ToNurbsCurve()));
            //model.Triangles.ForEach(t => traditionalRegions.Add(t.ToNurbsCurve()));
            model.GuidedRegions.ForEach(t => guidedRegions.Add(t != null? t.ToNurbsCurve() : null));
            model.ExtendedGuidedRegions.ForEach(t => extendedGuidedRegions.Add(t != null ? t.ToNurbsCurve() : null));


            // assignment of output variables
            DA.SetDataList(0, nodePoints);
            DA.SetDataList(1, nodeTypes);
            DA.SetDataList(2, parentIds);
            DA.SetDataList(3, triangles);
            DA.SetDataList(4, traditionalVoronoiCells);
            DA.SetDataList(5, guidedVoronoiCells);
            DA.SetDataList(6, traditionalRegions);
            DA.SetDataList(7, guidedRegions);
            DA.SetDataList(8, extendedGuidedRegions);
            DA.SetDataList(9, buildingIds);

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
