using Xunit;
using Курсовой_проект.Services;

namespace FleetManagement.Tests;

public class MaintenanceReportTests
{
    [Fact]
    public void ExportPreservesTextTotalsAndNeutralizesSpreadsheetFormulas()
    {
        var report = new MaintenanceReport(new(2026, 1, 1), new(2026, 1, 31), "Все автомобили",
            new(2026, 2, 1), [
                new(new(2026, 1, 10), "А123БВ", "=1+1", 1000, 1200.50m, "Фильтр; 2 шт.", "Строка \"один\"\nдва"),
                new(new(2026, 1, 31), "В123АБ", "ТО", 2000, 200m, "", "")
            ]);
        Assert.Equal(1400.50m, report.Total);
        var csv = MaintenanceReportService.Csv(report);
        Assert.Contains("\"'=1+1\"", csv);
        Assert.Contains("\"Фильтр; 2 шт.\"", csv);
        Assert.Contains("\"\"один\"\"", csv);
        Assert.Contains("1400,50", csv);
        var session = new UserSession();
        var service = new MaintenanceReportService(new AccessService(session), session);
        Assert.False(service.CanRead);
        session.SignIn(new SessionUser(1, "Наблюдатель", "Наблюдатель"));
        Assert.False(service.CanRead);
        session.SignIn(new SessionUser(2, "Механик", "Механик"));
        Assert.True(service.CanRead);
    }
}
