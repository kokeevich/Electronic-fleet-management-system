using Microsoft.EntityFrameworkCore;
using Курсовой_проект.Models;

namespace Курсовой_проект.Services;

public interface IFleetService
{
    Task<List<Vehicle>> GetVehiclesAsync();
    Task<VehicleResult> SaveAsync(VehicleData? original, VehicleData edited);
    Task<VehicleResult> DeleteAsync(VehicleData original);
}

public sealed class FleetService(AccessService access, UserSession session, IVehicleStore store) : IFleetService
{
    public Task<VehicleResult> SaveAsync(VehicleData? original, VehicleData edited)
    {
        access.Require(Permission.ManageVehicles);
        if (original == null && edited.VehicleId != 0 || original != null &&
            (original.VehicleId <= 0 || original.VehicleId != edited.VehicleId))
            return Task.FromResult(new VehicleResult(false, "Некорректный идентификатор автомобиля."));
        var normalized = edited.Normalize();
        var error = normalized.Validate();
        return error == null ? store.SaveAsync(original, normalized)
            : Task.FromResult(new VehicleResult(false, error));
    }

    public Task<VehicleResult> DeleteAsync(VehicleData original)
    {
        access.Require(Permission.ManageVehicles);
        if (original.VehicleId <= 0)
            return Task.FromResult(new VehicleResult(false, "Некорректный идентификатор автомобиля."));
        return store.DeleteAsync(original);
    }

    public async Task<List<Vehicle>> GetVehiclesAsync()
    {
        access.Require(Permission.ViewVehicles);
        var requestUser = session.Current;
        await using var database = DatabaseFactory.Create();
        // Успешное чтение проверяет и соединение, и доступ к таблице.
        var vehicles = await database.Vehicles.AsNoTracking()
            .OrderBy(vehicle => vehicle.LicensePlate).ToListAsync();
        access.Require(Permission.ViewVehicles);
        if (!ReferenceEquals(requestUser, session.Current))
            throw new UnauthorizedAccessException("Сессия завершена. Войдите снова.");
        return vehicles;
    }
}
