using System.Windows;
using Курсовой_проект.ViewModels;

namespace Курсовой_проект;

public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel;
    public event EventHandler? LogoutRequested;
    public MainWindow(MainViewModel viewModel, CatalogViewModel catalog)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = new WorkspaceViewModel(viewModel, catalog);
        Loaded += WindowLoaded;
        Closing += (_, e) => e.Cancel = viewModel.IsBusy;
    }

    private async void WindowLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= WindowLoaded;
        await viewModel.LoadAsync();
    }

    private void Logout_Click(object sender, RoutedEventArgs e) =>
        LogoutRequested?.Invoke(this, EventArgs.Empty);

    private async Task OpenEditorAsync(bool createNew)
    {
        var editor = viewModel.CreateEditor(createNew);
        if (editor == null) return;
        var window = new VehicleEditorWindow(editor) { Owner = this };
        if (window.ShowDialog() == true)
            await viewModel.LoadAsync();
    }

    private async void AddVehicle_Click(object sender, RoutedEventArgs e) => await OpenEditorAsync(true);
    private async void OpenVehicle_Click(object sender, RoutedEventArgs e) => await OpenEditorAsync(false);
    private async void DeleteVehicle_Click(object sender, RoutedEventArgs e)
    {
        if (!viewModel.CanEditSelection) return;
        var plate = viewModel.SelectedVehicle!.LicensePlate;
        if (MessageBox.Show(this, $"Удалить автомобиль {plate}? Это действие нельзя отменить.",
            "Удаление автомобиля", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes)
            await viewModel.DeleteSelectedAsync();
    }
}
