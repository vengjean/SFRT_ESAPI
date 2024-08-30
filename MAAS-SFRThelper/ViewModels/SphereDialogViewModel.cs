﻿using MAAS_SFRThelper.Models;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;


namespace MAAS_SFRThelper.ViewModels
{
    public class SphereDialogViewModel : BindableBase
    {
        private string output;
        public string Output
        {
            get { return output; }
            set { SetProperty(ref output, value); }
        }

        private bool createIndividual;
        public bool CreateIndividual
        {
            get { return createIndividual; }
            set { SetProperty(ref createIndividual, value); }
        }

        private bool isHex;
        public bool IsHex
        {
            get { return isHex; }
            set { SetProperty(ref isHex, value); }
        }

        private bool isRect;
        public bool IsRect
        {
            get { return isRect; }
            set { SetProperty(ref isRect, value); }
        }

        private double xShift;
        public double XShift
        {
            get { return xShift; }
            set { SetProperty(ref xShift, value); }
        }

        private double yShift;
        public double YShift
        {
            get { return yShift; }
            set { SetProperty(ref yShift, value); }
        }

        private float radius;
        public float Radius
        {
            get { return radius; }
            set { SetProperty(ref radius, value); }
        }

        private List<string> targetStructures;
        public List<string> TargetStructures
        {
            get { return targetStructures; }
            set { SetProperty(ref targetStructures, value); }
        }

        private int targetSelected;
        public int TargetSelected
        {
            get { return targetSelected; }
            set { SetProperty(ref targetSelected, value); }
        }

        private float vThresh;
        public float VThresh
        {
            get { return vThresh; }
            set { SetProperty(ref vThresh, value); }
        }

        private List<Spacing> validSpacings;
        public List<Spacing> ValidSpacings
        {
            get { return validSpacings; }
            set { SetProperty(ref validSpacings, value); }
        }

        private Spacing spacingSelected;
        public Spacing SpacingSelected
        {
            get { return spacingSelected; }
            set { SetProperty(ref spacingSelected, value); }
        }

        private readonly ScriptContext scriptContext;

        public SphereDialogViewModel(ScriptContext context)
        {
            // ctor
            scriptContext = context;

            // Set UI value defaults
            VThresh = 0;
            IsHex = true; // default to hex
            createIndividual = true; // default to keeping individual structures
            XShift = 0;
            YShift = 0;
            Output = "Welcome to the SFRT-Helper";


            // Set valid spacings based on CT img z resolution
            ValidSpacings = new List<Spacing>();
            var spacing = context.Image.ZRes;
            for (int i = 1; i < 10; i++)
            {
                ValidSpacings.Add(new Spacing(spacing * i * 10));
            }

            // Default to first value
            SpacingSelected = ValidSpacings.FirstOrDefault();

            // Target structures
            targetStructures = new List<string>();
            targetSelected = -1;
            string planTargetId = null;

            foreach (var i in context.StructureSet.Structures)
            {
                if (i.DicomType != "PTV") continue;
                targetStructures.Add(i.Id);
                if (planTargetId == null) continue;
                if (i.Id == planTargetId) targetSelected = targetStructures.Count() - 1;
            }
        }

        private void AddContoursToMain(ref Structure PrimaryStructure, ref Structure SecondaryStructure)
        {
            // Loop through each image plane
            // { foreach (var segment in contours) { lowResSSource.AddContourOnImagePlane(segment, j); } }
            for (int z = 0; z < scriptContext.Image.ZSize; ++z)
            {
                var contours = SecondaryStructure.GetContoursOnImagePlane(z);
                foreach (var seg in contours)
                {
                    PrimaryStructure.AddContourOnImagePlane(seg, z);
                }
            }
        }

        private void BuildSphere(Structure parentStruct, VVector center, float r)
        {
            double z_center = center.z;
            double min_z = z_center - r;
            // Find the closest slice number to the minimum z value
            int min_z_idx = (int)Math.Floor((min_z - scriptContext.Image.Origin.z) / scriptContext.Image.ZRes);
            // Find the closest slice number to the maximum z value
            int max_z_idx = (int)Math.Ceiling((z_center + r - scriptContext.Image.Origin.z) / scriptContext.Image.ZRes);

            // Make sure they are above 0 and below the max number of slices
            min_z_idx = Math.Max(min_z_idx, 0);
            max_z_idx = Math.Min(max_z_idx, scriptContext.Image.ZSize);

            for (int z = min_z_idx; z < max_z_idx; ++z)
            {
                double zCoord = z * (scriptContext.Image.ZRes) + scriptContext.Image.Origin.z;

                // For each slice find in plane radius
                var z_diff = Math.Abs(zCoord - center.z);
                if (z_diff > r) // If we are out of range of the sphere continue
                {
                    continue;
                }

                // Otherwise do the thing (make spheres)
                var r_z = Math.Sqrt(Math.Pow(r, 2.0) - Math.Pow(z_diff, 2.0));
                var contour = CreateContour(center, r_z, 50);
                parentStruct.AddContourOnImagePlane(contour, z);
            }
        }

