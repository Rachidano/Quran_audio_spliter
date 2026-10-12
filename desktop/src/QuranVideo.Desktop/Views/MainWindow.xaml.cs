using System.Windows;
using QuranVideo.Desktop.ViewModels;

namespace QuranVideo.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (s, e) =>
        {
            await viewModel.InitializeSessionAsync();
        };
    }
}
