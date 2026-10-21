using Курсовой_проект.Models;

namespace Курсовой_проект.Services;

public sealed record VehicleData(int VehicleId, string LicensePlate, string Brand, string Model,
    int YearOfManufacture, int Mileage, string Status, string? VinNumber)
{
    public static VehicleData From(Vehicle vehicle) => new(vehicle.VehicleId, vehicle.LicensePlate,
        vehicle.Brand, vehicle.Model, vehicle.YearOfManufacture, vehicle.Mileage, vehicle.Status, vehicle.VinNumber);

    public VehicleData Normalize() => this with
    {
        LicensePlate = LicensePlate.Trim().ToUpperInvariant(),
        Brand = Brand.Trim(), Model = Model.Trim(), Status = Status.Trim(),
        VinNumber = string.IsNullOrWhiteSpace(VinNumber) ? null : VinNumber.Trim().ToUpperInvariant()
    };

    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(LicensePlate) || LicensePlate.Length > 20)
            return "Укажите госномер длиной до 20 символов.";
        if (string.IsNullOrWhiteSpace(Brand) || Brand.Length > 100 ||
            string.IsNullOrWhiteSpace(Model) || Model.Length > 100)
            return "Укажите марку и модель (до 100 символов каждое).";
        if (YearOfManufacture < 1900 || YearOfManufacture > DateTime.Today.Year + 1)
            return $"Год выпуска должен быть от 1900 до {DateTime.Today.Year + 1}.";
        if (Mileage < 0) return "Пробег не может быть отрицательным.";
        if (string.IsNullOrWhiteSpace(Status) || Status.Length > 50)
            return "Укажите статус длиной до 50 символов.";
        if (VinNumber?.Length > 50) return "VIN не должен превышать 50 символов.";
        if (new[] { LicensePlate, Brand, Model, Status, VinNumber ?? "" }.Any(s => s.Any(char.IsControl)))
            return "Поля не должны содержать управляющие символы.";
        return null;
    }
}

public sealed record VehicleResult(bool Success, string Message);
