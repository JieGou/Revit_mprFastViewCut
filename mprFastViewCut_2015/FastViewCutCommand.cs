namespace mprFastViewCut
{
    using System;
    using System.Collections.Generic;
    using Autodesk.Revit.Attributes;
    using Autodesk.Revit.DB;
    using Autodesk.Revit.UI;
    using Autodesk.Revit.UI.Selection;
    using ModPlusAPI;
    using ModPlusAPI.Windows;

    /// <inheritdoc />
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class FastViewCutCommand : IExternalCommand
    {
        private static string Li => new ModPlusConnector().Name;

        /// <inheritdoc />
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                Statistic.SendCommandStarting(new ModPlusConnector());

                var activeView = commandData.Application.ActiveUIDocument.Document.ActiveView;
                if (activeView.IsTemplate)
                {
                    // Работа в шаблоне вида невозможна
                    MessageBox.Show(GetLocalValue("h1"));
                    return Result.Cancelled;
                }

                if (activeView.ViewType == ViewType.Legend)
                {
                    // Работа в легендах невозможна
                    MessageBox.Show(GetLocalValue("h2"));
                    return Result.Cancelled;
                }

                if (activeView.ViewType == ViewType.Schedule)
                {
                    // Работа в спецификациях невозможна
                    MessageBox.Show(GetLocalValue("h3"));
                    return Result.Cancelled;
                }

                if (activeView.ViewType == ViewType.DraftingView)
                {
                    // Работа в чертежных видах невозможна
                    MessageBox.Show(GetLocalValue("h4"));
                    return Result.Cancelled;
                }

                CutView(commandData.Application);

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception exception)
            {
                ExceptionBox.Show(exception);
                return Result.Failed;
            }
        }

        private void CutView(UIApplication uiApplication)
        {
            var trName = Language.GetFunctionLocalName(new ModPlusConnector().Name, new ModPlusConnector().LName);
            var uiDoc = uiApplication.ActiveUIDocument;
            var doc = uiDoc.Document;
            var selection = uiDoc.Selection;

            // Укажите прямоугольную область для создания границ подрезки
            var pickedBox = selection.PickBox(PickBoxStyle.Crossing, GetLocalValue("h5"));

            if (Math.Abs(pickedBox.Min.DistanceTo(pickedBox.Max)) < 0.001)
                return;

            var view = doc.ActiveView;

            if (view is View3D view3D)
            {
                // from: https://thebuildingcoder.typepad.com/blog/2009/12/crop-3d-view-to-room.html

                var bb = view3D.CropBox;
                var transform = bb.Transform;
                var transformInverse = transform.Inverse;

                var pt1 = transformInverse.OfPoint(pickedBox.Min);
                var pt2 = transformInverse.OfPoint(pickedBox.Max);

                var xMin = GetSmaller(pt1.X, pt2.X);
                var xMax = GetBigger(pt1.X, pt2.X);
                var yMin = GetSmaller(pt1.Y, pt2.Y);
                var yMax = GetBigger(pt1.Y, pt2.Y);

                bb.Max = new XYZ(xMax, yMax, bb.Max.Z);
                bb.Min = new XYZ(xMin, yMin, bb.Min.Z);

                using (var tr = new Transaction(doc, trName))
                {
                    tr.Start();
                    if (!view.CropBoxActive)
                    {
                        view.CropBoxActive = true;
                        view.CropBoxVisible = false;
                    }

                    view3D.CropBox = bb;
                    tr.Commit();
                }
            }
            else
            {
                var cropRegionShapeManager = view.GetCropRegionShapeManager();
                
                var pt1 = pickedBox.Min;
                var pt3 = pickedBox.Max;
                var plane = CreatePlane(view.UpDirection, pt3);
                var pt2 = plane.ProjectOnto(pt1);
                plane = CreatePlane(view.UpDirection, pt1);
                var pt4 = plane.ProjectOnto(pt3);

                var line1 = Line.CreateBound(pt1, pt2);
                var line2 = Line.CreateBound(pt2, pt3);
                var line3 = Line.CreateBound(pt3, pt4);
                var line4 = Line.CreateBound(pt4, pt1);

                var curveLoop = CurveLoop.Create(new List<Curve>
                {
                    line1, line2, line3, line4
                });

                if (curveLoop.IsRectangular(CreatePlane(view.ViewDirection, view.Origin)))
                {
                    using (var tr = new Transaction(doc, trName))
                    {
                        tr.Start();
                        if (!view.CropBoxActive)
                        {
                            view.CropBoxActive = true;
                            view.CropBoxVisible = false;
                        }
#if R2015
                        cropRegionShapeManager.SetCropRegionShape(curveLoop);
#else
                        cropRegionShapeManager.SetCropShape(curveLoop);
#endif
                        tr.Commit();
                    }
                }
                else
                {
                    // Не удалось получить валидную прямоугольную область. Попробуйте еще раз
                    MessageBox.Show(GetLocalValue("h6"));
                }
            }
        }

        private static Plane CreatePlane(XYZ vectorNormal, XYZ origin)
        {
#if R2015 || R2016
            return new Plane(vectorNormal, origin);
#else
            return Plane.CreateByNormalAndOrigin(vectorNormal, origin);
#endif
        }

        private static double GetBigger(double d1, double d2)
        {
            return d1 > d2 ? d1 : d2;
        }

        private static double GetSmaller(double d1, double d2)
        {
            return d1 < d2 ? d1 : d2;
        }

        private static string GetLocalValue(string key)
        {
            return Language.GetItem(Li, key);
        }
    }
}