        private List<double> Arange(double start, double stop, double step)
        {
            //log.Debug($"Arange with start stop step = {start} {stop} {step}\n");
            var retval = new List<double>();
            var currentval = start;
            while (currentval < stop)
            {
                retval.Add(currentval);
                currentval += step;
            }
            return retval;
        }

        private List<VVector> BuildGrid(List<double> xcoords, List<double> ycoords, List<double> zcoords)
        {
            var retval = new List<VVector>();
            foreach (var x in xcoords)
            {
                foreach (var y in ycoords)
                {
                    foreach (var z in zcoords)
                    {
                        var pt = new VVector(x, y, z);

                        retval.Add(pt);
                    }
                }
            }

            return retval;
        }

        private List<VVector> BuildHexGrid(double Xstart, double Xsize, double Ystart, double Ysize, double Zstart, double Zsize)
        {
            double A = SpacingSelected.Value;
            var retval = new List<VVector>();

            void CreateLayer(double zCoord, double x0, double y0)
            {
                // create planar hexagonal sphere packing grid
                var yeven = Arange(y0, y0 + Ysize, 2.0 * A);
                var xeven = Arange(x0, x0 + Xsize, 2.0 * A);
                foreach (var y in yeven)
                {
                    foreach (var x in xeven)
                    {
                        retval.Add(new VVector(x, y, zCoord));
                        retval.Add(new VVector(x + (A / 2.0), y + (A / 2.0), zCoord));
                    }
                }
            }

            foreach (var z in Arange(Zstart, Zstart + Zsize, 2.0 * A))
            {
                CreateLayer(z, Xstart, Ystart);
                CreateLayer(z + A, Xstart + (A / 2.0), Ystart + (A / 2.0));

            }

            return retval;
        }

        private bool PreSpheres()
        {
            // Check if we are ready to make spheres
            if (!IsHex && !IsRect)
            {
                var msg = "No pattern selected. Returning.";
                Output += "\n" + msg;
                Thread.Sleep(100);
                //MessageBox.Show(msg);
                return false;
            }

            // Check vol thresh for spheres
            if (VThresh > 100 || VThresh < 0)
            {
                MessageBox.Show("Volume threshold must be between 0 and 100");
                return false;
            }

            // Check target
            if (targetSelected == -1)
            {
                MessageBox.Show("Must have target selected, canceling operation.");
                return false;
            }

            if (Radius <= 0)
            {
                MessageBox.Show("Radius must be greater than zero.");
                return false;
            }

            if (SpacingSelected.Value < Radius * 2)
            {
                var buttons = MessageBoxButton.OKCancel;
                var result = MessageBox.Show($"WARNING: Sphere center spacing is less than sphere diameter ({Radius * 2}) mm.\n Continue?", "", buttons);
                return result == MessageBoxResult.OK;
            }

            return true;
        }

