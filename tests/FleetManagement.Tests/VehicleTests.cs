using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Курсовой_проект.Models;
using Курсовой_проект.Services;
using Курсовой_проект.ViewModels;

namespace FleetManagement.Tests;

public class VehicleTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly DbContextOptions<OzvsKursachContext> options;
    private readonly VehicleStore store;

    public VehicleTests()
    {
        connection.Open();
        options = new DbContextOptionsBuilder<OzvsKursachContext>().UseSqlite(connection).Options;
        using var database = CreateContext();
        // Только изолированная тестовая SQLite в памяти; приложение этого не делает.
        database.Database.EnsureCreated();
        store = new VehicleStore(CreateContext);
    }

    private OzvsKursachContext CreateContext() => new(options);
    public void Dispose() => connection.Dispose();
    private static VehicleData Valid() => new(0, "А123БВ196", "Kia", "Rio", 2021, 1000, "В эксплуатации", "TEST-VIN-1");
    private async Task<VehicleData> CreateAsync()
    {
        Assert.True((await store.SaveAsync(null, Valid())).Success);
        await using var database = CreateContext();
        return VehicleData.From(await database.Vehicles.SingleAsync());
    }

    [Fact]
    public async Task CreateUpdateAndDeletePersist()
    {
        var original = await CreateAsync();
        var edited = original with { Mileage = 2000, Status = "В ремонте" };
        Assert.True((await store.SaveAsync(original, edited)).Success);
        await using (var database = CreateContext())
            Assert.Equal(edited, VehicleData.From(await database.Vehicles.SingleAsync()));
        Assert.True((await store.DeleteAsync(edited)).Success);
        await using var empty = CreateContext();
        Assert.Empty(await empty.Vehicles.ToListAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DuplicatePlateOrVinCannotCreateAnotherVehicle(bool duplicatePlate)
    {
        await CreateAsync();
        var edited = duplicatePlate ? Valid() with { VinNumber = "OTHER-VIN" }
            : Valid() with { LicensePlate = "В456ГД196" };
        Assert.False((await store.SaveAsync(null, edited)).Success);
        await using var database = CreateContext();
        Assert.Single(await database.Vehicles.ToListAsync());
    }

    [Fact]
    public async Task NullVinMatchesOriginalSqlServerUniqueRule()
    {
        Assert.True((await store.SaveAsync(null, Valid() with { VinNumber = null })).Success);
        var result = await store.SaveAsync(null, Valid() with { LicensePlate = "В456ГД196", VinNumber = null });
        Assert.False(result.Success);
        Assert.Contains("пустой VIN", result.Message);
    }

    [Fact]
    public async Task StaleEditAndDeleteCannotOverwriteAnotherUsersChanges()
    {
        var original = await CreateAsync();
        var changed = original with { Mileage = 5000 };
        Assert.True((await store.SaveAsync(original, changed)).Success);
        Assert.False((await store.SaveAsync(original, original with { Brand = "Другая марка" })).Success);
        Assert.False((await store.DeleteAsync(original)).Success);
        await using var database = CreateContext();
        Assert.Equal(changed, VehicleData.From(await database.Vehicles.SingleAsync()));
    }

    [Fact]
    public async Task EfDetectsChangesAfterEntityWasRead()
    {
        var original = await CreateAsync();
        await using var first = CreateContext();
        await using var second = CreateContext();
        var vehicle = await first.Vehicles.SingleAsync();
        var another = await second.Vehicles.SingleAsync();
        another.Mileage = 9000;
        await second.SaveChangesAsync();
        vehicle.Brand = "Changed";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => first.SaveChangesAsync());
    }

    [Theory]
    [InlineData("document")]
    [InlineData("maintenance")]
    [InlineData("assignment")]
    public async Task RelatedRecordsPreventDeletion(string relation)
    {
        var original = await CreateAsync();
        await using (var database = CreateContext())
        {
            if (relation == "document")
                database.Documents.Add(new Document { VehicleId = original.VehicleId, DocumentType = "ОСАГО",
                    DocumentNumber = "TEST-1", IssueDate = new DateOnly(2026, 1, 1) });
            else if (relation == "maintenance")
                database.MaintenanceRepairs.Add(new MaintenanceRepair { VehicleId = original.VehicleId,
                    WorkDate = new DateOnly(2026, 1, 1), WorkType = "ТО", Mileage = 1000, Cost = 100 });
            else
                database.VehicleAssignments.Add(new VehicleAssignment { VehicleId = original.VehicleId,
                    StartDate = new DateOnly(2026, 1, 1), Driver = new Driver { LastName = "Тестов",
                        FirstName = "Иван", LicenseNumber = "TEST-1", LicenseCategory = "B",
                        LicenseIssueDate = new DateOnly(2020, 1, 1), LicenseExpiryDate = new DateOnly(2030, 1, 1) } });
            await database.SaveChangesAsync();
        }
        var result = await store.DeleteAsync(original);
        Assert.False(result.Success);
        Assert.Contains("Удаление запрещено", result.Message);
        await using var remaining = CreateContext();
        Assert.Single(await remaining.Vehicles.ToListAsync());
    }

    [Theory]
    [InlineData("Механик")]
    [InlineData("Наблюдатель")]
    [InlineData("Неизвестная роль")]
    public async Task ServiceDeniesWritesBeforeReachingStorage(string role)
    {
        var session = new UserSession();
        session.SignIn(new SessionUser(1, "Тест", role));
        var service = new FleetService(new AccessService(session), session, store);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveAsync(null, Valid()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteAsync(Valid() with { VehicleId = 1 }));
        await using var database = CreateContext();
        Assert.Empty(await database.Vehicles.ToListAsync());
    }

    [Theory]
    [InlineData("Администратор")]
    [InlineData("Диспетчер")]
    public async Task AllowedRolesSaveNormalizedDataAndRejectInvalidValues(string role)
    {
        var session = new UserSession();
        session.SignIn(new SessionUser(1, "Тест", role));
        var service = new FleetService(new AccessService(session), session, store);
        Assert.False((await service.SaveAsync(null, Valid() with { Mileage = -1 })).Success);
        Assert.False((await service.SaveAsync(null, Valid() with { VehicleId = 7 })).Success);
        Assert.True((await service.SaveAsync(null, Valid() with { LicensePlate = " а123бв196 ", Brand = " Kia " })).Success);
        await using var database = CreateContext();
        var saved = await database.Vehicles.SingleAsync();
        Assert.Equal("А123БВ196", saved.LicensePlate);
        Assert.Equal("Kia", saved.Brand);
    }

    [Fact]
    public void ValidationRespectsSchemaAndYearRange()
    {
        Assert.NotNull((Valid() with { YearOfManufacture = DateTime.Today.Year + 2 }).Validate());
        Assert.NotNull((Valid() with { YearOfManufacture = 1899 }).Validate());
        Assert.NotNull((Valid() with { Mileage = -1 }).Validate());
        Assert.NotNull((Valid() with { LicensePlate = new string('А', 21) }).Validate());
        Assert.NotNull((Valid() with { Brand = new string('а', 101) }).Validate());
        Assert.NotNull((Valid() with { Model = "" }).Validate());
        Assert.NotNull((Valid() with { VinNumber = new string('A', 51) }).Validate());
        Assert.NotNull((Valid() with { Status = "" }).Validate());
        Assert.Null((Valid() with { YearOfManufacture = DateTime.Today.Year + 1, VinNumber = null }).Validate());
    }
}
