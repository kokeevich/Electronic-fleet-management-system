using System.Text.RegularExpressions;
using Xunit;
using Курсовой_проект.Models;
using Курсовой_проект.Services;

namespace FleetManagement.Tests;

public class DemoAccountTests
{
    private sealed class Store(User user) : IUserStore
    {
        public Task<User?> FindByLoginAsync(string login) =>
            Task.FromResult<User?>(user.Login == login ? user : null);
    }

    [Theory]
    [InlineData("fleet_admin", "TaxiAdmin!2026", "Администратор")]
    [InlineData("fleet_dispatcher", "TaxiDispatch!2026", "Диспетчер")]
    [InlineData("fleet_mechanic", "TaxiMechanic!2026", "Механик")]
    [InlineData("fleet_viewer", "TaxiViewer!2026", "Наблюдатель")]
    public async Task SqlSeedHashesWorkWithTheActualAuthenticationService(string login, string password, string role)
    {
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "database-demo.sql"));
        var match = Regex.Match(script, "N'" + Regex.Escape(login) + "', N'" + Regex.Escape(role) + "', N'([^']+)'");
        Assert.True(match.Success);
        var hash = match.Groups[1].Value;
        Assert.True(PasswordHasher.Verify(password, hash));
        Assert.False(PasswordHasher.Verify(password + "wrong", hash));
        var user = new User { UserId = 1, Login = login, Password = hash, FirstName = "Тестовый",
            LastName = "Пользователь", Role = new Role { Name = role } };
        var session = new UserSession();
        Assert.True(await new AuthenticationService(new Store(user), session).SignInAsync(login, password));
        Assert.Equal(role, session.Current!.Role);
    }
}
