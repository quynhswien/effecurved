using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using effecurved.Services;
using effecurved.Views;

namespace effecurved.Commands
{
    /// <summary>
    /// Command to unroll/flatten curved surfaces into drafting views
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class UnrollCurvedSurfaceCommand : BaseCommand
    {
        public const string COMMAND_NAME = "Unroll Curved Surface";
        public override string CommandName => COMMAND_NAME;

        private static UnrollCurvedSurfaceView _activeWindow;

        protected override Result ExecuteCommand(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uidoc = uiApp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                Log.Information("Starting Unroll Curved Surface command");

                // Check if window is already open
                if (_activeWindow != null)
                {
                    Log.Information("Window already open, activating it");
                    _activeWindow.Activate();
                    _activeWindow.Focus();
                    return Result.Succeeded;
                }

                // Create and show UI
                var viewModel = new ViewModels.UnrollCurvedSurfaceViewModel(uidoc);
                _activeWindow = new UnrollCurvedSurfaceView
                {
                    DataContext = viewModel
                };

                // Handle window closed event to cleanup
                _activeWindow.Closed += (s, e) =>
                {
                    _activeWindow = null;
                    Log.Information("Window closed and reference cleaned up");
                };

                // Show the dialog
                bool? dialogResult = _activeWindow.ShowDialog();
                
                if (dialogResult != true)
                {
                    Log.Information("User cancelled the operation");
                    return Result.Cancelled;
                }

                Log.Information("Unroll Curved Surface command completed successfully");
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                Log.Information("Operation cancelled by user");
                _activeWindow = null;
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in Unroll Curved Surface command");
                message = $"Error: {ex.Message}";
                StatisticsCollectorService.Instance.RecordError("UnrollCurvedSurfaceError", ex.Message, ex.StackTrace);
                _activeWindow = null;
                return Result.Failed;
            }
        }
    }
}
