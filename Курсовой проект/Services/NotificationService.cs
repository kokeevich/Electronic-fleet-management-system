using Microsoft.EntityFrameworkCore;
using Курсовой_проект.Models;

namespace Курсовой_проект.Services;

public sealed record Notice(string Key, string Title, string Message, string Category, bool Overdue,
    int? VehicleId, bool IsRead = false)
{
    public string Badge => Overdue ? "Просрочено" : "Приближается";
    public string Color => Overdue ? "#B42318" : "#996000";
    public string ReadLabel => IsRead ? "Прочитано" : "Новое";
}
public sealed class NotificationService
{
    private readonly AccessService access;
    private readonly UserSession session;
    private readonly Func<OzvsKursachContext> createContext;
    public bool CanRead => access.Can(Permission.ViewNotifications);
    public static event Action? DataChanged;
    public static void NotifyDataChanged() => DataChanged?.Invoke();
    public NotificationService(AccessService access, UserSession session) : this(access, session, DatabaseFactory.Create) { }
    internal NotificationService(AccessService access, UserSession session, Func<OzvsKursachContext> createContext)
    { this.access = access; this.session = session; this.createContext = createContext; }
    private async Task<SessionUser> Check(OzvsKursachContext db)
    {
        access.Require(Permission.ViewNotifications);
        var actor = session.Current!;
        var role = await db.Users.Where(x => x.UserId == actor.UserId).Select(x => x.Role.Name).SingleOrDefaultAsync();
        if (role != actor.Role || !ReferenceEquals(actor, session.Current)) throw new UnauthorizedAccessException();
        return actor;
    }
    public async Task<List<Notice>> LoadAsync()
    {
        await using var db = createContext();
        var actor = await Check(db);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var docs = actor.Role == "Механик" ? new List<Document>() : await db.Documents.AsNoTracking().Include(x => x.Vehicle)
            .Where(x => x.VehicleId != null && (x.DocumentType == "ОСАГО" || x.DocumentType == "КАСКО")).ToListAsync();
        var drivers = actor.Role == "Механик" ? new List<Driver>() : await db.Drivers.AsNoTracking().ToListAsync();
        var works = actor.Role == "Диспетчер" ? new List<MaintenanceRepair>() : await db.MaintenanceRepairs.AsNoTracking()
            .Include(x => x.Vehicle).ToListAsync();
        var notices = Build(today, docs, drivers, works);
        var read = (await db.Set<NotificationRead>().AsNoTracking().Where(x => x.UserId == actor.UserId)
            .Select(x => x.EventKey).ToListAsync()).ToHashSet();
        await Check(db);
        if (!ReferenceEquals(actor, session.Current)) throw new UnauthorizedAccessException();
        return notices.Select(x => x with { IsRead = read.Contains(x.Key) }).ToList();
    }
    public async Task MarkReadAsync(string key)
    {
        // Повторно проверяем, что событие существует и доступно текущей роли.
        var actor = session.Current;
        var notices = await LoadAsync();
        if (!ReferenceEquals(actor, session.Current) || !notices.Any(x => x.Key == key)) throw new UnauthorizedAccessException();
        await using var db = createContext();
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        await Check(db);
        if (!ReferenceEquals(actor, session.Current)) throw new UnauthorizedAccessException();
        if (!await db.Set<NotificationRead>().AnyAsync(x => x.UserId == actor!.UserId && x.EventKey == key))
        {
            db.Add(new NotificationRead { UserId = actor!.UserId, EventKey = key, ReadAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        await tx.CommitAsync();
    }
    internal static List<Notice> Build(DateOnly today, IEnumerable<Document> documents, IEnumerable<Driver> drivers,
        IEnumerable<MaintenanceRepair> works)
    {
        var result = new List<Notice>();
        void DateNotice(string key, string title, string category, DateOnly date, int? vehicleId)
        {
            var days = date.DayNumber - today.DayNumber;
            if (days > 30) return;
            var overdue = days < 0;
            result.Add(new($"{key}:{date:yyyyMMdd}:{overdue}", title,
                $"Срок: {date:dd.MM.yyyy}. " + (overdue ? $"Просрочено на {-days} дн." : days == 0 ? "Срок наступает сегодня." : $"Осталось {days} дн."),
                category, overdue, vehicleId));
        }
        // Будущий полис ещё не заменяет действующий. Среди вступивших в силу берём последний.
        foreach (var group in documents.Where(x => x.IssueDate <= today).GroupBy(x => (x.VehicleId, x.DocumentType)))
        {
            var doc = group.OrderByDescending(x => x.IssueDate).ThenByDescending(x => x.ExpiryDate).ThenByDescending(x => x.DocumentId).First();
            if (doc.ExpiryDate is { } date)
                DateNotice($"doc:{doc.DocumentId}", $"{doc.DocumentType} · {doc.Vehicle?.LicensePlate} · {doc.DocumentNumber}", "Страхование", date, doc.VehicleId);
        }
        foreach (var driver in drivers)
            DateNotice($"driver:{driver.DriverId}:{driver.LicenseNumber}", $"Удостоверение · {driver.LastName} {driver.FirstName} {driver.MiddleName}",
                "Удостоверения", driver.LicenseExpiryDate, null);
        // Последняя выполненная работа определяет актуальный план следующего ТО.
        foreach (var group in works.Where(x => x.WorkDate <= today).GroupBy(x => x.VehicleId))
        {
            var work = group.OrderByDescending(x => x.WorkDate).ThenByDescending(x => x.WorkId).First();
            if (work.NextMaintenanceDate is { } date)
                DateNotice($"maintenance-date:{work.WorkId}", $"Плановое ТО · {work.Vehicle.LicensePlate}", "ТО", date, work.VehicleId);
            if (work.NextMaintenanceMileage is { } target)
            {
                var remaining = (long)target - work.Vehicle.Mileage;
                if (remaining <= 1000)
                    result.Add(new($"maintenance-mileage:{work.WorkId}:{target}:{remaining < 0}",
                        $"ТО по пробегу · {work.Vehicle.LicensePlate}",
                        $"План: {target:N0} км. Текущий пробег: {work.Vehicle.Mileage:N0} км. " +
                        (remaining < 0 ? $"Превышение: {-remaining:N0} км." : $"Осталось {remaining:N0} км."),
                        "ТО", remaining < 0, work.VehicleId));
            }
        }
        return result.OrderByDescending(x => x.Overdue).ThenBy(x => x.Category).ThenBy(x => x.Title).ToList();
    }
    public static string Csv(IEnumerable<Notice> notices)
    {
        static string Cell(string value)
        {
            if (value.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@') value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return "Категория;Событие;Состояние;Подробности\r\n" + string.Join("\r\n", notices.Select(x =>
            string.Join(";", new[] { x.Category, x.Title, x.Badge, x.Message }.Select(Cell))));
    }
}
