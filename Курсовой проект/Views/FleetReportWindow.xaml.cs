using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Курсовой_проект.Services;
using Microsoft.Win32;

namespace Курсовой_проект.Views;

public partial class FleetReportWindow : Window
{
    private readonly FleetReportService service;
    private FleetReport? report;
    private bool busy;
    private sealed record FilterOption(string? Value, string Label);
    private void Display()
    {
        Rows.ItemsSource = report!.Rows;
        Summary.Text = report.Summary;
        Parameters.Text = report.Parameters;
        Status.Text = report.Rows.Count == 0 ? "Автомобилей с выбранным статусом нет." : "Отчёт готов. После изменения статуса нажмите «Сформировать».";
    }
    public FleetReportWindow(FleetReportService service)
    {
        InitializeComponent();
        this.service = service;
        Closing += (_, e) => e.Cancel = busy;
        Loaded += async (_, _) =>
        {
            Busy(true);
            try
            {
                report = await service.GenerateAsync();
                StatusFilter.ItemsSource = new[] { new FilterOption(null, "Все статусы") }.Concat(
                    report.Rows.Select(x => x.Status).Distinct().OrderBy(x => x).Select(x => new FilterOption(x, x))).ToList();
                StatusFilter.SelectedIndex = 0;
                Display();
            }
            catch { Status.Text = "Не удалось сформировать отчёт. Проверьте подключение и права."; }
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
            Status.Text = "Формирование…";
            report = await service.GenerateAsync((StatusFilter.SelectedItem as FilterOption)?.Value);
            Display();
        }
        catch (ArgumentException ex) { Status.Text = ex.Message; }
        catch (UnauthorizedAccessException) { Status.Text = "Нет доступа к отчёту. Войдите заново."; }
        catch { Status.Text = "Не удалось сформировать отчёт. Проверьте подключение к БД."; }
        finally { Busy(false); }
    }
    private void Export(object sender, RoutedEventArgs e)
    {
        if (report == null) return;
        var dialog = new SaveFileDialog { Filter = "CSV для Excel (*.csv)|*.csv", FileName = $"Автопарк_{report.Created:yyyyMMdd_HHmm}.csv" };
        if (dialog.ShowDialog(this) != true) return;
        try { File.WriteAllText(dialog.FileName, FleetReportService.Csv(report), new UTF8Encoding(true)); Status.Text = "Отчёт сохранён."; }
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
            document.Blocks.Add(new Paragraph(new Run("Состояние автопарка")) { FontSize = 20 });
            document.Blocks.Add(new Paragraph(new Run(Parameters.Text + "\n" + report.Summary)));
            foreach (var row in report.Rows)
                document.Blocks.Add(new Paragraph(new Run($"{row.Vehicle}\n{row.Status} · {row.Year} г. · {row.Mileage} км\n{row.Drivers}")));
            dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, "Состояние автопарка");
        }
        catch { Status.Text = "Не удалось напечатать отчёт. Проверьте принтер или выберите Microsoft Print to PDF."; }
    }
}
