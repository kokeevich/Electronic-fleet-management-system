using Xunit;
using Курсовой_проект.Models;
using Курсовой_проект.Services;

namespace FleetManagement.Tests;

public class AuditPresentationTests
{
    [Fact]
    public void LegacySnapshotsDecodeUnicodeAndShowOnlyChangedFields()
    {
        var row = AuditPresentation.Present(new AuditLog
        {
            Action = "Изменение", EntityName = "Транспорт", ActorName = "Иван", ActorUserId = 7,
            BeforeData = """{"Brand":"\u041a\u0438\u0430","Mileage":100,"Status":"Работает"}""",
            AfterData = """{"Brand":"\u041a\u0438\u0430","Mileage":200,"Status":"Работает"}"""
        });
        Assert.Contains("Пробег, км", row.Details);
        Assert.Contains("Было: 100", row.Details);
        Assert.Contains("Стало: 200", row.Details);
        Assert.DoesNotContain("Марка", row.Details);
        Assert.Contains("Иван (ID 7)", row.Subtitle);
        var deleted = AuditPresentation.Present(new AuditLog
        {
            Action = "Удаление", BeforeData = """{"Brand":"\u041a\u0438\u0430"}"""
        });
        Assert.Contains("Марка: Киа", deleted.Details);
        Assert.DoesNotContain("\\u", deleted.Details);
    }
}
