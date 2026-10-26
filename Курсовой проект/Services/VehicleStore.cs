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
    private readonly UserSession? session;
    public VehicleStore(AccessService access, UserSession session) { this.session = session; createContext = DatabaseFactory.Create; }
    internal VehicleStore() => createContext = DatabaseFactory.Create;
    internal VehicleStore(Func<OzvsKursachContext> createContext) { this.createContext = createContext; }
    internal VehicleStore(Func<OzvsKursachContext> createContext, UserSession session) { this.createContext = createContext; this.session = session; }
    private static VehicleResult Conflict() => new(false,
        "Запись уже изменена или удалена другим пользователем. Обновите список и откройте карточку заново.");

    public async Task<VehicleResult> SaveAsync(VehicleData? original, VehicleData edited)
    {
        await using var database = createContext();
        var actor = session?.Current;
        if (session != null && actor == null) throw new UnauthorizedAccessException();
        await using var transaction = await database.Database.BeginTransactionAsync();
        if (actor != null)
        {
            var role = await database.Users.Where(x => x.UserId == actor.UserId).Select(x => x.Role.Name).SingleOrDefaultAsync();
            if (role != actor.Role || role is not ("Администратор" or "Диспетчер")) throw new UnauthorizedAccessException();
        }
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
            var before = original == null ? null : System.Text.Json.JsonSerializer.Serialize(original);
            await database.SaveChangesAsync();
            if (actor != null)
            {
                database.AuditLogs.Add(AuditSnapshot.Create(actor, original == null ? "Создание" : "Изменение", "Транспорт",
                    vehicle.VehicleId, before, System.Text.Json.JsonSerializer.Serialize(VehicleData.From(vehicle))));
                await database.SaveChangesAsync();
            }
            await transaction.CommitAsync();
            NotificationService.NotifyDataChanged();
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
        var actor = session?.Current;
        if (session != null && actor == null) throw new UnauthorizedAccessException();
        await using var transaction = await database.Database.BeginTransactionAsync();
        if (actor != null)
        {
            var role = await database.Users.Where(x => x.UserId == actor.UserId).Select(x => x.Role.Name).SingleOrDefaultAsync();
            if (role != actor.Role || role is not ("Администратор" or "Диспетчер")) throw new UnauthorizedAccessException();
        }
        var vehicle = await database.Vehicles.FindAsync(original.VehicleId);
        if (vehicle == null || VehicleData.From(vehicle) != original) return Conflict();
        if (await database.VehicleAssignments.AnyAsync(x => x.VehicleId == original.VehicleId) ||
            await database.Documents.AnyAsync(x => x.VehicleId == original.VehicleId) ||
            await database.MaintenanceRepairs.AnyAsync(x => x.VehicleId == original.VehicleId))
            return new(false, "Удаление запрещено: есть закрепления, документы или история обслуживания. Можно установить статус «Не используется».");

        database.Vehicles.Remove(vehicle);
        try
        {
            var before = System.Text.Json.JsonSerializer.Serialize(original);
            await database.SaveChangesAsync();
            if (actor != null)
            {
                database.AuditLogs.Add(AuditSnapshot.Create(actor, "Удаление", "Транспорт", original.VehicleId, before, null));
                await database.SaveChangesAsync();
            }
            await transaction.CommitAsync();
            NotificationService.NotifyDataChanged();
            return new(true, "Автомобиль удалён.");
        }
        catch (DbUpdateConcurrencyException) { return Conflict(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
        {
            return new(false, "Удаление запрещено: у автомобиля появились связанные записи. Обновите список.");
        }
    }
}
