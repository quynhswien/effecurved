using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using effecurved.Services;

namespace effecurved.ViewModels
{
    /// <summary>
    /// ViewModel for the Unroll Curved Surface feature
    /// </summary>
    public partial class UnrollCurvedSurfaceViewModel : ObservableObject, IExternalEventHandler
    {
        private readonly UIDocument _uidoc;
        private readonly Document _doc;
        private readonly ExternalEvent _externalEvent;
        private Action _currentAction;
        private Window _window;

        [ObservableProperty]
        private string _selectedGeometryInfo = "No geometry selected";

        [ObservableProperty]
        private ObservableCollection<ViewDrafting> _draftingViews;

        [ObservableProperty]
        private ViewDrafting _selectedDraftingView;

        [ObservableProperty]
        private string _newViewName = "Unrolled_Surface_01";

        [ObservableProperty]
        private bool _createNewView = true;

        [ObservableProperty]
        private bool _useExistingView = false;

        [ObservableProperty]
        private bool _isProcessing = false;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        private List<Face> _selectedFaces = new List<Face>();
        private List<Curve> _selectedCurves = new List<Curve>();

        public RelayCommand SelectGeometryCommand { get; }
        public RelayCommand ProcessCommand { get; }
        public RelayCommand CancelCommand { get; }

        public UnrollCurvedSurfaceViewModel(UIDocument uidoc)
        {
            _uidoc = uidoc;
            _doc = uidoc.Document;
            _externalEvent = ExternalEvent.Create(this);

            // Initialize commands
            SelectGeometryCommand = new RelayCommand(ExecuteSelectGeometry);
            ProcessCommand = new RelayCommand(ExecuteProcess, CanExecuteProcess);
            CancelCommand = new RelayCommand(ExecuteCancel);

            // Load drafting views
            LoadDraftingViews();
        }

        /// <summary>
        /// Loads all drafting views from the document
        /// </summary>
        private void LoadDraftingViews()
        {
            try
            {
                FilteredElementCollector collector = new FilteredElementCollector(_doc);
                var views = collector
                    .OfClass(typeof(ViewDrafting))
                    .Cast<ViewDrafting>()
                    .Where(v => !v.IsTemplate)
                    .OrderBy(v => v.Name)
                    .ToList();

                DraftingViews = new ObservableCollection<ViewDrafting>(views);

                if (DraftingViews.Any())
                {
                    SelectedDraftingView = DraftingViews.First();
                }

                Log.Debug($"Loaded {DraftingViews.Count} drafting views");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading drafting views");
                StatusMessage = "Error loading drafting views";
            }
        }

        partial void OnCreateNewViewChanged(bool value)
        {
            UseExistingView = !value;
            ProcessCommand.NotifyCanExecuteChanged();
        }

        partial void OnUseExistingViewChanged(bool value)
        {
            CreateNewView = !value;
            ProcessCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Command to select geometry in Revit
        /// </summary>
        private void ExecuteSelectGeometry()
        {
            // Hide window during selection
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _window?.Hide();
            });
            
            _currentAction = SelectGeometryAction;
            _externalEvent.Raise();
        }

