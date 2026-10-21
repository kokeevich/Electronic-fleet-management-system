using System.Windows;
using Курсовой_проект.ViewModels;

namespace Курсовой_проект;

public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel;
    public event EventHandler? LogoutRequested;
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        Loaded += WindowLoaded;
    }

    private async void WindowLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= WindowLoaded;
        await viewModel.LoadAsync();
    }

    private void Logout_Click(object sender, RoutedEventArgs e) =>
        LogoutRequested?.Invoke(this, EventArgs.Empty);
}
