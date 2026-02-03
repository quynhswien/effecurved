using System.Windows;
using effecurved.ViewModels;

namespace effecurved.Views
{
    /// <summary>
    /// Interaction logic for UnrollCurvedSurfaceView.xaml
    /// </summary>
    public partial class UnrollCurvedSurfaceView : Window
    {
        public UnrollCurvedSurfaceView()
        {
            InitializeComponent();
            
            // Set window reference in ViewModel
            Loaded += (s, e) =>
            {
                if (DataContext is UnrollCurvedSurfaceViewModel viewModel)
                {
                    viewModel.SetWindow(this);
                }
            };
        }
    }
}
