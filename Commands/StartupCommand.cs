using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using effecurved.Services;
using effecurved.ViewModels;
using effecurved.Views;

namespace effecurved.Commands
{
    /// <summary>
    ///     External command entry point invoked from the Revit interface
    /// </summary>
    [UsedImplicitly]
    [Transaction(TransactionMode.Manual)]
    public class StartupCommand : BaseCommand
    {
        public static readonly string COMMAND_NAME = "effecurved";
        /// <summary>
        /// Command name
        /// </summary>
        public override string CommandName => COMMAND_NAME;

        protected override Result ExecuteCommand(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Record feature usage
                StatisticsCollectorService.Instance.RecordFeatureUsage(COMMAND_NAME + "Command");

                var viewModel = new effecurvedViewModel();
                var view = new effecurvedView(viewModel);
                view.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in StartupCommand");
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}