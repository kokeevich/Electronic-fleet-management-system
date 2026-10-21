using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Курсовой_проект.Models;

namespace Курсовой_проект.Services;

public interface IVehicleStore
{
    Task<VehicleResult> SaveAsync(VehicleData? original, VehicleData edited);
    Task<VehicleResult> DeleteAsync(VehicleData original);
}

public sealed class VehicleStore : IVehicleStore
{
    private readonly Func<OzvsKursachContext> createContext;
    public VehicleStore() => createContext = DatabaseFactory.Create;
    internal VehicleStore(Func<OzvsKursachContext> createContext) => this.createContext = createContext;
    private static VehicleResult Conflict() => new(false,
        "Запись уже изменена или удалена другим пользователем. Обновите список и откройте карточку заново.");

    public async Task<VehicleResult> SaveAsync(VehicleData? original, VehicleData edited)
    {
        await using var database = createContext();
        var vehicle = original == null ? new Vehicle() : await database.Vehicles.FindAsync(original.VehicleId);
        if (vehicle == null || original != null && VehicleData.From(vehicle) != original)
            return Conflict();

        if (await database.Vehicles.AnyAsync(v => v.VehicleId != edited.VehicleId && v.LicensePlate == edited.LicensePlate))
            return new(false, "Автомобиль с таким госномером уже существует.");
        // Исходное UNIQUE(VinNumber) в SQL Server разрешает только один NULL.
        if (await database.Vehicles.AnyAsync(v => v.VehicleId != edited.VehicleId && v.VinNumber == edited.VinNumber))
            return new(false, edited.VinNumber == null
                ? "В базе уже есть автомобиль без VIN. Укажите VIN: текущая схема допускает только один пустой VIN."
                : "Автомобиль с таким VIN уже существует.");

        vehicle.LicensePlate = edited.LicensePlate;
        vehicle.Brand = edited.Brand;
        vehicle.Model = edited.Model;
        vehicle.YearOfManufacture = edited.YearOfManufacture;
        vehicle.Mileage = edited.Mileage;
        vehicle.Status = edited.Status;
        vehicle.VinNumber = edited.VinNumber;
        if (original == null) database.Vehicles.Add(vehicle);
        try
        {
            await database.SaveChangesAsync();
            return new(true, original == null ? "Автомобиль добавлен." : "Изменения сохранены.");
        }
        catch (DbUpdateConcurrencyException) { return Conflict(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return new(false, "Госномер или VIN уже занят. Проверьте данные и обновите список.");
        }
    }

    public async Task<VehicleResult> DeleteAsync(VehicleData original)
    {
        await using var database = createContext();
        var vehicle = await database.Vehicles.FindAsync(original.VehicleId);
        if (vehicle == null || VehicleData.From(vehicle) != original) return Conflict();
        if (await database.VehicleAssignments.AnyAsync(x => x.VehicleId == original.VehicleId) ||
            await database.Documents.AnyAsync(x => x.VehicleId == original.VehicleId) ||
            await database.MaintenanceRepairs.AnyAsync(x => x.VehicleId == original.VehicleId))
            return new(false, "Удаление запрещено: есть закрепления, документы или история обслуживания. Можно установить статус «Не используется».");

        database.Vehicles.Remove(vehicle);
        try
        {
            await database.SaveChangesAsync();
            return new(true, "Автомобиль удалён.");
        }
        catch (DbUpdateConcurrencyException) { return Conflict(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
        {
            return new(false, "Удаление запрещено: у автомобиля появились связанные записи. Обновите список.");
        }
    }
}
