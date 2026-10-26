using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Курсовой_проект.Services;

namespace Курсовой_проект.ViewModels;

public sealed class CatalogViewModel : INotifyPropertyChanged
{
    private readonly ICatalogService service;
    private readonly AccessService access;
    private readonly ICatalogEditService? editor;
    private CatalogRow? selectedRow;
    private List<CatalogRow> allRows = new();
    private CatalogDefinition? selected;
    private string search = "", status = "Выберите раздел для просмотра.";
    private bool busy;
    private int request;
    public CatalogViewModel(ICatalogService service, AccessService access, ICatalogEditService? editor = null)
    {
        this.service = service;
        this.access = access;
        this.editor = editor;
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
            Changed(); Changed(nameof(Title)); Changed(nameof(IsAuditLog)); Changed(nameof(ShowResetPassword)); Changed(nameof(Search)); Changed(nameof(CanManageSection)); Changed(nameof(CanCreateRecord)); Changed(nameof(CanDeleteRecord)); Changed(nameof(CanCreateDelete));
            _ = LoadAsync();
        }
    }
    public bool ShowResetPassword => SelectedSection?.Section == CatalogSection.Users && access.Can(Permission.ManageUsers);
    public CatalogEditorViewModel? CreatePasswordResetEditor() => ShowResetPassword && CanEditRecord && editor is IPasswordResetService
        ? new CatalogEditorViewModel(editor, CatalogSection.Users, SelectedRow!.Id, true) : null;
    public bool IsAuditLog => SelectedSection?.Section == CatalogSection.AuditLog;
    public string Title => SelectedSection?.Title ?? "Реестры";
    public CatalogRow? SelectedRow
    {
        get => selectedRow;
        set { selectedRow = value; Changed(); Changed(nameof(CanOpenRecord)); Changed(nameof(CanEditRecord)); Changed(nameof(CanCreateRecord)); Changed(nameof(CanDeleteRecord)); }
    }
    public bool CanOpenRecord => !IsBusy && SelectedRow != null;
    public bool CanEditRecord => CanOpenRecord && CanManageSection && SelectedRow!.Id > 0 && editor != null;
    public CatalogEditorViewModel? CreateEditor() => CanEditRecord
        ? new CatalogEditorViewModel(editor!, SelectedSection!.Section, SelectedRow!.Id) : null;
    public bool CanCreateDelete => CanManageSection && SelectedSection?.Section != CatalogSection.Roles;
    public bool CanCreateRecord => CanCreateDelete && !IsBusy && editor != null;
    public bool CanDeleteRecord => CanCreateDelete && CanEditRecord;
    public CatalogEditorViewModel? CreateNewEditor() => CanCreateRecord
        ? new CatalogEditorViewModel(editor!, SelectedSection!.Section, 0) : null;

    public async Task<CatalogEditData?> PrepareDeleteAsync()
    {
        if (!CanDeleteRecord) return null;
        var section = SelectedSection!.Section;
        var id = SelectedRow!.Id;
        IsBusy = true;
        try { return await editor!.LoadAsync(section, id); }
        catch (Exception) { Status = "Не удалось подготовить удаление. Обновите список и проверьте права доступа."; return null; }
        finally { IsBusy = false; }
    }
    public async Task DeleteAsync(CatalogEditData data)
    {
        if (IsBusy || editor == null) return;
        IsBusy = true;
        try
        {
            var result = await editor.DeleteAsync(data);
            if (result.Success) await LoadAsync();
            Status = result.Message;
        }
        catch (UnauthorizedAccessException) { Status = "Недостаточно прав или роль изменилась. Войдите заново."; }
        catch (Exception) { Status = "Не удалось подтвердить удаление. Обновите список перед повтором."; }
        finally { IsBusy = false; }
    }
    // Кнопка видна по роли; сервис независимо проверяет право сохранения.
    public bool CanManageSection => SelectedSection?.Section switch
    {
        CatalogSection.Drivers or CatalogSection.Assignments => access.Can(Permission.ManageDrivers),
        CatalogSection.Documents => access.Can(Permission.ManageDocuments),
        CatalogSection.Maintenance or CatalogSection.WorkParts => access.Can(Permission.ManageMaintenance),
        CatalogSection.Parts => access.Can(Permission.ManageParts),
        CatalogSection.Users or CatalogSection.Roles => access.Can(Permission.ManageUsers),
        CatalogSection.AuditLog => false,
        _ => false
    };
    public string Search { get => search; set { search = value; Changed(); Filter(); } }
    public string Status { get => status; private set { status = value; Changed(); } }
    public bool IsBusy { get => busy; private set { busy = value; Changed(); Changed(nameof(CanRefresh)); Changed(nameof(CanOpenRecord)); Changed(nameof(CanEditRecord)); Changed(nameof(CanCreateRecord)); Changed(nameof(CanDeleteRecord)); } }
    public bool CanRefresh => !IsBusy && SelectedSection != null;
    public string CountText => $"Показано: {Rows.Count} из {allRows.Count}";
    public bool HasLoaded { get; private set; }

    public async Task LoadAsync()
    {
        var current = ++request;
        var section = SelectedSection;
        allRows = new List<CatalogRow>(); Rows.Clear(); SelectedRow = null; Changed(nameof(CountText));
        if (section == null) { IsBusy = false; Status = "Нет доступных разделов."; return; }
        IsBusy = true;
        Status = "Загрузка данных…";
        try
        {
            var rows = await service.ReadAsync(section.Section);
            if (current != request) return;
            allRows = new List<CatalogRow>(rows);
            Filter();
            Status = rows.Count == 0 ? "В этом разделе пока нет записей." : $"Загружено записей: {rows.Count}.";
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
        SelectedRow = null;
        var query = Search.Trim();
        foreach (var row in allRows.Where(x => query.Length == 0 ||
            string.Join(" ", x.Title, x.Subtitle, x.Details, x.Badge).Contains(query, StringComparison.CurrentCultureIgnoreCase)))
            Rows.Add(row);
        Changed(nameof(CountText));
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
