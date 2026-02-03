using System.Windows;
using effecurved.ViewModels;

namespace effecurved.Views
{
    /// <summary>
    /// Interaction logic for LicenseWarningView.xaml
    /// </summary>
    public partial class LicenseWarningView : Window
    {
        public LicenseWarningViewModel ViewModel { get; }

        public LicenseWarningView(string warningMessage, string licenseRequirement)
        {
            InitializeComponent();
            ViewModel = new LicenseWarningViewModel(this, warningMessage, licenseRequirement);
            DataContext = ViewModel;
        }
    }
}