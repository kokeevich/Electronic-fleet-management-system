using System.Text.RegularExpressions;
using Xunit;
using Курсовой_проект.Services;

namespace FleetManagement.Tests;

public class DatabaseInstallTests
{
    [Fact]
    public void AllCollegeAccountsAcceptDocumentedPasswords()
    {
        var sql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "database-install.sql"));
        var accounts = new Dictionary<string, string>
        {
            ["fleet_admin"] = "TaxiAdmin!2026", ["fleet_dispatcher"] = "TaxiDispatch!2026",
            ["fleet_mechanic"] = "TaxiMechanic!2026", ["fleet_viewer"] = "TaxiViewer!2026",
            ["fleet_admin2"] = "TaxiAdmin2!2026", ["fleet_dispatcher2"] = "TaxiDispatch2!2026",
            ["fleet_mechanic2"] = "TaxiMechanic2!2026", ["fleet_viewer2"] = "TaxiViewer2!2026"
        };
        foreach (var (login, password) in accounts)
        {
            var match = Regex.Match(sql, "N'" + login + @"', N'(PBKDF2-SHA256[^']+)'");
            Assert.True(match.Success, login);
            Assert.True(PasswordHasher.Verify(password, match.Groups[1].Value), login);
            Assert.False(PasswordHasher.Verify("wrong-password", match.Groups[1].Value));
        }
    }
}
