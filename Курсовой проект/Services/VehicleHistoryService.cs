using Microsoft.EntityFrameworkCore;
using Курсовой_проект.Models;

namespace Курсовой_проект.Services;

public sealed record VehicleHistory(string Title, string Summary, IReadOnlyList<CatalogRow> Assignments,
    IReadOnlyList<CatalogRow> Documents, IReadOnlyList<CatalogRow> Maintenance);

public sealed class VehicleHistoryService
{
    private readonly AccessService access;
    private readonly UserSession session;
    private readonly Func<OzvsKursachContext> createContext;
    public VehicleHistoryService(AccessService access, UserSession session) : this(access, session, DatabaseFactory.Create) { }
    internal VehicleHistoryService(AccessService access, UserSession session, Func<OzvsKursachContext> createContext)
    { this.access = access; this.session = session; this.createContext = createContext; }
    public async Task<VehicleHistory> LoadAsync(int vehicleId)
    {
        access.Require(Permission.ViewVehicles);
        access.Require(Permission.ViewDrivers);
        access.Require(Permission.ViewDocuments);
        access.Require(Permission.ViewMaintenance);
        var actor = session.Current!;
        await using var db = createContext();
        async Task CheckActor()
        {
            var role = await db.Users.Where(x => x.UserId == actor.UserId).Select(x => x.Role.Name).SingleOrDefaultAsync();
            if (role != actor.Role || !ReferenceEquals(actor, session.Current)) throw new UnauthorizedAccessException();
        }
        await CheckActor();
        var car = await db.Vehicles.AsNoTracking().SingleOrDefaultAsync(x => x.VehicleId == vehicleId)
            ?? throw new InvalidOperationException("Автомобиль удалён. Обновите список транспорта.");
        var assignments = await db.VehicleAssignments.AsNoTracking().Include(x => x.Driver)
            .Where(x => x.VehicleId == vehicleId).OrderByDescending(x => x.StartDate).ThenByDescending(x => x.AssignmentId).ToListAsync();
        var documents = await db.Documents.AsNoTracking().Where(x => x.VehicleId == vehicleId)
            .OrderByDescending(x => x.IssueDate).ThenByDescending(x => x.DocumentId).ToListAsync();
        var works = await db.MaintenanceRepairs.AsNoTracking().Include(x => x.WorkParts).ThenInclude(x => x.Part)
            .Where(x => x.VehicleId == vehicleId).OrderByDescending(x => x.WorkDate).ThenByDescending(x => x.WorkId).ToListAsync();
        await CheckActor();
        var today = DateOnly.FromDateTime(DateTime.Today);
        string Expiry(DateOnly? date) => !date.HasValue ? "Без срока" : date < today ? "Просрочен" : date <= today.AddDays(30) ? "Скоро истекает" : "Действует";
        var current = assignments.Where(x => x.StartDate <= today && (!x.EndDate.HasValue || x.EndDate >= today))
            .Select(x => $"{x.Driver.LastName} {x.Driver.FirstName} {x.Driver.MiddleName}".Trim()).ToList();
        return new($"{car.LicensePlate} · {car.Brand} {car.Model}",
            $"{car.Status} · {car.YearOfManufacture} г. · {car.Mileage:N0} км\n" +
            $"Текущие водители: {(current.Count == 0 ? "не закреплены" : string.Join("; ", current))}\n" +
            $"Работ: {works.Count} · Затраты на ТО и ремонты: {works.Sum(x => x.Cost):N2} ₽",
            assignments.Select(x => new CatalogRow($"{x.Driver.LastName} {x.Driver.FirstName} {x.Driver.MiddleName}".Trim(),
                $"С {x.StartDate:dd.MM.yyyy} по {(x.EndDate.HasValue ? x.EndDate.Value.ToString("dd.MM.yyyy") : "настоящее время / без даты окончания")}",
                $"Удостоверение: {x.Driver.LicenseNumber} · Телефон: {x.Driver.Phone ?? "не указан"}",
                x.StartDate > today ? "Запланировано" : x.EndDate < today ? "Завершено" : "Действует")).ToList(),
            documents.Select(x => new CatalogRow($"{x.DocumentType} · {x.DocumentNumber}",
                $"Выдан {x.IssueDate:dd.MM.yyyy} · До: {(x.ExpiryDate.HasValue ? x.ExpiryDate.Value.ToString("dd.MM.yyyy") : "не указано")}",
                $"Организация: {x.IssuingOrganization ?? "не указана"}\nСтоимость: {(x.Cost.HasValue ? x.Cost.Value.ToString("N2") + " ₽" : "не указана")}",
                x.IssueDate > today ? "Ещё не действует" : Expiry(x.ExpiryDate))).ToList(),
            works.Select(x => new CatalogRow(x.WorkType, $"{x.WorkDate:dd.MM.yyyy} · {x.Mileage:N0} км · {x.Cost:N2} ₽",
                $"{x.Description}\nЗапчасти: {(x.WorkParts.Count == 0 ? "не указаны" : string.Join("; ", x.WorkParts.Select(p => $"{p.Part.Name} ({p.Part.ArticleNumber}) — {p.Quantity} шт.")))}\n" +
                $"Следующее ТО: {(x.NextMaintenanceDate.HasValue ? x.NextMaintenanceDate.Value.ToString("dd.MM.yyyy") : "дата не указана")}; пробег: {x.NextMaintenanceMileage?.ToString() ?? "не указан"}",
                "Обслуживание")).ToList());
    }
}
