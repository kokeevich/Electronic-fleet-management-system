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

    public MainViewModel(IFleetService fleet, UserSession session, AccessService access)
    {
        this.fleet = fleet;
        UserName = session.Current?.FullName ?? "";
        RoleName = session.Current?.Role ?? "";
        CanManageSettings = access.Can(Permission.ManageSettings);
        RefreshCommand = new AsyncCommand(LoadAsync);
    }

    public ObservableCollection<Vehicle> Vehicles { get; } = new();
    public string UserName { get; }
    public string RoleName { get; }
    public bool CanManageSettings { get; }
    public ICommand RefreshCommand { get; }
    public bool IsBusy
    {
        get => isBusy;
        private set { isBusy = value; OnPropertyChanged(); }
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
        try
        {
            var vehicles = await fleet.GetVehiclesAsync();
            foreach (var vehicle in vehicles) Vehicles.Add(vehicle);
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
