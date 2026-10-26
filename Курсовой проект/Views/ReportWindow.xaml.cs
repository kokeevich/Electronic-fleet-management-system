using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Курсовой_проект.Services;
using Microsoft.Win32;

namespace Курсовой_проект.Views;

public partial class ReportWindow : Window
{
    private readonly MaintenanceReportService service;
    private MaintenanceReport? report;
    private bool busy;
    public ReportWindow(MaintenanceReportService service)
    {
        InitializeComponent();
        this.service = service;
        From.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        To.SelectedDate = DateTime.Today;
        Closing += (_, e) => e.Cancel = busy;
        Loaded += async (_, _) =>
        {
            Busy(true);
            try { Vehicle.ItemsSource = await service.VehiclesAsync(); Vehicle.SelectedIndex = 0; Status.Text = "Выберите параметры и сформируйте отчёт."; }
            catch { Status.Text = "Не удалось загрузить автомобили. Проверьте подключение и права."; }
            finally { Busy(false); }
        };
    }
    private void Busy(bool value) { busy = value; Filters.IsEnabled = !value; Exports.IsEnabled = !value && report != null; }
    private async void Generate(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        report = null; Rows.ItemsSource = null; Summary.Text = ""; Parameters.Text = "";
        Busy(true);
        try
        {
            if (From.SelectedDate == null || To.SelectedDate == null || Vehicle.SelectedItem is not ReportVehicle car)
                throw new ArgumentException("Выберите обе даты и автомобиль.");
            Status.Text = "Формирование…";
            report = await service.GenerateAsync(DateOnly.FromDateTime(From.SelectedDate.Value), DateOnly.FromDateTime(To.SelectedDate.Value), car.Id);
            Rows.ItemsSource = report.Rows; Summary.Text = report.Summary;
            Parameters.Text = $"{report.From:dd.MM.yyyy} — {report.To:dd.MM.yyyy} · {report.Vehicle} · Сформирован {report.Created:dd.MM.yyyy HH:mm}";
            Status.Text = report.Rows.Count == 0 ? "За выбранный период работ нет." : "Отчёт готов. После изменения параметров нажмите «Сформировать».";
        }
        catch (ArgumentException ex) { Status.Text = ex.Message; }
        catch (UnauthorizedAccessException) { Status.Text = "Нет доступа к отчёту. Войдите заново."; }
        catch { Status.Text = "Не удалось сформировать отчёт. Проверьте подключение к БД."; }
        finally { Busy(false); }
    }
    private void Export(object sender, RoutedEventArgs e)
    {
        if (report == null) return;
        var dialog = new SaveFileDialog { Filter = "CSV для Excel (*.csv)|*.csv", FileName = $"Расходы_{report.From:yyyyMMdd}_{report.To:yyyyMMdd}.csv" };
        if (dialog.ShowDialog(this) != true) return;
        try { File.WriteAllText(dialog.FileName, MaintenanceReportService.Csv(report), new UTF8Encoding(true)); Status.Text = "Отчёт сохранён."; }
        catch { Status.Text = "Не удалось сохранить файл. Проверьте папку и закройте файл в Excel."; }
    }
    private void Print(object sender, RoutedEventArgs e)
    {
        if (report == null) return;
        try
        {
            var dialog = new PrintDialog();
            if (dialog.ShowDialog() != true) return;
            var document = new FlowDocument { FontSize = 12, PagePadding = new Thickness(30),
                PageWidth = dialog.PrintableAreaWidth, PageHeight = dialog.PrintableAreaHeight, ColumnWidth = double.PositiveInfinity };
            document.Blocks.Add(new Paragraph(new Run("Обслуживание и расходы")) { FontSize = 20 });
            document.Blocks.Add(new Paragraph(new Run(Parameters.Text + "\n" + report.Summary)));
            foreach (var row in report.Rows)
                document.Blocks.Add(new Paragraph(new Run($"{row.Date:dd.MM.yyyy} · {row.Vehicle}\n{row.Work} · {row.Mileage} км · {row.Cost:N2} ₽\n{row.Parts}\n{row.Description}")));
            document.Blocks.Add(new Paragraph(new Run("Расходы включают общую стоимость работ; запчасти повторно не прибавляются.")));
            dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, "Обслуживание и расходы");
        }
        catch { Status.Text = "Не удалось напечатать отчёт. Проверьте принтер или выберите Microsoft Print to PDF."; }
    }
}