        /// <summary>
        /// Action to select geometry (runs in Revit context)
        /// </summary>
        private void SelectGeometryAction()
        {
            try
            {
                Log.Information("Starting geometry selection");
                
                // Try to select faces first
                var faceSelectionResult = SelectFaces();
                
                if (faceSelectionResult.Success)
                {
                    _selectedFaces = faceSelectionResult.Faces;
                    _selectedCurves.Clear();
                    
                    // Update UI on main thread and show window
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        SelectedGeometryInfo = $"{_selectedFaces.Count} face(s) selected";
                        StatusMessage = "Faces selected successfully";
                        ProcessCommand.NotifyCanExecuteChanged();
                        _window?.Show();
                        _window?.Activate();
                    });
                }
                else
                {
                    // Fallback to curve selection
                    var curveSelectionResult = SelectCurves();
                    
                    if (curveSelectionResult.Success)
                    {
                        _selectedCurves = curveSelectionResult.Curves;
                        _selectedFaces.Clear();
                        
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            SelectedGeometryInfo = $"{_selectedCurves.Count} curve(s) selected";
                            StatusMessage = "Curves selected successfully";
                            ProcessCommand.NotifyCanExecuteChanged();
                            _window?.Show();
                            _window?.Activate();
                        });
                    }
                    else
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            SelectedGeometryInfo = "No geometry selected";
                            StatusMessage = "Selection cancelled";
                            ProcessCommand.NotifyCanExecuteChanged();
                            _window?.Show();
                            _window?.Activate();
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error selecting geometry");
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"Error: {ex.Message}";
                    _window?.Show();
                    _window?.Activate();
                });
            }
        }

        /// <summary>
        /// Selects faces from user selection
        /// </summary>
        private (bool Success, List<Face> Faces) SelectFaces()
        {
            try
            {
                Selection selection = _uidoc.Selection;
                var references = selection.PickObjects(ObjectType.Face, "Select faces to unroll (or press ESC to select curves)");
                
                List<Face> faces = new List<Face>();
                foreach (Reference reference in references)
                {
                    Element element = _doc.GetElement(reference.ElementId);
                    GeometryObject geoObj = element.GetGeometryObjectFromReference(reference);
                    
                    if (geoObj is Face face)
                    {
                        faces.Add(face);
                    }
                }
                
                return (faces.Any(), faces);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return (false, new List<Face>());
            }
        }

        /// <summary>
        /// Selects curves from user selection
        /// </summary>
        private (bool Success, List<Curve> Curves) SelectCurves()
        {
            try
            {
                Selection selection = _uidoc.Selection;
                var references = selection.PickObjects(ObjectType.Edge, "Select edges to unroll");
                
                List<Curve> curves = new List<Curve>();
                foreach (Reference reference in references)
                {
                    Element element = _doc.GetElement(reference.ElementId);
                    GeometryObject geoObj = element.GetGeometryObjectFromReference(reference);
                    
                    if (geoObj is Edge edge)
                    {
                        curves.Add(edge.AsCurve());
                    }
                }
                
                return (curves.Any(), curves);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return (false, new List<Curve>());
            }
        }

        /// <summary>
        /// Checks if process command can execute
        /// </summary>
        private bool CanExecuteProcess()
        {
            bool hasGeometry = _selectedFaces.Any() || _selectedCurves.Any();
            bool hasValidView = (CreateNewView && !string.IsNullOrWhiteSpace(NewViewName)) ||
                               (UseExistingView && SelectedDraftingView != null);
            
            return hasGeometry && hasValidView && !IsProcessing;
        }

        /// <summary>
        /// Command to process the unrolling
        /// </summary>
        private void ExecuteProcess()
        {
            _currentAction = ProcessAction;
            _externalEvent.Raise();
        }

        /// <summary>
        /// Action to process unrolling (runs in Revit context)
        /// </summary>
        private void ProcessAction()
        {
            Transaction transaction = null;
            
            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    IsProcessing = true;
                    StatusMessage = "Processing...";
                });

                Log.Information("Starting unroll process");

                transaction = new Transaction(_doc, "Unroll Curved Surface");
                transaction.Start();

                // Get or create drafting view
                ViewDrafting targetView = null;
                if (CreateNewView)
                {
                    targetView = ViewDrafting.Create(_doc, GetFirstViewFamilyType());
                    targetView.Name = GenerateUniqueViewName(NewViewName);
                    Log.Information($"Created new drafting view: {targetView.Name}");
                }
                else
                {
                    targetView = SelectedDraftingView;
                    Log.Information($"Using existing drafting view: {targetView.Name}");
                }

                // Process selected geometry - create separate filled region for each face
                // CRITICAL: insertionPoint must have Z = 0 for drafting view
                XYZ insertionPoint = new XYZ(0, 0, 0);
                int successCount = 0;
                int failCount = 0;

                if (_selectedFaces.Any())
                {
                    foreach (Face face in _selectedFaces)
                    {
                        try
                        {
                            Log.Information($"Processing face {_selectedFaces.IndexOf(face) + 1} of {_selectedFaces.Count}");
                            
                            // Unroll this face (outer + inner loops = openings)
                            var result = GeometryUnrollService.Instance.UnrollFace(face, insertionPoint);
                            
                            if (result?.OuterCurves != null && result.OuterCurves.Count >= 3)
                            {
                                // Bước 1: Luôn vẽ DetailCurves trước để user thấy hình dù FilledRegion có lỗi
                                try
                                {
                                    GeometryUnrollService.Instance.DrawDetailCurvesFromResult(_doc, targetView, result);
                                }
                                catch (Exception ex)
                                {
                                    Log.Warning(ex, "Draw detail curves (continuing)");
                                }
                                // Bước 2: Thử tạo FilledRegion
                                try
                                {
                                    GeometryUnrollService.Instance.CreateFilledRegion(_doc, targetView, result);
                                    Log.Information($"Created filled region for face {_selectedFaces.IndexOf(face) + 1}" +
                                        (result.HasOpenings ? $" ({result.InnerLoops.Count} opening(s) or outer only)" : ""));
                                    successCount++;
                                    insertionPoint = new XYZ(insertionPoint.X + 10, insertionPoint.Y, 0);
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex, $"Failed to create filled region for face {_selectedFaces.IndexOf(face) + 1}");
                                    failCount++;
                                }
                            }
                            else
                            {
                                Log.Warning($"Not enough curves for face {_selectedFaces.IndexOf(face) + 1}: outer={result?.OuterCurves?.Count ?? 0}");
                                failCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, $"Failed to unroll face {_selectedFaces.IndexOf(face) + 1}, continuing with others");
                            failCount++;
                        }
                    }
                }

                Log.Information($"Completed unrolling: {successCount} successful, {failCount} failed");

                transaction.Commit();

                // Set the active view to the drafting view
                _uidoc.ActiveView = targetView;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"Successfully unrolled geometry to {targetView.Name}";
                    IsProcessing = false;
                    
                    // Record statistics
                    StatisticsCollectorService.Instance.RecordFeatureUsage(
                        "UnrollCurvedSurface",
                        1,
                        new Dictionary<string, object>
                        {
                            { "faceCount", _selectedFaces.Count },
                            { "curveCount", _selectedCurves.Count }
                        });
                    
                    // Close the window
                    _window?.Close();
                });
            }
            catch (Exception ex)
            {
                if (transaction != null && transaction.HasStarted())
                {
                    transaction.RollBack();
                }

                Log.Error(ex, "Error processing unroll");
                StatisticsCollectorService.Instance.RecordError("UnrollProcessError", ex.Message, ex.StackTrace);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusMessage = $"Error: {ex.Message}";
                    IsProcessing = false;
                    MessageBox.Show($"Error processing geometry: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        /// <summary>
        /// Gets the first view family type for drafting views
        /// </summary>
        private ElementId GetFirstViewFamilyType()
        {
            FilteredElementCollector collector = new FilteredElementCollector(_doc);
            ViewFamilyType viewFamilyType = collector
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.Drafting);

            if (viewFamilyType == null)
            {
                throw new InvalidOperationException("No drafting view family type found");
            }

            return viewFamilyType.Id;
        }

        /// <summary>
        /// Generates a unique view name
        /// </summary>
        private string GenerateUniqueViewName(string baseName)
        {
            string viewName = baseName;
            int counter = 1;

            while (ViewExists(viewName))
            {
                viewName = $"{baseName}_{counter:D2}";
                counter++;
            }

            return viewName;
        }

        /// <summary>
        /// Checks if a view with the given name exists
        /// </summary>
        private bool ViewExists(string name)
        {
            FilteredElementCollector collector = new FilteredElementCollector(_doc);
            return collector
                .OfClass(typeof(View))
                .Cast<View>()
                .Any(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Command to cancel operation
        /// </summary>
        private void ExecuteCancel()
        {
            _window?.Close();
        }

        /// <summary>
        /// Sets the owner window reference
        /// </summary>
        public void SetWindow(Window window)
        {
            _window = window;
        }

        #region IExternalEventHandler Implementation

        public void Execute(UIApplication app)
        {
            try
            {
                _currentAction?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in external event handler");
            }
        }

        public string GetName()
        {
            return "Unroll Curved Surface Event Handler";
        }

        #endregion
    }
}
