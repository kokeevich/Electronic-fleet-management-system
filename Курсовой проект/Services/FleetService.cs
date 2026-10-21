using Microsoft.EntityFrameworkCore;
using Курсовой_проект.Configuration;
using Курсовой_проект.Models;

namespace Курсовой_проект.Services;

public interface IFleetService
{
    Task<List<Vehicle>> GetVehiclesAsync();
}

public sealed class FleetService : IFleetService
{
    public async Task<List<Vehicle>> GetVehiclesAsync()
    {
        var options = new DbContextOptionsBuilder<OzvsKursachContext>()
            .UseSqlServer(DatabaseSettings.GetConnectionString(),
                sql => sql.CommandTimeout(15))
            .Options;
        await using var database = new OzvsKursachContext(options);
        // Успешное чтение проверяет и соединение, и доступ к таблице.
        return await database.Vehicles.AsNoTracking()
            .OrderBy(vehicle => vehicle.LicensePlate).ToListAsync();
    }
}
