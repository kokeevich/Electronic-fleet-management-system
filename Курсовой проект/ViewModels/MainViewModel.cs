using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Курсовой_проект.Models;
using Курсовой_проект.Services;

namespace Курсовой_проект.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IFleetService fleet;
    private bool isBusy;
    private string status = "Подключение ещё не проверено.";
    private List<Vehicle> allVehicles = new();
    private string searchText = "", selectedStatus = "Все статусы";
    private Vehicle? selectedVehicle;

    public MainViewModel(IFleetService fleet, UserSession session, AccessService access)
    {
        this.fleet = fleet;
        UserName = session.Current?.FullName ?? "";
        RoleName = session.Current?.Role ?? "";
        CanManageSettings = access.Can(Permission.ManageSettings);
        CanManageVehicles = access.Can(Permission.ManageVehicles);
        RefreshCommand = new AsyncCommand(LoadAsync);
    }

    public ObservableCollection<Vehicle> Vehicles { get; } = new();
    public ObservableCollection<string> Statuses { get; } = new() { "Все статусы", "В эксплуатации", "В ремонте", "Не используется" };
    public string SearchText
    {
        get => searchText;
        set { searchText = value; OnPropertyChanged(); ApplyFilter(); }
    }
    public string SelectedStatus
    {
        get => selectedStatus;
        set { selectedStatus = value ?? "Все статусы"; OnPropertyChanged(); ApplyFilter(); }
    }
    public Vehicle? SelectedVehicle
    {
        get => selectedVehicle;
        set { selectedVehicle = value; OnPropertyChanged(); SelectionChanged(); }
    }
    public bool CanManageVehicles { get; }
    public bool IsIdle => !IsBusy;
    public bool CanAdd => CanManageVehicles && IsIdle;
    public bool CanOpenCard => SelectedVehicle != null && IsIdle;
    public bool CanEditSelection => CanOpenCard && CanManageVehicles;
    public string ResultSummary => $"Показано: {Vehicles.Count} из {allVehicles.Count}";

    private void SelectionChanged()
    {
        OnPropertyChanged(nameof(CanAdd));
        OnPropertyChanged(nameof(CanOpenCard));
        OnPropertyChanged(nameof(CanEditSelection));
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        Vehicles.Clear();
        SelectedVehicle = null;
        foreach (var vehicle in allVehicles.Where(v =>
            (SelectedStatus == "Все статусы" || v.Status == SelectedStatus) &&
            (query.Length == 0 || v.LicensePlate.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
             v.Brand.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
             v.Model.Contains(query, StringComparison.CurrentCultureIgnoreCase))))
            Vehicles.Add(vehicle);
        OnPropertyChanged(nameof(ResultSummary));
    }

    public VehicleEditorViewModel? CreateEditor(bool createNew)
    {
        if (createNew && !CanAdd || !createNew && !CanOpenCard) return null;
        return new VehicleEditorViewModel(fleet,
            createNew ? null : VehicleData.From(SelectedVehicle!), !CanManageVehicles);
    }

    public async Task DeleteSelectedAsync()
    {
        if (!CanEditSelection) return;
        var original = VehicleData.From(SelectedVehicle!);
        IsBusy = true;
        VehicleResult? result = null;
        try
        {
            result = await fleet.DeleteAsync(original);
            Status = result.Message;
        }
        catch (UnauthorizedAccessException) { Status = "Недостаточно прав для удаления транспорта."; }
        catch (Exception) { Status = "Не удалось подтвердить удаление. Проверьте подключение и обновите список перед повтором."; }
        finally { IsBusy = false; }
        if (result?.Success == true)
        {
            await LoadAsync();
            Status = result.Message + " " + Status;
        }
    }
    public string UserName { get; }
    public string RoleName { get; }
    public bool CanManageSettings { get; }
    public ICommand RefreshCommand { get; }
    public bool IsBusy
    {
        get => isBusy;
        private set { isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsIdle)); SelectionChanged(); }
    }
    public string Status
    {
        get => status;
        private set { status = value; OnPropertyChanged(); }
    }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Status = "Загрузка данных из SQL Server…";
        Vehicles.Clear();
        allVehicles.Clear();
        SelectedVehicle = null;
        OnPropertyChanged(nameof(ResultSummary));
        try
        {
            var vehicles = await fleet.GetVehiclesAsync();
            allVehicles = vehicles;
            foreach (var vehicleStatus in vehicles.Select(v => v.Status).Distinct())
                if (!Statuses.Contains(vehicleStatus)) Statuses.Add(vehicleStatus);
            ApplyFilter();
            Status = vehicles.Count == 0
                ? "Подключение установлено. В базе пока нет транспортных средств."
                : $"Подключение установлено. Загружено транспортных средств: {vehicles.Count}.";
        }
        catch (UnauthorizedAccessException)
        {
            Status = "Доступ закрыт. Войдите в систему с подходящей ролью.";
        }
        catch (Exception)
        {
            // Исключение может содержать сервер или строку подключения.
            Status = "Не удалось загрузить данные. Проверьте настройку подключения, доступность SQL Server и права чтения. Инструкция — в README. После исправления нажмите «Обновить».";
        }
        finally { IsBusy = false; }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class AsyncCommand(Func<Task> execute) : ICommand
{
    private bool running;
    public bool CanExecute(object? parameter) => !running;
    public event EventHandler? CanExecuteChanged;
    public async void Execute(object? parameter)
    {
        if (running) return;
        running = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await execute(); }
        finally
        {
            running = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
