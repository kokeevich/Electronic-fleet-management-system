using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Курсовой_проект.Models;
using Курсовой_проект.Services;
using Курсовой_проект.ViewModels;

namespace FleetManagement.Tests;

public class CatalogEditTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly DbContextOptions<OzvsKursachContext> options;
    private readonly UserSession session = new();
    private readonly CatalogEditService service;
    private readonly AccessService access;
    private static readonly (CatalogSection Section, int Id)[] Records =
    [
        (CatalogSection.Drivers, 10), (CatalogSection.Assignments, 20), (CatalogSection.Documents, 30),
        (CatalogSection.Maintenance, 40), (CatalogSection.Parts, 50), (CatalogSection.WorkParts, 60),
        (CatalogSection.Users, 5), (CatalogSection.Roles, 4)
    ];
    public CatalogEditTests()
    {
        connection.Open();
        options = new DbContextOptionsBuilder<OzvsKursachContext>().UseSqlite(connection).Options;
        access = new AccessService(session);
        service = new CatalogEditService(access, session, () => new OzvsKursachContext(options));
        using var db = new OzvsKursachContext(options);
        db.Database.EnsureCreated();
        db.Roles.AddRange(new Role { RoleId = 1, Name = "Администратор" },
            new Role { RoleId = 2, Name = "Диспетчер" }, new Role { RoleId = 3, Name = "Механик" },
            new Role { RoleId = 4, Name = "Наблюдатель" });
        for (var id = 1; id <= 5; id++)
            db.Users.Add(new User { UserId = id, RoleId = Math.Min(id, 4), FirstName = "Иван", LastName = "Тестов",
                Login = "test" + id, Password = "HASH-MUST-NOT-CHANGE" });
        db.Drivers.Add(new Driver { DriverId = 10, LastName = "Тестов", FirstName = "Олег",
            LicenseNumber = "D-1", LicenseCategory = "B", LicenseIssueDate = new(2020, 1, 1), LicenseExpiryDate = new(2030, 1, 1) });
        db.Vehicles.Add(new Vehicle { VehicleId = 10, LicensePlate = "А123БВ196", Brand = "Kia",
            Model = "Rio", YearOfManufacture = 2021, Mileage = 1000, Status = "В эксплуатации" });
        db.VehicleAssignments.Add(new VehicleAssignment { AssignmentId = 20, VehicleId = 10, DriverId = 10,
            StartDate = new(2026, 1, 1), EndDate = new(2026, 6, 1) });
        db.Documents.Add(new Document { DocumentId = 30, VehicleId = 10, DocumentType = "ОСАГО",
            DocumentNumber = "P-1", IssueDate = new(2026, 1, 1), ExpiryDate = new(2027, 1, 1), Cost = 100 });
        db.MaintenanceRepairs.Add(new MaintenanceRepair { WorkId = 40, VehicleId = 10, WorkDate = new(2026, 1, 1),
            WorkType = "ТО", Mileage = 1000, Cost = 1000 });
        db.Parts.Add(new Part { PartId = 50, Name = "Фильтр", ArticleNumber = "F-1", Cost = 100, Quantity = 10 });
        db.WorkParts.Add(new WorkPart { CompositionId = 60, WorkId = 40, PartId = 50, Quantity = 1 });
        db.SaveChanges();
        session.SignIn(new SessionUser(1, "Администратор", "Администратор"));
    }
    public void Dispose() => connection.Dispose();
    private async Task<CatalogEditResult> Change(CatalogSection section, int id, string key, string value)
    {
        var data = await service.LoadAsync(section, id);
        var values = data.Original.ToDictionary(x => x.Key, x => x.Value);
        values[key] = value;
        return await service.SaveAsync(data, values);
    }

    [Fact]
    public async Task NotificationsRespectRolesReadStateAndResolvedEvents()
    {
        await using var db = new OzvsKursachContext(options);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var document = await db.Documents.FindAsync(30);
        document!.IssueDate = today.AddDays(-100); document.ExpiryDate = today.AddDays(5);
        var driver = await db.Drivers.FindAsync(10);
        driver!.LicenseExpiryDate = today.AddDays(10);
        var work = await db.MaintenanceRepairs.FindAsync(40);
        work!.WorkDate = today.AddDays(-10); work.NextMaintenanceDate = today.AddDays(20);
        work.NextMaintenanceMileage = 1500;
        await db.SaveChangesAsync();
        var service = new NotificationService(access, session, () => new OzvsKursachContext(options));
        var all = await service.LoadAsync();
        Assert.Equal(4, all.Count);
        var insurance = all.Single(x => x.Category == "Страхование");
        await service.MarkReadAsync(insurance.Key);
        await service.MarkReadAsync(insurance.Key);
        Assert.Single(await db.Set<NotificationRead>().ToListAsync());
        Assert.True((await service.LoadAsync()).Single(x => x.Key == insurance.Key).IsRead);
        session.SignIn(new SessionUser(2, "Диспетчер", "Диспетчер"));
        var dispatcher = await service.LoadAsync();
        Assert.Equal(2, dispatcher.Count);
        Assert.All(dispatcher, x => Assert.False(x.IsRead));
        session.SignIn(new SessionUser(3, "Механик", "Механик"));
        Assert.All(await service.LoadAsync(), x => Assert.Equal("ТО", x.Category));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.MarkReadAsync(insurance.Key));
        session.SignIn(new SessionUser(4, "Наблюдатель", "Наблюдатель"));
        Assert.Equal(4, (await service.LoadAsync()).Count);
        document.ExpiryDate = today.AddYears(1);
        await db.SaveChangesAsync();
        Assert.DoesNotContain(await service.LoadAsync(), x => x.Category == "Страхование");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.MarkReadAsync(insurance.Key));
        var actor = await db.Users.FindAsync(4); actor!.RoleId = 3; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.LoadAsync());
    }

    [Fact]
    public void NotificationRulesSuppressReplacedPoliciesAndUseLatestWorkPlan()
    {
        var today = new DateOnly(2026, 10, 26);
        var car = new Vehicle { VehicleId = 10, LicensePlate = "TEST", Mileage = 10000 };
        var docs = new[] {
            new Document { DocumentId = 1, VehicleId = 10, Vehicle = car, DocumentType = "ОСАГО", IssueDate = today.AddYears(-1), ExpiryDate = today.AddDays(-1) },
            new Document { DocumentId = 2, VehicleId = 10, Vehicle = car, DocumentType = "ОСАГО", IssueDate = today, ExpiryDate = today.AddYears(1) }
        };
        var works = new[] {
            new MaintenanceRepair { WorkId = 1, VehicleId = 10, Vehicle = car, WorkDate = today.AddDays(-20), NextMaintenanceDate = today.AddDays(-1) },
            new MaintenanceRepair { WorkId = 2, VehicleId = 10, Vehicle = car, WorkDate = today, NextMaintenanceDate = today.AddDays(30), NextMaintenanceMileage = 11000 }
        };
        var rows = NotificationService.Build(today, docs, [], works);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, x => Assert.False(x.Overdue));
        var overdue = NotificationService.Build(today.AddDays(31), docs, [], works);
        Assert.Contains(overdue, x => x.Overdue);
        Assert.NotEqual(rows.Single(x => x.Key.StartsWith("maintenance-date")).Key,
            overdue.Single(x => x.Key.StartsWith("maintenance-date")).Key);
        works[1].NextMaintenanceDate = today.AddDays(31);
        works[1].NextMaintenanceMileage = 11001;
        Assert.Empty(NotificationService.Build(today, docs, [], works));
    }

    [Fact]
    public async Task VehicleHistoryContainsOnlySelectedCarAndRejectsRevokedSession()
    {
        await using var db = new OzvsKursachContext(options);
        db.Vehicles.Add(new Vehicle { VehicleId = 11, LicensePlate = "OTHER", Brand = "Kia", Model = "Rio",
            YearOfManufacture = 2020, Status = "В ремонте", VinNumber = "OTHER-VIN" });
        db.MaintenanceRepairs.Add(new MaintenanceRepair { VehicleId = 11, WorkDate = new(2026, 1, 1),
            WorkType = "OTHER-WORK", Cost = 9999, Mileage = 20 });
        await db.SaveChangesAsync();
        var service = new VehicleHistoryService(access, session, () => new OzvsKursachContext(options));
        var history = await service.LoadAsync(10);
        Assert.Single(history.Assignments);
        Assert.Single(history.Documents);
        Assert.Single(history.Maintenance);
        Assert.Contains("Фильтр", history.Maintenance[0].Details);
        Assert.DoesNotContain("OTHER-WORK", history.Maintenance[0].Title);
        var empty = await service.LoadAsync(11);
        Assert.Empty(empty.Assignments);
        Assert.Empty(empty.Documents);
        Assert.Single(empty.Maintenance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LoadAsync(999));
        session.SignIn(new SessionUser(4, "Наблюдатель", "Наблюдатель"));
        Assert.Single((await service.LoadAsync(10)).Maintenance);
        var user = await db.Users.FindAsync(4);
        user!.RoleId = 3;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.LoadAsync(10));
    }

    [Fact]
    public async Task FleetReportUsesCurrentAssignmentsCountsVehiclesAndChecksActualRole()
    {
        await using var db = new OzvsKursachContext(options);
        var assignment = await db.VehicleAssignments.FindAsync(20);
        assignment!.StartDate = DateOnly.FromDateTime(DateTime.Today);
        assignment.EndDate = assignment.StartDate;
        db.Vehicles.Add(new Vehicle { VehicleId = 11, LicensePlate = "TEST-2", Brand = "Kia", Model = "Rio",
            YearOfManufacture = 2020, Mileage = 5000, Status = "В ремонте", VinNumber = "SECOND" });
        await db.SaveChangesAsync();
        var reports = new FleetReportService(access, session, () => new OzvsKursachContext(options));
        var report = await reports.GenerateAsync();
        Assert.Equal(2, report.Rows.Count);
        Assert.Contains("Тестов Олег", report.Rows.Single(x => x.Plate != "TEST-2").Drivers);
        Assert.Equal("Не закреплён", report.Rows.Single(x => x.Plate == "TEST-2").Drivers);
        Assert.Contains("В ремонте: 1", report.Summary);
        Assert.Single((await reports.GenerateAsync("В ремонте")).Rows);
        assignment.EndDate = assignment.StartDate.AddDays(-1);
        assignment.StartDate = assignment.StartDate.AddDays(-5);
        await db.SaveChangesAsync();
        Assert.All((await reports.GenerateAsync()).Rows, row => Assert.Equal("Не закреплён", row.Drivers));
        var exported = FleetReportService.Csv(new FleetReport(DateTime.Now, null,
            [new("=1+1", "Kia", "Rio", 2020, 100, "В ремонте", "Имя; Фамилия")]));
        Assert.Contains("\"'=1+1\"", exported);
        Assert.Contains("\"Имя; Фамилия\"", exported);
        var actor = await db.Users.FindAsync(1);
        actor!.RoleId = 4;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reports.GenerateAsync());
    }

    [Fact]
    public async Task PasswordResetRequiresAdministratorAndNeverLogsSecrets()
    {
        const string password = "NewPassword!2026";
        Assert.False((await service.ResetPasswordAsync(5, password, "different")).Success);
        Assert.True((await service.ResetPasswordAsync(5, password, password)).Success);
        await using var db = new OzvsKursachContext(options);
        var user = await db.Users.FindAsync(5);
        Assert.True(PasswordHasher.Verify(password, user!.Password));
        var log = Assert.Single(await db.AuditLogs.ToListAsync());
        Assert.Null(log.BeforeData);
        Assert.DoesNotContain(password, log.AfterData!);
        Assert.DoesNotContain(user.Password, log.AfterData!);
        session.SignIn(new SessionUser(2, "Диспетчер", "Диспетчер"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ResetPasswordAsync(5, password, password));
        session.SignIn(new SessionUser(1, "Администратор", "Администратор"));
        var admin = await db.Users.FindAsync(1);
        admin!.RoleId = 4;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ResetPasswordAsync(5, password, password));
    }

    [Fact]
    public async Task CreateAndDeleteEveryMutableRegistryAndHashNewPassword()
    {
        foreach (var (section, id) in Records.Where(x => x.Section != CatalogSection.Roles))
        {
            var template = await service.LoadAsync(section, id);
            var draft = await service.LoadAsync(section, 0);
            var values = template.Original.ToDictionary(x => x.Key, x => x.Value);
            switch (section)
            {
                case CatalogSection.Drivers: values["LicenseNumber"] = "NEW-LICENSE"; break;
                case CatalogSection.Assignments:
                    values["StartDate"] = "01.01.2028"; values["EndDate"] = ""; break;
                case CatalogSection.Parts: values["ArticleNumber"] = "NEW-PART"; break;
                case CatalogSection.WorkParts:
                    await using (var db = new OzvsKursachContext(options))
                    {
                        db.Parts.Add(new Part { PartId = 99, Name = "Новая", ArticleNumber = "P99", Cost = 10 });
                        await db.SaveChangesAsync();
                    }
                    values["PartId"] = "99"; break;
                case CatalogSection.Users:
                    values["Login"] = "new_user";
                    values["Password"] = "NewPassword!2026";
                    values["PasswordConfirmation"] = "wrong";
                    Assert.False((await service.SaveAsync(draft, values)).Success);
                    values["PasswordConfirmation"] = values["Password"];
                    break;
            }
            Assert.True((await service.SaveAsync(draft, values)).Success, section.ToString());
            var rows = await new CatalogService(access, session, () => new OzvsKursachContext(options)).ReadAsync(section);
            var newId = rows.Max(x => x.Id);
            Assert.True(newId > id);
            if (section == CatalogSection.Users)
            {
                await using var db = new OzvsKursachContext(options);
                var user = await db.Users.FindAsync(newId);
                Assert.True(PasswordHasher.Verify(values["Password"], user!.Password));
            }
            var created = await service.LoadAsync(section, newId);
            Assert.True((await service.DeleteAsync(created)).Success, section.ToString());
            Assert.DoesNotContain(await new CatalogService(access, session, () => new OzvsKursachContext(options)).ReadAsync(section),
                x => x.Id == newId);
        }
    }

    [Fact]
    public async Task DeleteProtectsRelationsSelfAndStaleRecordsAndFixedRoles()
    {
        foreach (var (section, id) in new[] { (CatalogSection.Drivers, 10), (CatalogSection.Maintenance, 40),
            (CatalogSection.Parts, 50), (CatalogSection.Users, 1), (CatalogSection.Roles, 4) })
            Assert.False((await service.DeleteAsync(await service.LoadAsync(section, id))).Success);
        var stale = await service.LoadAsync(CatalogSection.Documents, 30);
        Assert.True((await Change(CatalogSection.Documents, 30, "DocumentNumber", "CHANGED")).Success);
        Assert.False((await service.DeleteAsync(stale)).Success);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.LoadAsync(CatalogSection.Roles, 0));
        Assert.True((await service.DeleteAsync(await service.LoadAsync(CatalogSection.WorkParts, 60))).Success);
        Assert.True((await service.DeleteAsync(await service.LoadAsync(CatalogSection.Parts, 50))).Success);
    }

    [Fact]
    public async Task CreateAndDeleteEnforcePermissionsEvenWithPreparedData()
    {
        var driver = await service.LoadAsync(CatalogSection.Drivers, 10);
        var draft = await service.LoadAsync(CatalogSection.Drivers, 0);
        session.SignIn(new SessionUser(3, "Механик", "Механик"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.LoadAsync(CatalogSection.Drivers, 0));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveAsync(draft, driver.Original));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteAsync(driver));
        var part = await service.LoadAsync(CatalogSection.Parts, 0);
        var values = new Dictionary<string, string> { ["Name"] = "Фильтр", ["ArticleNumber"] = "M-NEW",
            ["Cost"] = "10", ["Quantity"] = "2", ["Category"] = "" };
        Assert.True((await service.SaveAsync(part, values)).Success);
        await using (var db = new OzvsKursachContext(options))
        {
            var created = await db.Parts.SingleAsync(x => x.ArticleNumber == "M-NEW");
            Assert.True((await service.DeleteAsync(await service.LoadAsync(CatalogSection.Parts, created.PartId))).Success);
        }
        session.SignIn(new SessionUser(4, "Наблюдатель", "Наблюдатель"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.LoadAsync(CatalogSection.Parts, 0));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteAsync(driver));
    }

    [Fact]
    public async Task EveryEnabledRegistryCanBeEditedAndPasswordsAndRoleNamesArePreserved()
    {
        var catalog = new CatalogViewModel(new CatalogService(access, session, () => new OzvsKursachContext(options)), access, service);
        await catalog.LoadAsync();
        catalog.SelectedRow = Assert.Single(catalog.Rows);
        Assert.True(catalog.CanEditRecord);
        Assert.NotNull(catalog.CreateEditor());
        var changes = new[]
        {
            ("Phone", "987"), ("EndDate", "02.06.2026"), ("Cost", "125.50"),
            ("Description", "Строка 1\nСтрока 2"), ("Category", "Фильтры"), ("Quantity", "3"),
            ("FirstName", "Пётр"), ("Description", "Только просмотр")
        };
        for (var i = 0; i < Records.Length; i++)
        {
            var (section, id) = Records[i];
            var (key, value) = changes[i];
            Assert.True((await Change(section, id, key, value)).Success);
            var data = await service.LoadAsync(section, id);
            Assert.Equal(key == "Cost" ? "125.5" : value, data.Original[key]);
            Assert.DoesNotContain(data.Fields, x => x.Key == "Password");
        }
        using var db = new OzvsKursachContext(options);
        Assert.All(await db.Users.ToListAsync(), x => Assert.Equal("HASH-MUST-NOT-CHANGE", x.Password));
        Assert.Equal("Наблюдатель", (await db.Roles.FindAsync(4))!.Name);
        Assert.Equal(10, (await db.Parts.FindAsync(50))!.Quantity); // состав работ не списывает склад автоматически
        Assert.Equal(1000m, (await db.MaintenanceRepairs.FindAsync(40))!.Cost);
    }

    [Fact]
    public async Task PermissionsAreEnforcedForLoadAndSaveAcrossAllRegistries()
    {
        var snapshots = new List<CatalogEditData>();
        foreach (var (section, id) in Records) snapshots.Add(await service.LoadAsync(section, id));
        foreach (var (id, role, allowed) in new[]
        {
            (2, "Диспетчер", new[] { CatalogSection.Drivers, CatalogSection.Assignments, CatalogSection.Documents }),
            (3, "Механик", new[] { CatalogSection.Maintenance, CatalogSection.Parts, CatalogSection.WorkParts }),
            (4, "Наблюдатель", Array.Empty<CatalogSection>())
        })
        {
            session.SignIn(new SessionUser(id, "Тест", role));
            foreach (var snapshot in snapshots)
            {
                if (allowed.Contains(snapshot.Section))
                {
                    Assert.NotEmpty((await service.LoadAsync(snapshot.Section, snapshot.Id)).Fields);
                    Assert.True((await service.SaveAsync(snapshot, snapshot.Original)).Success);
                }
                else
                {
                    await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.LoadAsync(snapshot.Section, snapshot.Id));
                    await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveAsync(snapshot, snapshot.Original));
                }
            }
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.LoadAsync(CatalogSection.Notifications, 1));
        }
    }

    [Fact]
    public async Task InvalidValuesDuplicatesAndOverlappingAssignmentsDoNotSave()
    {
        Assert.False((await Change(CatalogSection.Drivers, 10, "FirstName", "")).Success);
        Assert.False((await Change(CatalogSection.Drivers, 10, "LicenseExpiryDate", "01.01.2010")).Success);
        Assert.False((await Change(CatalogSection.Documents, 30, "VehicleId", "999")).Success);
        Assert.False((await Change(CatalogSection.Documents, 30, "ExpiryDate", "")).Success);
        Assert.False((await Change(CatalogSection.Documents, 30, "Cost", "1,234")).Success);
        Assert.False((await Change(CatalogSection.Maintenance, 40, "NextMaintenanceMileage", "500")).Success);
        Assert.False((await Change(CatalogSection.Parts, 50, "Quantity", "-1")).Success);
        Assert.False((await Change(CatalogSection.WorkParts, 60, "Quantity", "0")).Success);
        Assert.False((await Change(CatalogSection.Users, 5, "Login", "test1")).Success);
        using (var db = new OzvsKursachContext(options))
        {
            db.VehicleAssignments.Add(new VehicleAssignment { VehicleId = 10, DriverId = 10,
                StartDate = new(2026, 6, 2), EndDate = new(2026, 12, 1) });
            db.Parts.Add(new Part { Name = "Другая", ArticleNumber = "F-2", Cost = 50 });
            await db.SaveChangesAsync();
        }
        Assert.False((await Change(CatalogSection.Assignments, 20, "EndDate", "02.06.2026")).Success);
        Assert.False((await Change(CatalogSection.Parts, 50, "ArticleNumber", "F-2")).Success);
        Assert.Equal("01.06.2026", (await service.LoadAsync(CatalogSection.Assignments, 20)).Original["EndDate"]);
        Assert.Equal("Иван", (await service.LoadAsync(CatalogSection.Users, 5)).Original["FirstName"]);
    }

    [Fact]
    public async Task StaleSnapshotsAndRevokedRolesCannotWrite()
    {
        var old = await service.LoadAsync(CatalogSection.Parts, 50);
        Assert.True((await Change(CatalogSection.Parts, 50, "Name", "Новый фильтр")).Success);
        Assert.False((await service.SaveAsync(old, old.Original)).Success);
        Assert.False((await Change(CatalogSection.Users, 1, "RoleId", "4")).Success);
        using (var db = new OzvsKursachContext(options))
        {
            var admin = (await db.Users.FindAsync(1))!;
            admin.RoleId = 4;
            await db.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.LoadAsync(CatalogSection.Parts, 50));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveAsync(old, old.Original));
    }

    private sealed class PendingEditor : ICatalogEditService
    {
        public Task<CatalogEditResult> DeleteAsync(CatalogEditData original) => throw new NotSupportedException();
        public TaskCompletionSource<CatalogEditResult> Pending { get; } = new();
        public int Saves;
        public Task<CatalogEditData> LoadAsync(CatalogSection section, int id) => Task.FromResult(
            new CatalogEditData(section, id, "Запчасть", new[] { new EditField(new("Name", "Название"), "Фильтр", Array.Empty<EditOption>()) },
                new Dictionary<string, string> { ["Name"] = "Фильтр" }));
        public Task<CatalogEditResult> SaveAsync(CatalogEditData data, IReadOnlyDictionary<string, string> values)
        { Saves++; return Pending.Task; }
    }
    [Fact]
    public async Task EditorPreventsDuplicateSubmissionAndHidesTechnicalErrors()
    {
        var pending = new PendingEditor();
        var vm = new CatalogEditorViewModel(pending, CatalogSection.Parts, 50);
        await vm.LoadAsync();
        Assert.True(vm.CanSave);
        var first = vm.SaveAsync();
        Assert.False(vm.CanClose);
        Assert.False(await vm.SaveAsync());
        Assert.Equal(1, pending.Saves);
        pending.Pending.SetException(new Exception("Server=private;Password=secret"));
        Assert.False(await first);
        Assert.DoesNotContain("secret", vm.Message);
        Assert.True(vm.CanSave);
    }
}
