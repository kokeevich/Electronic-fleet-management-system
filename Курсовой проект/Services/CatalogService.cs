using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Курсовой_проект.Models;

namespace Курсовой_проект.Services;

public enum CatalogSection { Drivers, Assignments, Documents, Maintenance, Parts, WorkParts, Notifications, Users, Roles }
public sealed record CatalogDefinition(CatalogSection Section, string Title, Permission Permission)
{
    public static IReadOnlyList<CatalogDefinition> All { get; } = new[]
    {
        new CatalogDefinition(CatalogSection.Drivers, "Водители", Permission.ViewDrivers),
        new CatalogDefinition(CatalogSection.Assignments, "Закрепления", Permission.ViewDrivers),
        new CatalogDefinition(CatalogSection.Documents, "Страхование и документы", Permission.ViewDocuments),
        new CatalogDefinition(CatalogSection.Maintenance, "ТО и ремонты", Permission.ViewMaintenance),
        new CatalogDefinition(CatalogSection.Parts, "Запчасти", Permission.ViewParts),
        new CatalogDefinition(CatalogSection.WorkParts, "Состав работ", Permission.ViewParts),
        new CatalogDefinition(CatalogSection.Notifications, "Уведомления", Permission.ViewNotifications),
        new CatalogDefinition(CatalogSection.Users, "Пользователи", Permission.ManageUsers),
        new CatalogDefinition(CatalogSection.Roles, "Роли", Permission.ManageUsers)
    };
}

// Только данные для просмотра. Сущности EF и пароли не передаются в интерфейс.
public sealed record CatalogRow(string Title, string Subtitle, string Details, string Badge)
{
    public string DetailsPreview => Details.Replace("\r", "").Replace("\n", " · ");
}
public interface ICatalogService
{
    Task<List<CatalogRow>> ReadAsync(CatalogSection section);
}

public sealed class CatalogService : ICatalogService
{
    private readonly AccessService access;
    private readonly UserSession session;
    private readonly Func<OzvsKursachContext> createContext;
    public CatalogService(AccessService access, UserSession session)
        : this(access, session, DatabaseFactory.Create) { }
    internal CatalogService(AccessService access, UserSession session, Func<OzvsKursachContext> createContext)
    {
        this.access = access;
        this.session = session;
        this.createContext = createContext;
    }

