using Microsoft.EntityFrameworkCore;
using Курсовой_проект.Models;
using System.Globalization;

namespace Курсовой_проект.Services;

public sealed record ReportVehicle(int Id, string Name);
public sealed record MaintenanceReportRow(DateOnly Date, string Vehicle, string Work, int Mileage, decimal Cost, string Parts, string Description);
public sealed record MaintenanceReport(DateOnly From, DateOnly To, string Vehicle, DateTime Created,
    IReadOnlyList<MaintenanceReportRow> Rows)
{
    public decimal Total => Rows.Sum(x => x.Cost);
    public string Summary => $"Работ: {Rows.Count} · Расходы: {Total:N2} ₽";
}

public sealed class MaintenanceReportService(AccessService access, UserSession session)
{
    public bool CanRead => access.Can(Permission.MaintenanceReports) || access.Can(Permission.FleetReports);
    private async Task<SessionUser> CheckAsync(OzvsKursachContext db)
    {
        var actor = session.Current;
        if (!CanRead || actor == null) throw new UnauthorizedAccessException();
        var role = await db.Users.Where(x => x.UserId == actor.UserId).Select(x => x.Role.Name).SingleOrDefaultAsync();
        if (role != actor.Role || !ReferenceEquals(actor, session.Current)) throw new UnauthorizedAccessException();
        return actor;
    }
    public async Task<List<ReportVehicle>> VehiclesAsync()
    {
        await using var db = DatabaseFactory.Create();
        var actor = await CheckAsync(db);
        var rows = await db.Vehicles.AsNoTracking().OrderBy(x => x.LicensePlate)
            .Select(x => new ReportVehicle(x.VehicleId, x.LicensePlate + " · " + x.Brand + " " + x.Model)).ToListAsync();
        if (!ReferenceEquals(actor, session.Current)) throw new UnauthorizedAccessException();
        rows.Insert(0, new(0, "Все автомобили"));
        return rows;
    }
    public async Task<MaintenanceReport> GenerateAsync(DateOnly from, DateOnly to, int vehicleId)
    {
        if (from > to) throw new ArgumentException("Начало периода должно быть не позже окончания.");
        await using var db = DatabaseFactory.Create();
        var actor = await CheckAsync(db);
        var vehicleName = vehicleId == 0 ? "Все автомобили" : await db.Vehicles.Where(x => x.VehicleId == vehicleId)
            .Select(x => x.LicensePlate + " · " + x.Brand + " " + x.Model).SingleOrDefaultAsync()
            ?? throw new ArgumentException("Автомобиль удалён. Откройте отчёт заново.");
        var works = await db.MaintenanceRepairs.AsNoTracking().Include(x => x.Vehicle)
            .Include(x => x.WorkParts).ThenInclude(x => x.Part)
            .Where(x => x.WorkDate >= from && x.WorkDate <= to && (vehicleId == 0 || x.VehicleId == vehicleId))
            .OrderBy(x => x.WorkDate).ThenBy(x => x.WorkId).ToListAsync();
        await CheckAsync(db);
        if (!ReferenceEquals(actor, session.Current)) throw new UnauthorizedAccessException();
        return new(from, to, vehicleName, DateTime.Now, works.Select(x => new MaintenanceReportRow(x.WorkDate,
            x.Vehicle.LicensePlate + " · " + x.Vehicle.Brand + " " + x.Vehicle.Model, x.WorkType, x.Mileage, x.Cost,
            string.Join("; ", x.WorkParts.Select(p => $"{p.Part.Name} ({p.Part.ArticleNumber}) — {p.Quantity} шт.")),
            x.Description ?? "")).ToList());
    }
    public static string Csv(MaintenanceReport report)
    {
        static string Cell(string value)
        {
            if (value.TrimStart().StartsWith('=') || value.TrimStart().StartsWith('+') ||
                value.TrimStart().StartsWith('-') || value.TrimStart().StartsWith('@')) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        static string Line(params string[] cells) => string.Join(";", cells.Select(Cell));
        var lines = new List<string> {
            Line("Обслуживание и расходы"), Line("Период", report.From.ToString("dd.MM.yyyy"), report.To.ToString("dd.MM.yyyy")),
            Line("Автомобиль", report.Vehicle), Line("Сформирован", report.Created.ToString("dd.MM.yyyy HH:mm")),
            Line("Дата", "Автомобиль", "Работа", "Пробег, км", "Стоимость, ₽", "Запчасти", "Описание")
        };
        lines.AddRange(report.Rows.Select(x => Line(x.Date.ToString("dd.MM.yyyy"), x.Vehicle, x.Work,
            x.Mileage.ToString(), x.Cost.ToString("0.00", CultureInfo.GetCultureInfo("ru-RU")), x.Parts, x.Description)));
        lines.Add(Line("Итого", "", "", "", report.Total.ToString("0.00", CultureInfo.GetCultureInfo("ru-RU"))));
        return string.Join("\r\n", lines);
    }
}
