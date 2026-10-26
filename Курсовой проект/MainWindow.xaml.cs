using System.Windows;
using Курсовой_проект.ViewModels;

namespace Курсовой_проект;

public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel;
    private readonly Курсовой_проект.Services.FleetReportService fleetReports;
    private readonly Курсовой_проект.Services.MaintenanceReportService reports;
    public event EventHandler? LogoutRequested;
    public MainWindow(MainViewModel viewModel, CatalogViewModel catalog, Курсовой_проект.Services.MaintenanceReportService reports, Курсовой_проект.Services.FleetReportService fleetReports)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        this.reports = reports;
        this.fleetReports = fleetReports;
        ReportsButton.Visibility = reports.CanRead ? Visibility.Visible : Visibility.Collapsed;
        DataContext = new WorkspaceViewModel(viewModel, catalog);
        Loaded += WindowLoaded;
        Closing += (_, e) => e.Cancel = viewModel.IsBusy;
    }

    private void Reports_Click(object sender, RoutedEventArgs e)
    {
        var menu = new System.Windows.Controls.ContextMenu();
        if (fleetReports.CanRead)
        {
            var fleet = new System.Windows.Controls.MenuItem { Header = "Состояние автопарка" };
            fleet.Click += (_, _) => new Views.FleetReportWindow(fleetReports) { Owner = this }.ShowDialog();
            menu.Items.Add(fleet);
        }
        if (reports.CanRead)
        {
            var maintenance = new System.Windows.Controls.MenuItem { Header = "Обслуживание и расходы" };
            maintenance.Click += (_, _) => new Views.ReportWindow(reports) { Owner = this }.ShowDialog();
            menu.Items.Add(maintenance);
        }
        menu.PlacementTarget = ReportsButton;
        menu.IsOpen = true;
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
