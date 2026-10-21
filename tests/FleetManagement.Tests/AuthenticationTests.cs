using Xunit;
using Курсовой_проект.Models;
using Курсовой_проект.Services;
using Курсовой_проект.ViewModels;
using FleetManagement.PasswordMigration;

namespace FleetManagement.Tests;

public class AuthenticationTests
{
    private const string Password = "Тестовый пароль 42!";
    private static readonly string Hash = PasswordHasher.Hash(Password);

    private sealed class Store(User? user) : IUserStore
    {
        public Task<User?> FindByLoginAsync(string login) =>
            Task.FromResult(login == "test" ? user : null);
    }

    private static User Account(string role = "Диспетчер", string? password = null) => new()
    {
        UserId = 12, Login = "test", LastName = "Тестов", FirstName = "Иван",
        Password = password ?? Hash, Role = new Role { Name = role }
    };

    [Fact]
    public void HashUsesSaltAndFitsExistingColumn()
    {
        var other = PasswordHasher.Hash(Password);
        Assert.NotEqual(Hash, other);
        Assert.True(other.Length <= 256);
        Assert.True(PasswordHasher.Verify(Password, other));
        Assert.False(PasswordHasher.Verify(Password + " ", other));
    }

    [Theory]
    [InlineData("plaintext")]
    [InlineData("")]
    [InlineData("PBKDF2-SHA256$1$999999999$bad$bad")]
    [InlineData("PBKDF2-SHA256$1$600000$bad$bad")]
    [InlineData("PBKDF2-SHA256$2$600000$bad$bad")]
    public void InvalidOrPlaintextHashesAreRejected(string value) =>
        Assert.False(PasswordHasher.Verify(Password, value));

    [Theory]
    [InlineData("Администратор")]
    [InlineData("Диспетчер")]
    [InlineData("Механик")]
    [InlineData("Наблюдатель")]
    public async Task ValidAccountCreatesSessionWithItsRole(string role)
    {
        var session = new UserSession();
        var service = new AuthenticationService(new Store(Account(role)), session);
        Assert.True(await service.SignInAsync(" test ", Password));
        Assert.Equal(new SessionUser(12, "Тестов Иван", role), session.Current);
        session.SignOut();
        Assert.Null(session.Current);
    }

    [Theory]
    [InlineData("missing", Password, "Диспетчер", false)]
    [InlineData("test", "wrong", "Диспетчер", false)]
    [InlineData("test", Password, "Неизвестная роль", false)]
    [InlineData("test", Password, "Диспетчер", true)]
    public async Task InvalidLoginNeverLeavesAnAuthenticatedSession(string login, string password,
        string role, bool plaintext)
    {
        var session = new UserSession();
        session.SignIn(new SessionUser(1, "Прежний пользователь", "Администратор"));
        var service = new AuthenticationService(new Store(Account(role, plaintext ? Password : Hash)), session);
        Assert.False(await service.SignInAsync(login, password));
        Assert.Null(session.Current);
    }

    [Fact]
    public void MigrationIsRepeatableAndDoesNotChangeOriginalObjects()
    {
        var user = Account(password: Password);
        var replacements = MigrationPlan.Prepare(new[] { user });
        Assert.Equal(Password, user.Password);
        Assert.True(PasswordHasher.Verify(Password, replacements[user.UserId]));
        user.Password = replacements[user.UserId];
        Assert.Empty(MigrationPlan.Prepare(new[] { user }));
    }

    [Fact]
    public void MigrationRejectsDamagedHashBeforeChangingAnyAccount()
    {
        var user = Account(password: Password);
        var broken = Account(password: "PBKDF2-SHA256$bad");
        broken.UserId = 13;
        Assert.Throws<InvalidOperationException>(() => MigrationPlan.Prepare(new[] { user, broken }));
        Assert.Equal(Password, user.Password);
    }

    private sealed class FailingAuthentication : IAuthenticationService
    {
        public Task<bool> SignInAsync(string login, string password) =>
            throw new Exception("Server=private;Password=secret");
    }

    [Fact]
    public async Task LoginErrorDoesNotExposeConnectionDetails()
    {
        var vm = new LoginViewModel(new FailingAuthentication()) { Login = "test" };
        Assert.False(await vm.SignInAsync(Password));
        Assert.Contains("SQL Server", vm.Error);
        Assert.DoesNotContain("secret", vm.Error);
        Assert.True(vm.CanSubmit);
    }

    [Fact]
    public async Task EmptyInputDoesNotCallAuthentication()
    {
        var vm = new LoginViewModel(new FailingAuthentication());
        Assert.False(await vm.SignInAsync(""));
        Assert.Equal("Введите логин и пароль.", vm.Error);
    }

    private sealed class PendingStore(Task<User?> result) : IUserStore
    {
        public Task<User?> FindByLoginAsync(string login) => result;
    }

    [Fact]
    public async Task LogoutDuringLoginPreventsSessionFromBeingRestored()
    {
        var pending = new TaskCompletionSource<User?>();
        var session = new UserSession();
        var service = new AuthenticationService(new PendingStore(pending.Task), session);
        var attempt = service.SignInAsync("test", Password);
        session.SignOut();
        pending.SetResult(Account());
        Assert.False(await attempt);
        Assert.Null(session.Current);
    }

    private sealed class PendingAuthentication : IAuthenticationService
    {
        public int Calls { get; private set; }
        public TaskCompletionSource<bool> Result { get; } = new();
        public Task<bool> SignInAsync(string login, string password)
        {
            Calls++;
            return Result.Task;
        }
    }

    [Fact]
    public async Task LoginFormBlocksDuplicateSubmissionsAndAllowsRetry()
    {
        var authentication = new PendingAuthentication();
        var vm = new LoginViewModel(authentication) { Login = "test" };
        var first = vm.SignInAsync(Password);
        Assert.False(vm.CanSubmit);
        Assert.False(await vm.SignInAsync(Password));
        Assert.Equal(1, authentication.Calls);
        authentication.Result.SetResult(false);
        Assert.False(await first);
        Assert.True(vm.CanSubmit);
        Assert.False(await vm.SignInAsync(Password));
        Assert.Equal(2, authentication.Calls);
    }
}
