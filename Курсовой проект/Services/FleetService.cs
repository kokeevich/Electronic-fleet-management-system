using Microsoft.EntityFrameworkCore;
using Курсовой_проект.Models;

namespace Курсовой_проект.Services;

public interface IFleetService
{
    Task<List<Vehicle>> GetVehiclesAsync();
}

public sealed class FleetService(AccessService access, UserSession session) : IFleetService
{
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
