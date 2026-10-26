using System.Windows;
using Курсовой_проект.ViewModels;

namespace Курсовой_проект;

public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel;
    private readonly Курсовой_проект.Services.NotificationService notifications;
    private readonly System.Windows.Threading.DispatcherTimer notificationTimer = new() { Interval = TimeSpan.FromMinutes(5) };
    private bool checkingNotifications;
    private bool notificationRefreshPending;
    private bool closed;
    private readonly Курсовой_проект.Services.VehicleHistoryService history;
    private readonly Курсовой_проект.Services.FleetReportService fleetReports;
    private readonly Курсовой_проект.Services.MaintenanceReportService reports;
    public event EventHandler? LogoutRequested;
    public MainWindow(MainViewModel viewModel, CatalogViewModel catalog, Курсовой_проект.Services.MaintenanceReportService reports, Курсовой_проект.Services.FleetReportService fleetReports, Курсовой_проект.Services.VehicleHistoryService history, Курсовой_проект.Services.NotificationService notifications)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        this.history = history;
        this.notifications = notifications;
        notificationTimer.Tick += async (_, _) => await RefreshNotifications();
        Курсовой_проект.Services.NotificationService.DataChanged += NotificationDataChanged;
        Closed += (_, _) =>
        {
            closed = true;
            notificationTimer.Stop();
            Курсовой_проект.Services.NotificationService.DataChanged -= NotificationDataChanged;
        };
        this.reports = reports;
        this.fleetReports = fleetReports;
        ReportsButton.Visibility = reports.CanRead ? Visibility.Visible : Visibility.Collapsed;
        DataContext = new WorkspaceViewModel(viewModel, catalog);
        Loaded += WindowLoaded;
        Closing += (_, e) => e.Cancel = viewModel.IsBusy;
    }

    private void NotificationDataChanged() => Dispatcher.BeginInvoke(new Action(async () => await RefreshNotifications()));
    private async Task RefreshNotifications()
    {
        if (closed) return;
        if (checkingNotifications) { notificationRefreshPending = true; return; }
        checkingNotifications = true;
        try
        {
            var rows = await notifications.LoadAsync();
            if (closed) return;
            var count = rows.Count(x => !x.IsRead);
            NotificationCount.Text = count > 99 ? "99+" : count.ToString();
            NotificationBadge.Visibility = count == 0 ? Visibility.Collapsed : Visibility.Visible;
            NotificationsButton.ToolTip = $"Уведомления: новых {count}";
        }
        catch
        {
            if (!closed)
            {
                NotificationBadge.Visibility = Visibility.Visible;
                NotificationCount.Text = "!";
                NotificationsButton.ToolTip = "Не удалось проверить уведомления. Откройте список для повторной проверки.";
            }
        }
        finally
        {
            checkingNotifications = false;
            if (notificationRefreshPending && !closed)
            {
                notificationRefreshPending = false;
                await RefreshNotifications();
            }
        }
    }
    private async void Notifications_Click(object sender, RoutedEventArgs e)
    {
        new Views.NotificationsWindow(notifications, history) { Owner = this }.ShowDialog();
        await RefreshNotifications();
    }

    private void VehicleHistory_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.CanOpenCard && viewModel.SelectedVehicle is { } car)
            new Views.VehicleHistoryWindow(history, car.VehicleId) { Owner = this }.ShowDialog();
    }

    private void Reports_Click(object sender, RoutedEventArgs e)
    {
        var menu = new System.Windows.Controls.ContextMenu();
        var deadlines = new System.Windows.Controls.MenuItem { Header = "Предстоящие и просроченные события" };
        deadlines.Click += Notifications_Click;
        menu.Items.Add(deadlines);
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
        await RefreshNotifications();
        if (!closed) notificationTimer.Start();
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
