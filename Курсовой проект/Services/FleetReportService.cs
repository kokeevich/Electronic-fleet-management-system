using Microsoft.EntityFrameworkCore;
using Курсовой_проект.Models;
using System.Globalization;

namespace Курсовой_проект.Services;

public sealed record FleetReportRow(string Plate, string Brand, string Model, int Year, int Mileage,
    string Status, string Drivers)
{
    public string Vehicle => $"{Plate} · {Brand} {Model}";
}
public sealed record FleetReport(DateTime Created, string? StatusFilter, IReadOnlyList<FleetReportRow> Rows)
{
    public string Summary => $"Автомобилей: {Rows.Count}\n" + string.Join(" · ",
        Rows.GroupBy(x => x.Status).OrderBy(x => x.Key).Select(x => $"{x.Key}: {x.Count()}"));
    public string Parameters => $"Состояние на {Created:dd.MM.yyyy HH:mm} · {StatusFilter ?? "Все статусы"}";
}
public sealed class FleetReportService
{
    private readonly AccessService access;
    private readonly UserSession session;
    private readonly Func<OzvsKursachContext> createContext;
    public FleetReportService(AccessService access, UserSession session) : this(access, session, DatabaseFactory.Create) { }
    internal FleetReportService(AccessService access, UserSession session, Func<OzvsKursachContext> createContext)
    { this.access = access; this.session = session; this.createContext = createContext; }
    public bool CanRead => access.Can(Permission.FleetReports);
    public async Task<FleetReport> GenerateAsync(string? status = null)
    {
        access.Require(Permission.FleetReports);
        var actor = session.Current!;
        await using var db = createContext();
        var actualRole = await db.Users.Where(x => x.UserId == actor.UserId).Select(x => x.Role.Name).SingleOrDefaultAsync();
        if (actualRole != actor.Role) throw new UnauthorizedAccessException();
        var created = DateTime.Now;
        var today = DateOnly.FromDateTime(created);
        var vehicles = await db.Vehicles.AsNoTracking()
            .Include(x => x.VehicleAssignments.Where(a => a.StartDate <= today && (a.EndDate == null || a.EndDate >= today)))
            .ThenInclude(x => x.Driver)
            .Where(x => status == null || x.Status == status).OrderBy(x => x.LicensePlate).ToListAsync();
        actualRole = await db.Users.Where(x => x.UserId == actor.UserId).Select(x => x.Role.Name).SingleOrDefaultAsync();
        if (actualRole != actor.Role || !ReferenceEquals(actor, session.Current)) throw new UnauthorizedAccessException();
        return new(created, status, vehicles.Select(x => new FleetReportRow(x.LicensePlate, x.Brand, x.Model,
            x.YearOfManufacture, x.Mileage, x.Status, DriverText(x.VehicleAssignments))).ToList());
    }
    private static string DriverText(IEnumerable<VehicleAssignment> assignments)
    {
        var rows = assignments.OrderBy(x => x.Driver.LastName).ThenBy(x => x.AssignmentId).ToList();
        if (rows.Count == 0) return "Не закреплён";
        return (rows.Count > 1 ? "Несколько активных закреплений: " : "") + string.Join("; ", rows.Select(x =>
            $"{x.Driver.LastName} {x.Driver.FirstName} {x.Driver.MiddleName}".Trim() +
            $" (ID {x.DriverId}, с {x.StartDate:dd.MM.yyyy}" +
            (x.EndDate.HasValue ? $" по {x.EndDate:dd.MM.yyyy})" : ", без даты окончания)")));
    }
    public static string Csv(FleetReport report)
    {
        static string Cell(string s)
        {
            if (s.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@') s = "'" + s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
        static string Line(params string[] cells) => string.Join(";", cells.Select(Cell));
        var lines = new List<string> { Line("Состояние автопарка"), Line(report.Parameters), Line(report.Summary),
            Line("Госномер", "Марка", "Модель", "Год выпуска", "Пробег, км", "Статус", "Текущий водитель") };
        lines.AddRange(report.Rows.Select(x => Line(x.Plate, x.Brand, x.Model, x.Year.ToString(CultureInfo.InvariantCulture),
            x.Mileage.ToString(CultureInfo.InvariantCulture), x.Status, x.Drivers)));
        return string.Join("\r\n", lines);
    }
}