        public void BuildSpheres(bool makeIndividual, bool alignGrid)
        {

            if (!PreSpheres())
            {
                return;
            }

            // Total lattice structure with all spheres
            Structure structMain = null;
            Structure structMain_cold = null;

            var target_name = targetStructures[targetSelected];
            var target_initial = scriptContext.StructureSet.Structures.Where(x => x.Id == target_name).First();
            Structure target = null;
            bool deleteAutoTarget = false;

            if (!target_initial.IsHighResolution)
            {
                target = scriptContext.StructureSet.AddStructure("PTV", "AutoTarget");
                AddContoursToMain(ref target, ref target_initial);
                target.ConvertToHighResolution();
                deleteAutoTarget = true;
                MessageBox.Show("Created HiRes target.");
            }
            else
            {
                target = target_initial;
            }

            if (target == null)
            {
                //MessageBox.Show($"Could not find target with Id: {target_name}");
                return;
            }

            // Generate a regular grid accross the dummie bounding box 
            var bounds = target.MeshGeometry.Bounds;

            // If alignGrid calculate z to snap to
            double z0 = bounds.Z;
            double zf = bounds.Z + bounds.SizeZ;
            if (alignGrid)
            {
                // Snap z to nearest z slice
                // where z slices = img.origin.z + (c * zres)
                // x, y, z --> dropdown all equal
                // z0 --> rounded to nearest grid slice
                var zSlices = new List<double>();
                var plane_idx = (bounds.Z - scriptContext.Image.Origin.z) / scriptContext.Image.ZRes;
                int plane_int = (int)Math.Round(plane_idx);

                z0 = scriptContext.Image.Origin.z + (plane_int * scriptContext.Image.ZRes);
                //MessageBox.Show($"Original z | Snapped z = {bounds.Z} | {Math.Round(z0, 2)}");
                Output += $"\nOriginal z | Snapped z = {Math.Round(bounds.Z, 2)} | {Math.Round(z0, 2)}";
                Thread.Sleep(100);
            }

            // Get points that are not in the image
            List<VVector> grid = null;
            List<VVector> cold_grid = null;

            if (IsHex)
            {
                grid = BuildHexGrid(bounds.X + XShift, bounds.SizeX, bounds.Y + YShift, bounds.SizeY, z0, bounds.SizeZ);
                structMain = CreateStructure("Lattice_Hot", true, true);
                cold_grid = BuildHexGrid(bounds.X + XShift + SpacingSelected.Value / 2, bounds.SizeX, bounds.Y + YShift, bounds.SizeY, z0, bounds.SizeZ);
                structMain_cold = CreateStructure("Lattice_Cold", true, true);
            }
            else if (IsRect)
            {
                var xcoords = Arange(bounds.X + XShift, bounds.X + bounds.SizeX + XShift, SpacingSelected.Value);
                var ycoords = Arange(bounds.Y + XShift, bounds.Y + bounds.SizeY + YShift, SpacingSelected.Value);
                var zcoords = Arange(z0, zf, SpacingSelected.Value);

                grid = BuildGrid(xcoords, ycoords, zcoords);
                structMain = CreateStructure("LatticeRect", true, true);
            }

            // 4. Make spheres
            int sphere_count = 0;

            var prevSpheres = scriptContext.StructureSet.Structures.Where(x => x.Id.Contains("Sphere")).ToList();
            int deleted_spheres = 0;
            foreach (var sp in prevSpheres)
            {
                scriptContext.StructureSet.RemoveStructure(sp);
                deleted_spheres++;
            }
            if (deleted_spheres > 0) { MessageBox.Show($"{deleted_spheres} pre-existing spheres deleted "); }


            // // Hold on to single sphere ids
            // var singleIds = new List<string>();
            // var singleVols = new List<double>();

            // Calculate full sphere volume
            var sphere_volume = (4.0 / 3.0) * Math.PI * Math.Pow(Radius, 3.0);
            var volThresh = sphere_volume * (VThresh / 100);


            // Starting message
            Output += "\nCreating spheres, this could take several minutes ...";
            MessageBox.Show("About to create spheres.");

            // Create all individual spheres
            foreach (VVector ctr in grid)
            {
                if (makeIndividual)
                {
                    // Create a new structure and build sphere on that
                    var singleId = $"Sphere_{sphere_count}";
                    var singleSphere = CreateStructure(singleId, false, true);
                    BuildSphere(singleSphere, ctr, Radius);

                    // Crop to target
                    singleSphere.SegmentVolume = singleSphere.SegmentVolume.And(target);

                    sphere_count++;
                    if (singleSphere.Volume > volThresh)
                    {
                        // Add to main structure
                        AddContoursToMain(ref structMain, ref singleSphere);
                    }

                    // Delete single sphere
                    scriptContext.StructureSet.RemoveStructure(singleSphere);
                }
            }

            foreach (VVector ctr in cold_grid)
            {
                if (makeIndividual)
                {
                    // Create a new structure and build sphere on that
                    var singleId = $"Sphere_{sphere_count}";
                    var singleSphere = CreateStructure(singleId, false, true);
                    BuildSphere(singleSphere, ctr, Radius);

                    // Crop to target
                    singleSphere.SegmentVolume = singleSphere.SegmentVolume.And(target);

                    sphere_count++;
                    if (singleSphere.Volume > volThresh)
                    {
                        // Add to main structure
                        AddContoursToMain(ref structMain_cold, ref singleSphere);
                    }

                    // Delete single sphere
                    scriptContext.StructureSet.RemoveStructure(singleSphere);
                }
            }

            // Delete the autogenerated target if it exists
            if (deleteAutoTarget)
            {
                scriptContext.StructureSet.RemoveStructure(target);
            }

            // And the main structure with target
            Output += "\nCreated spheres";
            MessageBox.Show("Created Spheres");

        }

        private VVector[] CreateContour(VVector center, double radius, int nOfPoints)
        {
            VVector[] contour = new VVector[nOfPoints + 1];
            double angleIncrement = Math.PI * 2.0 / Convert.ToDouble(nOfPoints);
            for (int i = 0; i < nOfPoints; ++i)
            {
                double angle = Convert.ToDouble(i) * angleIncrement;
                double xDelta = radius * Math.Cos(angle);
                double yDelta = radius * Math.Sin(angle);
                VVector delta = new VVector(xDelta, yDelta, 0.0);
                contour[i] = center + delta;
            }
            contour[nOfPoints] = contour[0];

            return contour;
        }

        private Structure CreateStructure(string structName, bool showMessage, bool makeHiRes)
        {
            string msg = $"New structure ({structName}) created.";
            var prevStruct = scriptContext.StructureSet.Structures.FirstOrDefault(x => x.Id == structName);
            if (prevStruct != null)
            {
                scriptContext.StructureSet.RemoveStructure(prevStruct);
                msg += " Old structure overwritten.";
            }

            var structure = scriptContext.StructureSet.AddStructure("PTV", structName);
            if (makeHiRes)
            {
                structure.ConvertToHighResolution();
                msg += " Converted to Hi-Res";
            }

            if (showMessage) { MessageBox.Show(msg); }
            return structure;
        }

        public void CreateLattice()
        {
            scriptContext.Patient.BeginModifications();
            BuildSpheres(true, true);
        }
    }
}
