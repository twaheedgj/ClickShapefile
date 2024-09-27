using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Raster;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClickShapefile
{
    internal class CDUSB : Button
    {
        protected override void OnClick()
        {
            DriveInfo[] drives = DriveInfo.GetDrives();
            string usbdrivepath = null;
            foreach (DriveInfo drive in drives)
            {
                if (drive.DriveType == DriveType.Removable)
                {
                    usbdrivepath = drive.RootDirectory.ToString();
                }
            }
            string proejctfolderPath = Project.Current.HomeFolderPath;
            string foldertocopy = Path.Combine(proejctfolderPath, "Data");
            if (!Directory.Exists(foldertocopy))
            {
                Directory.CreateDirectory(foldertocopy);
            }
            Openusb(usbdrivepath, foldertocopy);
            try
            {
                var filesToAdd = GetRelevantFilesInDataFolder(foldertocopy);
                foreach (string file in filesToAdd)
                {
                    AddDataToMap(file);
                    Debug.WriteLine(file);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
        private void Openusb(string usbPath, string foldertocopy)
        {
            try
            {
                var filesToCopy = Directory.GetFiles(usbPath, "*.*", SearchOption.AllDirectories)
                              .Where(file => file.EndsWith(".gdb") ||
                                                file.EndsWith(".shp") || file.EndsWith(".cpg") || file.EndsWith(".dbf") || file.EndsWith(".shx") || file.EndsWith(".sbn") || file.EndsWith(".sbx") || file.EndsWith(".prg") ||
                                                    file.EndsWith(".tif") || file.EndsWith(".img") || file.EndsWith(".png"));
                foreach (string file in filesToCopy)
                {
                    // Get the file name
                    string fileName = Path.GetFileName(file);

                    // Define the destination path in the project folder
                    string destinationPath = Path.Combine(foldertocopy, fileName);
                    Debug.WriteLine(file);
                    // Copy the file
                    File.Copy(file, destinationPath, overwrite: true);  // Overwrites the file if it already exists
                }
            }
            catch
            {
                Debug.WriteLine($"{usbPath} is null");
            }
        }
        public IEnumerable<string> GetRelevantFilesInDataFolder(string dataFolderPath)
        {
            // Search for shapefiles, geodatabases, and map packages
            var relevantFiles = Directory.GetFiles(dataFolderPath, "*.*", SearchOption.AllDirectories)
                                         .Where(file => file.EndsWith(".shp") ||
                                                        file.EndsWith(".gdb") ||
                                                        file.EndsWith(".tif") ||
                                                        file.EndsWith(".img") ||
                                                        file.EndsWith(".png"));
            return relevantFiles;
        }


        public void AddDataToMap(string filePath)
        {
            // Run tasks in the queued task environment to interact with the ArcGIS Pro UI
            QueuedTask.Run(() =>
            {
                Map map = ArcGIS.Desktop.Mapping.MapView.Active.Map; // Get the current active map

                // Check the file type and add it accordingly
                if (filePath.EndsWith(".shp"))
                {
                    // Add a shapefile to the map
                    Uri shapefileUri = new Uri(filePath);
                    LayerFactory.Instance.CreateLayer(shapefileUri, map);
                }
                else if (filePath.EndsWith(".gdb"))
                {
                    // Add feature classes from a geodatabase to the map
                    using (ArcGIS.Core.Data.Geodatabase gdb = new ArcGIS.Core.Data.Geodatabase(new FileGeodatabaseConnectionPath(new Uri(filePath))))
                    {
                        var featureClasses = gdb.GetDefinitions<FeatureClassDefinition>().ToList();

                        foreach (var featureClassDef in featureClasses)
                        {
                            // Add each feature class as a layer
                            Uri featureClassUri = new Uri(filePath + @"\" + featureClassDef.GetName());
                            LayerFactory.Instance.CreateLayer(featureClassUri, map);
                        }

                        // Add raster datasets from the geodatabase if present
                        var rasterDatasets = gdb.GetDefinitions<RasterDatasetDefinition>().ToList();

                        foreach (var rasterDatasetDef in rasterDatasets)
                        {
                            Uri rasterUri = new Uri(filePath + @"\" + rasterDatasetDef.GetName());
                            LayerFactory.Instance.CreateLayer(rasterUri, map);
                        }
                    }
                }
                else if (filePath.EndsWith(".tif") || filePath.EndsWith(".img") || filePath.EndsWith(".jpg"))
                {
                    // Add raster files (like .tif, .img, .jpg) directly to the map
                    Uri rasterUri = new Uri(filePath);
                    LayerFactory.Instance.CreateLayer(rasterUri, map);
                }
            });
        }
    }
}
