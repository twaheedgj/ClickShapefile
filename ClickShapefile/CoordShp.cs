using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Geometry;
using ArcGIS.Core.Internal.CIM;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Shapes;
using FieldDescription = ArcGIS.Core.Data.DDL.FieldDescription;

namespace ClickShapefile
{
    internal class CoordShp : MapTool
    {
        protected override void OnToolMouseDown(MapViewMouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                e.Handled = true; // Handle the event to trigger async method
            }
        }
        protected override Task HandleMouseDownAsync(MapViewMouseButtonEventArgs e)
        {
            return QueuedTask.Run(async () =>
            {
                // Convert the clicked point in client coordinates to map coordinates
                var mapPoint = MapView.Active.ClientToMap(e.ClientPoint);
                var projGDBPath = Project.Current.DefaultGeodatabasePath;
                //Get the Name and Refraance Name by using python tool
                string installPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string toolboxPath = System.IO.Path.Combine(installPath, "UserInput.pyt\\Tool");
                var result = await Geoprocessing.OpenToolDialogAsync(toolboxPath);
                string NameOutput = result.ReturnValue;
                string Name = NameOutput.Split(":")[0];
                string RefrenceName = NameOutput.Split(":")[1];
                //ArcGIS.Core.Internal.CIM.SpatialReference mySpatialReference = MapView.Active.Map.SpatialReference;
                FileGeodatabaseConnectionPath geodatabaseConnectionPath = new FileGeodatabaseConnectionPath(new Uri(projGDBPath));
                Geodatabase geodatabase = new Geodatabase(geodatabaseConnectionPath);
                GetSHP(geodatabase, mapPoint, Name, RefrenceName);
            });
        }
        public async void GetSHP(Geodatabase geodatabase, MapPoint mapPoint,string Name, string RefName)
        {
            // This static helper routine creates a FieldDescription for a GlobalID field with default values
            FieldDescription globalIDFieldDescription = FieldDescription.CreateGlobalIDField();
            // This static helper routine creates a FieldDescription for an ObjectID field with default values
            FieldDescription objectIDFieldDescription = FieldDescription.CreateObjectIDField();
            // This static helper routine creates a FieldDescription for a Cooediantes field with default values
            FieldDescription Xcoordinate = FieldDescription.CreateStringField("XCoordinate", 255);
            FieldDescription Ycoordinate = FieldDescription.CreateStringField("YCoordinate", 255);
            FieldDescription Zcoordinate = FieldDescription.CreateStringField("ZCoordinate", 255);
            // This static helper routine creates a FieldDescription for a string field
            FieldDescription nameFieldDescription = FieldDescription.CreateStringField("NAME", 255);
            // This static helper routine creates a FieldDescription for an integer field
            FieldDescription RefrenceNameFieldDescription = FieldDescription.CreateStringField("RefranceName", 255);
            List<FieldDescription> fieldDescriptions = new List<FieldDescription>()
            { globalIDFieldDescription, objectIDFieldDescription, nameFieldDescription,
                Xcoordinate,Ycoordinate,Zcoordinate, RefrenceNameFieldDescription };
            // Create a ShapeDescription object
            ShapeDescription shapeDescription = new ShapeDescription(mapPoint.GeometryType, MapView.Active.Map.SpatialReference);
            var FCName = $@"{Name}_{RefName}";
            FeatureClassDescription featureClassDescription =
            new FeatureClassDescription(FCName, fieldDescriptions, shapeDescription);
            // Create a SchemaBuilder object
            SchemaBuilder schemaBuilder = new SchemaBuilder(geodatabase);
            // Add the creation of the Cities feature class to our list of DDL tasks
            schemaBuilder.Create(featureClassDescription);
            // Execute the DDL
            bool success = schemaBuilder.Build();
            // Inspect error messages
            if (!success)
            {
                IReadOnlyList<string> errorMessages = schemaBuilder.ErrorMessages;
            }
            // Populating Feature Class table with Values
            using (FeatureClass featureClass = geodatabase.OpenDataset<FeatureClass>(FCName.ToUpper()))
            {
                RowBuffer rowBuffer = null;
                Feature feature = null;
                try
                {
                    EditOperation editOperation = new EditOperation();
                    editOperation.Callback(context =>
                    {
                        FeatureClassDefinition featureClassDefinition = featureClass.GetDefinition();
                        int nameIndex = featureClassDefinition.FindField("NAME");
                        rowBuffer = featureClass.CreateRowBuffer();
                        rowBuffer[nameIndex] = Name;
                        rowBuffer["RefranceName"] = RefName;
                        rowBuffer["XCoordinate"] = mapPoint.Coordinate2D.X;
                        rowBuffer["YCoordinate"] = mapPoint.Coordinate2D.Y;
                        rowBuffer["ZCoordinate"] = mapPoint.Coordinate3D.Z;
                        rowBuffer[featureClassDefinition.GetShapeField()] = new MapPointBuilderEx(mapPoint.Coordinate2D).ToGeometry();
                        feature = featureClass.CreateRow(rowBuffer);
                        context.Invalidate(feature);
                    }, featureClass);
                    bool editResult = editOperation.Execute();
                    long objectID = feature.GetObjectID();
                    Guid globalID = feature.GetGlobalID();
                    MapPoint mapP = feature.GetShape() as MapPoint;
                    _ = Project.Current.SaveEditsAsync();
                }
                catch (ArcGIS.Core.Data.Exceptions.GeodatabaseException exObj)
                {
                    Console.WriteLine(exObj.Message);
                    throw;
                }
                finally
                {
                    if (rowBuffer != null)
                    {
                        rowBuffer.Dispose();
                    }
                    if (feature != null)
                    {
                        feature.Dispose();
                    }

                }
                // Calling Arcgis pro conversion Tool to convert feature class to Shape at predefined Location 
                var environment = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);
                string tool_name = "conversion.FeatureClassToShapefile";
                string output_dir = "C:\\Shapefiels\\";
                if (!Directory.Exists(output_dir))
                {
                    Directory.CreateDirectory(output_dir);
                }
                var toolParameters = Geoprocessing.MakeValueArray(featureClass, output_dir);
                GPExecuteToolFlags executeFlags = GPExecuteToolFlags.AddOutputsToMap | GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddToHistory | GPExecuteToolFlags.RefreshProjectItems;
                IGPResult gpResult = await Geoprocessing.ExecuteToolAsync(tool_name, toolParameters, environment, null, null, executeFlags);
                Geoprocessing.ShowMessageBox(gpResult.Messages, "GP Messages", gpResult.IsFailed ? GPMessageBoxStyle.Error : GPMessageBoxStyle.Default);
            }
            schemaBuilder.Delete(featureClassDescription);
            bool isFCdeleted = schemaBuilder.Build();
        }
    }
}
