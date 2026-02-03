using System.Windows;
using effecurved.Services;
using effecurved.ViewModels;

namespace effecurved.Views
{
    /// <summary>
    /// Interaction logic for LicenseActivationView.xaml
    /// </summary>
    public partial class LicenseActivationView : Window
    {
        public LicenseActivationViewModel ViewModel { get; }

        public LicenseActivationView()
        {
            InitializeComponent();
            ViewModel = new LicenseActivationViewModel(this);
            DataContext = ViewModel;
        }
    }
}