    public async Task<List<CatalogRow>> ReadAsync(CatalogSection section)
    {
        var definition = CatalogDefinition.All.SingleOrDefault(x => x.Section == section)
            ?? throw new UnauthorizedAccessException();
        access.Require(definition.Permission);
        var user = session.Current!;
        await using var db = createContext();
        List<CatalogRow> rows;
        switch (section)
        {
            case CatalogSection.Drivers:
                rows = (await db.Drivers.AsNoTracking().OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ToListAsync())
                    .Select(x => new CatalogRow(Name(x), $"Телефон: {Text(x.Phone)}",
                        $"Удостоверение: {x.LicenseNumber} · категории {x.LicenseCategory}\nВыдано: {Date(x.LicenseIssueDate)} · действует до {Date(x.LicenseExpiryDate)}",
                        Expiry(x.LicenseExpiryDate))).ToList();
                break;
            case CatalogSection.Assignments:
                rows = (await db.VehicleAssignments.AsNoTracking().Include(x => x.Vehicle).Include(x => x.Driver)
                    .OrderByDescending(x => x.StartDate).ToListAsync())
                    .Select(x => new CatalogRow(Car(x.Vehicle), Name(x.Driver),
                        $"Начало: {Date(x.StartDate)} · окончание: {Date(x.EndDate)}",
                        x.StartDate > Today ? "Запланировано" : x.EndDate.HasValue && x.EndDate < Today ? "Завершено" : "Действует")).ToList();
                break;
            case CatalogSection.Documents:
                rows = (await db.Documents.AsNoTracking().Include(x => x.Vehicle).Include(x => x.Driver)
                    .OrderBy(x => x.DocumentType).ThenBy(x => x.DocumentNumber).ToListAsync())
                    .Select(x => new CatalogRow($"{x.DocumentType} · {x.DocumentNumber}",
                        string.Join(" · ", new[] { x.Vehicle == null ? null : Car(x.Vehicle), x.Driver == null ? null : Name(x.Driver) }.Where(v => v != null)),
                        $"Выдан: {Date(x.IssueDate)} · до: {Date(x.ExpiryDate)}\nОрганизация: {Text(x.IssuingOrganization)} · стоимость: {Money(x.Cost)}",
                        Expiry(x.ExpiryDate))).ToList();
                break;
            case CatalogSection.Maintenance:
                rows = (await db.MaintenanceRepairs.AsNoTracking().Include(x => x.Vehicle)
                    .OrderByDescending(x => x.WorkDate).ToListAsync())
                    .Select(x => new CatalogRow(x.WorkType, Car(x.Vehicle),
                        $"Дата: {Date(x.WorkDate)} · пробег: {x.Mileage:N0} км · стоимость: {Money(x.Cost)}\n{Text(x.Description)}\nСледующее ТО: {Date(x.NextMaintenanceDate)} · пробег: {(x.NextMaintenanceMileage.HasValue ? x.NextMaintenanceMileage + " км" : "не задан")}",
                        "Выполненные работы")).ToList();
                break;
            case CatalogSection.Parts:
                rows = (await db.Parts.AsNoTracking().OrderBy(x => x.Name).ToListAsync())
                    .Select(x => new CatalogRow(x.Name, $"Артикул: {x.ArticleNumber} · категория: {Text(x.Category)}",
                        $"Цена: {Money(x.Cost)} · остаток: {x.Quantity} шт.", x.Quantity > 0 ? "В наличии" : "Нет в наличии")).ToList();
                break;
            case CatalogSection.WorkParts:
                rows = (await db.WorkParts.AsNoTracking().Include(x => x.Part).Include(x => x.Work).ThenInclude(x => x.Vehicle)
                    .OrderByDescending(x => x.Work.WorkDate).ThenBy(x => x.Part.Name).ToListAsync())
                    .Select(x => new CatalogRow(x.Part.Name, $"{Car(x.Work.Vehicle)} · {x.Work.WorkType}",
                        $"Работа №{x.WorkId} от {Date(x.Work.WorkDate)} · артикул: {x.Part.ArticleNumber}",
                        $"{x.Quantity} шт.")).ToList();
                break;
            case CatalogSection.Notifications:
                var notifications = db.Notifications.AsNoTracking().AsQueryable();
                if (user.Role != "Администратор")
                {
                    notifications = notifications.Where(x => x.UserId == null || x.UserId == user.UserId);
                    notifications = user.Role == "Механик"
                        ? notifications.Where(x => x.NotificationType == "Плановое ТО")
                        : notifications.Where(x => x.NotificationType != "Плановое ТО");
                }
                rows = (await notifications.OrderByDescending(x => x.CreatedDate).ToListAsync())
                    .Select(x => new CatalogRow(x.Title, x.NotificationType,
                        $"{x.Message}\nКритическая дата: {Date(x.CriticalDate)} · предупреждение за {x.DaysBefore} дн.\nСоздано: {x.CreatedDate:dd.MM.yyyy HH:mm} · {x.RelatedEntityType} №{x.RelatedEntityId}",
                        x.IsRead ? "Прочитано" : "Не прочитано")).ToList();
                break;
            case CatalogSection.Users:
                // SELECT не включает столбец Password.
                var users = await db.Users.AsNoTracking().OrderBy(x => x.LastName).ThenBy(x => x.FirstName)
                    .Select(x => new { x.LastName, x.FirstName, x.MiddleName, x.Login, Role = x.Role.Name }).ToListAsync();
                rows = users.Select(x => new CatalogRow(FullName(x.LastName, x.FirstName, x.MiddleName),
                    $"Логин: {x.Login}", "Учётная запись", x.Role)).ToList();
                break;
            case CatalogSection.Roles:
                rows = (await db.Roles.AsNoTracking().OrderBy(x => x.Name)
                    .Select(x => new { x.Name, x.Description, Count = x.Users.Count }).ToListAsync())
                    .Select(x => new CatalogRow(x.Name, Text(x.Description), $"Пользователей: {x.Count}", "Роль доступа")).ToList();
                break;
            default: throw new UnauthorizedAccessException();
        }
        access.Require(definition.Permission);
        if (!ReferenceEquals(user, session.Current)) throw new UnauthorizedAccessException();
        return rows;
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
    private static string Text(string? value) => string.IsNullOrWhiteSpace(value) ? "не указано" : value;
    private static string Date(DateOnly? value) => value?.ToString("dd.MM.yyyy") ?? "не задано";
    private static string Money(decimal? value) => value.HasValue ? value.Value.ToString("N2", CultureInfo.GetCultureInfo("ru-RU")) + " ₽" : "не указана";
    private static string FullName(string last, string first, string? middle) => string.Join(" ", new[] { last, first, middle }.Where(x => !string.IsNullOrWhiteSpace(x)));
    private static string Name(Driver driver) => FullName(driver.LastName, driver.FirstName, driver.MiddleName);
    private static string Car(Vehicle vehicle) => $"{vehicle.LicensePlate} · {vehicle.Brand} {vehicle.Model}";
    private static string Expiry(DateOnly? date) => !date.HasValue ? "Без срока окончания"
        : date < Today ? "Срок истёк" : date <= Today.AddDays(30) ? "Скоро истекает" : "Действует";
}
