using System.Windows;
using Курсовой_проект.Services;

namespace Курсовой_проект.Views;

public partial class VehicleHistoryWindow : Window
{
    private readonly VehicleHistoryService service;
    private readonly int vehicleId;
    private bool busy;
    public VehicleHistoryWindow(VehicleHistoryService service, int vehicleId)
    {
        InitializeComponent();
        this.service = service;
        this.vehicleId = vehicleId;
        Loaded += Reload;
        Closing += (_, e) => e.Cancel = busy;
    }
    private async void Reload(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        busy = true; Refresh.IsEnabled = false;
        Assignments.ItemsSource = null; Documents.ItemsSource = null; Maintenance.ItemsSource = null;
        Summary.Text = ""; Status.Text = "Загрузка истории…";
        AssignmentsTab.Header = "Закрепления"; DocumentsTab.Header = "Документы"; MaintenanceTab.Header = "ТО и ремонты";
        try
        {
            var result = await service.LoadAsync(vehicleId);
            Heading.Text = result.Title;
            Summary.Text = result.Summary;
            Assignments.ItemsSource = result.Assignments; Documents.ItemsSource = result.Documents; Maintenance.ItemsSource = result.Maintenance;
            AssignmentsTab.Header = $"Закрепления ({result.Assignments.Count})";
            DocumentsTab.Header = $"Документы ({result.Documents.Count})";
            MaintenanceTab.Header = $"ТО и ремонты ({result.Maintenance.Count})";
            Status.Text = "Новые записи показаны первыми. Число 0 во вкладке означает отсутствие записей.";
        }
        catch (UnauthorizedAccessException) { Status.Text = "Права изменились. Войдите заново."; }
        catch (InvalidOperationException) { Status.Text = "Не удалось открыть историю. Обновите список транспорта и проверьте подключение."; }
        catch { Status.Text = "Не удалось загрузить историю. Проверьте подключение к БД."; }
        finally { busy = false; Refresh.IsEnabled = true; }
    }
}
