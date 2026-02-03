using effecurved.ViewModels;

namespace effecurved.Views
{
    public sealed partial class effecurvedView
    {
        public effecurvedView(effecurvedViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}