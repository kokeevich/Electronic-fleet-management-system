using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Globalization;
using Курсовой_проект.Services;

namespace Курсовой_проект.ViewModels;

public sealed class VehicleEditorViewModel : INotifyPropertyChanged
{
    private readonly IFleetService fleet;
    private readonly VehicleData? original;
    private string message = "";
    private bool busy;
    public VehicleEditorViewModel(IFleetService fleet, VehicleData? original, bool readOnly)
    {
        this.fleet = fleet;
        this.original = original;
        IsReadOnly = readOnly;
        LicensePlate = original?.LicensePlate ?? "";
        Brand = original?.Brand ?? "";
        Model = original?.Model ?? "";
        Year = (original?.YearOfManufacture ?? DateTime.Today.Year).ToString(CultureInfo.InvariantCulture);
        Mileage = (original?.Mileage ?? 0).ToString(CultureInfo.InvariantCulture);
        Status = original?.Status ?? "В эксплуатации";
        VinNumber = original?.VinNumber ?? "";
    }

    public string Title => original == null ? "Новый автомобиль" : IsReadOnly ? "Карточка автомобиля" : "Редактирование автомобиля";
    public bool IsReadOnly { get; }
    public bool CanEdit => !IsReadOnly && !IsBusy;
    public bool CanClose => !IsBusy;
    public string LicensePlate { get; set; }
    public string Brand { get; set; }
    public string Model { get; set; }
    public string Year { get; set; }
    public string Mileage { get; set; }
    public string Status { get; set; }
    public string VinNumber { get; set; }
    public string[] Statuses { get; } = ["В эксплуатации", "В ремонте", "Не используется"];
    public string Message { get => message; private set { message = value; Changed(); } }
    public bool IsBusy
    {
        get => busy;
        private set { busy = value; Changed(); Changed(nameof(CanEdit)); Changed(nameof(CanClose)); }
    }

    public async Task<bool> SaveAsync()
    {
        if (!CanEdit) return false;
        Message = "";
        if (!int.TryParse(Year, NumberStyles.None, CultureInfo.InvariantCulture, out var year) ||
            !int.TryParse(Mileage, NumberStyles.None, CultureInfo.InvariantCulture, out var mileage))
        {
            Message = "Год и пробег должны быть целыми неотрицательными числами.";
            return false;
        }
        IsBusy = true;
        try
        {
            var edited = new VehicleData(original?.VehicleId ?? 0, LicensePlate, Brand, Model, year, mileage, Status, VinNumber);
            var result = await fleet.SaveAsync(original, edited);
            Message = result.Message;
            return result.Success;
        }
        catch (UnauthorizedAccessException) { Message = "Недостаточно прав для изменения транспорта."; }
        catch (Exception)
        {
            Message = "Не удалось подтвердить сохранение. Проверьте подключение и обновите список перед повтором: запись могла сохраниться.";
        }
        finally { IsBusy = false; }
        return false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
