using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;

namespace BimShady;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class HelloWorldCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            UIApplication uiApp = commandData.Application;
            Document doc = uiApp.ActiveUIDocument?.Document;

            string projectName = doc?.Title ?? "No document open";

            var dialog = new Autodesk.Revit.UI.TaskDialog("BIM Shady");
            dialog.MainInstruction = "Hello World!";
            dialog.MainContent = $"BIM Shady plugin is working.\n\nCurrent project: {projectName}";
            dialog.CommonButtons = TaskDialogCommonButtons.Ok;
            dialog.Show();

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return Result.Failed;
        }
    }
}
