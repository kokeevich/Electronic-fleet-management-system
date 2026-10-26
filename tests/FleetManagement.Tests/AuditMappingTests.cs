using Microsoft.EntityFrameworkCore;
using Xunit;
using Курсовой_проект.Models;

namespace FleetManagement.Tests;

public class AuditMappingTests
{
    [Fact]
    public void SqlServerAuditTableMatchesInstallationScript()
    {
        using var db = new OzvsKursachContext(new DbContextOptionsBuilder<OzvsKursachContext>()
            .UseSqlServer("Server=localhost;Database=unused;Integrated Security=true").Options);
        Assert.Equal("AuditLog", db.Model.FindEntityType(typeof(AuditLog))!.GetTableName());
    }
}
