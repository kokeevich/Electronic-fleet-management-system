using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Microsoft.Win32;
using Курсовой_проект.Services;

namespace Курсовой_проект.Views;

public partial class NotificationsWindow : Window
{
    private readonly NotificationService service;
    private readonly VehicleHistoryService history;
    private List<Notice> notices = new();
    private bool busy;
    private bool loaded;
    private DateTime generated;
    public NotificationsWindow(NotificationService service, VehicleHistoryService history)
    {
        InitializeComponent();
        this.service = service; this.history = history;
        Loaded += Reload;
        Closing += (_, e) => e.Cancel = busy;
    }
    private void Busy(bool value)
    {
        busy = value; Refresh.IsEnabled = Filter.IsEnabled = !value;
        Exports.IsEnabled = !value && loaded;
        UpdateSelection();
    }
    private void UpdateSelection()
    {
        Read.IsEnabled = !busy && Rows.SelectedItem is Notice { IsRead: false };
        Details.IsEnabled = !busy && Rows.SelectedItem is Notice;
        Car.IsEnabled = !busy && Rows.SelectedItem is Notice { VehicleId: not null };
    }
    private async Task Load()
    {
        loaded = false; notices.Clear(); Rows.ItemsSource = null; Busy(true);
        Status.Text = "Проверка сроков…";
        try { notices = await service.LoadAsync(); generated = DateTime.Now; loaded = true; ApplyFilter(); }
        catch (UnauthorizedAccessException) { Status.Text = "Права изменились. Войдите заново."; }
        catch { Status.Text = "Не удалось загрузить уведомления. Проверьте подключение и выполнение notification-reads.sql."; }
        finally { Busy(false); }
    }
    private async void Reload(object sender, RoutedEventArgs e) { if (!busy) await Load(); }
    private List<Notice> Visible() => notices.Where(x => Filter.SelectedIndex switch
    { 1 => !x.IsRead, 2 => x.Overdue, 3 => !x.Overdue, _ => true }).ToList();
    private void ApplyFilter()
    {
        Rows.ItemsSource = Visible();
        Status.Text = $"Показано: {Visible().Count} · Новых: {notices.Count(x => !x.IsRead)} · Проверено {generated:dd.MM.yyyy HH:mm}";
    }
    private void FilterChanged(object sender, SelectionChangedEventArgs e) { if (Rows != null && loaded) ApplyFilter(); }
    private void SelectionChanged(object sender, SelectionChangedEventArgs e) { if (Read != null) UpdateSelection(); }
    private async void MarkRead(object sender, RoutedEventArgs e)
    {
        if (busy || Rows.SelectedItem is not Notice notice) return;
        Busy(true);
        try { await service.MarkReadAsync(notice.Key); await Load(); }
        catch { Status.Text = "Не удалось отметить событие. Обновите список и проверьте подключение."; }
        finally { Busy(false); }
    }
    private void OpenDetails(object sender, RoutedEventArgs e)
    {
        if (Rows.SelectedItem is Notice notice)
            new RecordDetailsWindow(new CatalogRow(notice.Title, notice.Category, notice.Message, notice.Badge))
                { Owner = this, Title = "Подробности уведомления" }.ShowDialog();
    }
    private void DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(Rows, source) is ListViewItem)
            OpenDetails(sender, e);
    }
    private void OpenCar(object sender, RoutedEventArgs e)
    {
        if (Rows.SelectedItem is Notice { VehicleId: { } id }) new VehicleHistoryWindow(history, id) { Owner = this }.ShowDialog();
    }
    private string ReportTitle => $"Предстоящие и просроченные события · {generated:dd.MM.yyyy HH:mm}\n{(Filter.SelectedItem as ComboBoxItem)?.Content} · Записей: {Visible().Count}";
    private void Export(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "CSV для Excel (*.csv)|*.csv", FileName = $"Сроки_{generated:yyyyMMdd_HHmm}.csv" };
        if (dialog.ShowDialog(this) != true) return;
        try { File.WriteAllText(dialog.FileName, "\"" + ReportTitle.Replace("\"", "\"\"") + "\"\r\n" + NotificationService.Csv(Visible()), new UTF8Encoding(true)); Status.Text = "Отчёт сохранён."; }
        catch { Status.Text = "Не удалось сохранить файл. Проверьте папку и закройте файл в Excel."; }
    }
    private void Print(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new PrintDialog();
            if (dialog.ShowDialog() != true) return;
            var document = new FlowDocument { FontSize = 12, PagePadding = new Thickness(30),
                PageWidth = dialog.PrintableAreaWidth, PageHeight = dialog.PrintableAreaHeight, ColumnWidth = double.PositiveInfinity };
            document.Blocks.Add(new Paragraph(new Run(ReportTitle)) { FontSize = 18 });
            foreach (var notice in Visible())
                document.Blocks.Add(new Paragraph(new Run($"{notice.Title}\n{notice.Badge} · {notice.Message}")));
            dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, "Предстоящие и просроченные события");
        }
        catch { Status.Text = "Не удалось напечатать отчёт. Проверьте выбранный принтер."; }
    }
}
