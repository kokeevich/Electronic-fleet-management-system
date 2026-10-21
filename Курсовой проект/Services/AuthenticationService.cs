using Microsoft.EntityFrameworkCore;
using Курсовой_проект.Models;

namespace Курсовой_проект.Services;

public interface IUserStore
{
    Task<User?> FindByLoginAsync(string login);
}

public sealed class UserStore : IUserStore
{
    public async Task<User?> FindByLoginAsync(string login)
    {
        await using var database = DatabaseFactory.Create();
        return await database.Users.AsNoTracking().Include(user => user.Role)
            .SingleOrDefaultAsync(user => user.Login == login);
    }
}

public interface IAuthenticationService
{
    Task<bool> SignInAsync(string login, string password);
}

public sealed class AuthenticationService(IUserStore users, UserSession session) : IAuthenticationService
{
    // Дорогостоящая проверка выполняется также для неизвестного логина.
    private static readonly string DummyHash = PasswordHasher.Hash("unused-dummy-password");

    public async Task<bool> SignInAsync(string login, string password)
    {
        session.SignOut();
        var requestVersion = session.Version;
        login = login.Trim();
        if (login.Length is 0 or > 100 || string.IsNullOrEmpty(password) || password.Length > 1024)
            return false;

        var user = await users.FindByLoginAsync(login);
        var supported = user != null && PasswordHasher.IsSupportedHash(user.Password);
        var matches = await Task.Run(() => PasswordHasher.Verify(password,
            supported ? user!.Password : DummyHash));
        if (!supported || !matches || user?.Role == null || !AccessService.IsKnownRole(user.Role.Name)
            || session.Version != requestVersion)
            return false;

        var name = string.Join(" ", new[] { user.LastName, user.FirstName, user.MiddleName }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
        session.SignIn(new SessionUser(user.UserId, name, user.Role.Name));
        return true;
    }
}
