using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Курсовой_проект.Models;
using Курсовой_проект.Services;
using Курсовой_проект.ViewModels;

namespace FleetManagement.Tests;

public class CatalogTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly DbContextOptions<OzvsKursachContext> options;
    private readonly UserSession session = new();
    private readonly AccessService access;
    private readonly CatalogService service;

    public CatalogTests()
    {
        connection.Open();
        options = new DbContextOptionsBuilder<OzvsKursachContext>().UseSqlite(connection).Options;
        access = new AccessService(session);
        service = new CatalogService(access, session, () => new OzvsKursachContext(options));
        using var db = new OzvsKursachContext(options);
        db.Database.EnsureCreated();
        var role = new Role { Name = "Администратор" };
        var user = new User { UserId = 1, Role = role, FirstName = "Иван", LastName = "Тестов", Login = "test", Password = "NOT-FOR-DISPLAY" };
        var other = new User { UserId = 2, Role = role, FirstName = "Анна", LastName = "Тестова", Login = "other", Password = "NOT-FOR-DISPLAY" };
        var driver = new Driver { FirstName = "Олег", LastName = "Водитель", Phone = "123",
            LicenseNumber = "TEST-LICENSE", LicenseCategory = "B", LicenseIssueDate = new(2020, 1, 1), LicenseExpiryDate = new(2030, 1, 1) };
        var car = new Vehicle { LicensePlate = "А123БВ196", Brand = "Kia", Model = "Rio", YearOfManufacture = 2021, Status = "В эксплуатации" };
        var work = new MaintenanceRepair { Vehicle = car, WorkDate = new(2026, 1, 1), WorkType = "Замена масла", Cost = 1200, Mileage = 9000 };
        var part = new Part { Name = "Фильтр", ArticleNumber = "F-1", Quantity = 10, Cost = 250 };
        db.Users.AddRange(user, other);
        db.VehicleAssignments.Add(new VehicleAssignment { Vehicle = car, Driver = driver, StartDate = new(2025, 1, 1) });
        db.Documents.Add(new Document { Vehicle = car, Driver = driver, DocumentType = "ОСАГО", DocumentNumber = "DOC-1", IssueDate = new(2026, 1, 1) });
        db.WorkParts.Add(new WorkPart { Work = work, Part = part, Quantity = 2 });
        db.SaveChanges();
        foreach (var (title, type, recipient) in new[]
        {
            ("own-insurance", "Окончание страховки", (int?)1),
            ("other-insurance", "Окончание страховки", (int?)2),
            ("public-maintenance", "Плановое ТО", (int?)null),
            ("own-maintenance", "Плановое ТО", (int?)1),
            ("public-insurance", "Окончание страховки", (int?)null)
        })
            db.Notifications.Add(new Notification { Title = title, NotificationType = type, UserId = recipient,
                Message = "Тест", RelatedEntityType = "Документ", RelatedEntityId = 1,
                CriticalDate = new(2026, 1, 1), CreatedDate = new DateTime(2026, 1, 1) });
        db.SaveChanges();
        session.SignIn(new SessionUser(1, "Тест", "Администратор"));
    }

    public void Dispose() => connection.Dispose();

    [Fact]
    public async Task AllSectionsReturnLinkedReadableRecordsWithoutPasswords()
    {
        foreach (var section in CatalogDefinition.All)
        {
            var rows = await service.ReadAsync(section.Section);
            Assert.NotEmpty(rows);
            Assert.All(rows, row => Assert.True(row.Id > 0));
            Assert.All(rows, row => Assert.DoesNotContain("NOT-FOR-DISPLAY", row.ToString()));
        }
        var assignment = Assert.Single(await service.ReadAsync(CatalogSection.Assignments));
        Assert.Contains("А123БВ196", assignment.Title);
        Assert.Contains("Водитель Олег", assignment.Subtitle);
        var composition = Assert.Single(await service.ReadAsync(CatalogSection.WorkParts));
        Assert.Equal("Фильтр", composition.Title);
        Assert.Contains("Замена масла", composition.Subtitle);
        Assert.Equal("2 шт.", composition.Badge);
        var document = Assert.Single(await service.ReadAsync(CatalogSection.Documents));
        Assert.Contains("А123БВ196", document.Subtitle);
        Assert.Contains("Водитель Олег", document.Subtitle);
        using var db = new OzvsKursachContext(options);
        Assert.All(await db.Users.ToListAsync(), user => Assert.Equal("NOT-FOR-DISPLAY", user.Password));
        Assert.All(await db.Notifications.ToListAsync(), notification => Assert.False(notification.IsRead));
    }

    [Fact]
    public async Task RoleChecksApplyAndNotificationsRemainUnavailable()
    {
        session.SignIn(new SessionUser(1, "Тест", "Диспетчер"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReadAsync(CatalogSection.Notifications));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReadAsync(CatalogSection.Users));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReadAsync(CatalogSection.Roles));
        session.SignIn(new SessionUser(1, "Тест", "Механик"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReadAsync(CatalogSection.Notifications));
        session.SignIn(new SessionUser(1, "Тест", "Наблюдатель"));
        var vm = new CatalogViewModel(service, access);
        Assert.DoesNotContain(vm.Sections, x => x.Section is CatalogSection.Users or CatalogSection.Roles or CatalogSection.Notifications);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReadAsync(CatalogSection.Notifications));
        session.SignIn(new SessionUser(1, "Тест", "Администратор"));
        Assert.DoesNotContain(new CatalogViewModel(service, access).Sections, x => x.Section == CatalogSection.Notifications);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReadAsync(CatalogSection.Notifications));
        session.SignOut();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReadAsync(CatalogSection.Drivers));
    }

    private sealed class PendingService : ICatalogService
    {
        public TaskCompletionSource<List<CatalogRow>> Drivers { get; } = new();
        public TaskCompletionSource<List<CatalogRow>> Assignments { get; } = new();
        public bool Fail;
        public Task<List<CatalogRow>> ReadAsync(CatalogSection section) => Fail
            ? throw new Exception("Password=secret")
            : section == CatalogSection.Drivers ? Drivers.Task : Assignments.Task;
    }

    [Fact]
    public async Task SwitchingAndSearchIgnoreOldResultsAndClearDataOnFailure()
    {
        var pending = new PendingService();
        var vm = new CatalogViewModel(pending, access);
        var oldLoad = vm.LoadAsync();
        vm.SelectedSection = vm.Sections.Single(x => x.Section == CatalogSection.Assignments);
        pending.Drivers.SetResult(new() { new("Old", "", "", "") });
        await oldLoad;
        Assert.DoesNotContain(vm.Rows, x => x.Title == "Old");
        pending.Assignments.SetResult(new() { new("Kia", "Олег", "А123БВ196", "Действует") });
        await vm.LoadAsync();
        vm.Search = " олег ";
        Assert.Single(vm.Rows);
        vm.Search = "нет совпадений";
        Assert.Empty(vm.Rows);
        vm.Search = "";
        Assert.Single(vm.Rows);
        pending.Fail = true;
        await vm.LoadAsync();
        Assert.Empty(vm.Rows);
        Assert.DoesNotContain("secret", vm.Status);
        Assert.True(vm.CanRefresh);
    }
}
