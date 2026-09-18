using SPHCaLArrayRMS.App.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace SPHCaLArrayRMS.App
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _viewModel.Cleanup();
            base.OnClosing(e);
        }
    }
}
