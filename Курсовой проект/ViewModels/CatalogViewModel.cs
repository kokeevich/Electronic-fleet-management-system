using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Курсовой_проект.Services;

namespace Курсовой_проект.ViewModels;

public sealed class CatalogViewModel : INotifyPropertyChanged
{
    private readonly ICatalogService service;
    private List<CatalogRow> allRows = new();
    private CatalogDefinition? selected;
    private string search = "", status = "Выберите раздел для просмотра.";
    private bool busy;
    private int request;
    public CatalogViewModel(ICatalogService service, AccessService access)
    {
        this.service = service;
        Sections = CatalogDefinition.All.Where(x => access.Can(x.Permission)).ToList();
        selected = Sections.FirstOrDefault();
        RefreshCommand = new AsyncCommand(LoadAsync);
    }
    public IReadOnlyList<CatalogDefinition> Sections { get; }
    public ObservableCollection<CatalogRow> Rows { get; } = new();
    public ICommand RefreshCommand { get; }
    public CatalogDefinition? SelectedSection
    {
        get => selected;
        set
        {
            if (selected == value) return;
            selected = value;
            search = "";
            Changed(); Changed(nameof(Title)); Changed(nameof(Search));
            _ = LoadAsync();
        }
    }
    public string Title => SelectedSection?.Title ?? "Реестры";
    public string Search { get => search; set { search = value; Changed(); Filter(); } }
    public string Status { get => status; private set { status = value; Changed(); } }
    public bool IsBusy { get => busy; private set { busy = value; Changed(); Changed(nameof(CanRefresh)); } }
    public bool CanRefresh => !IsBusy && SelectedSection != null;
    public string CountText => $"Показано: {Rows.Count} из {allRows.Count}";
    public bool HasLoaded { get; private set; }

    public async Task LoadAsync()
    {
        var current = ++request;
        var section = SelectedSection;
        allRows = new List<CatalogRow>(); Rows.Clear(); Changed(nameof(CountText));
        if (section == null) { IsBusy = false; Status = "Нет доступных разделов."; return; }
        IsBusy = true;
        Status = "Загрузка данных…";
        try
        {
            var rows = await service.ReadAsync(section.Section);
            if (current != request) return;
            allRows = new List<CatalogRow>(rows);
            Filter();
            Status = rows.Count == 0 ? "В этом разделе пока нет записей." : $"Загружено записей: {rows.Count}. Только просмотр.";
        }
        catch (UnauthorizedAccessException)
        {
            if (current == request) Status = "Нет доступа к разделу. Войдите с подходящей ролью.";
        }
        catch (Exception)
        {
            if (current == request) Status = "Не удалось загрузить данные. Проверьте подключение и права чтения, затем нажмите «Обновить».";
        }
        finally
        {
            if (current == request) { IsBusy = false; HasLoaded = true; }
        }
    }

    private void Filter()
    {
        Rows.Clear();
        var query = Search.Trim();
        foreach (var row in allRows.Where(x => query.Length == 0 ||
            string.Join(" ", x.Title, x.Subtitle, x.Details, x.Badge).Contains(query, StringComparison.CurrentCultureIgnoreCase)))
            Rows.Add(row);
        Changed(nameof(CountText));
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
