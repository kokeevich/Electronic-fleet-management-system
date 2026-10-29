using Xunit;
using Курсовой_проект.Models;
using Курсовой_проект.Services;
using Курсовой_проект.ViewModels;

namespace FleetManagement.Tests;

public class RegistrationTests
{
    private const string Password = "Учебный пароль 42!";
    private static RegistrationRequest ValidRequest() =>
        new(" Тестов ", " Иван ", " ", " test.user ", Password, Password);

    private sealed class MemoryStore : IRegistrationStore, IUserStore
    {
        public User? Account { get; private set; }
        public int Calls { get; private set; }
        public CreateAccountResult Result { get; set; } = CreateAccountResult.Created;

        public Task<CreateAccountResult> CreateObserverAsync(string lastName, string firstName,
            string? middleName, string login, string passwordHash)
        {
            Calls++;
            if (Account?.Login.Equals(login, StringComparison.OrdinalIgnoreCase) == true)
                return Task.FromResult(CreateAccountResult.DuplicateLogin);
            if (Result != CreateAccountResult.Created) return Task.FromResult(Result);
            Account = new User
            {
                UserId = 1, LastName = lastName, FirstName = firstName, MiddleName = middleName,
                Login = login, Password = passwordHash, RoleId = 42,
                Role = new Role { RoleId = 42, Name = "Наблюдатель" }
            };
            return Task.FromResult(CreateAccountResult.Created);
        }

        public Task<User?> FindByLoginAsync(string login) =>
            Task.FromResult(Account?.Login == login ? Account : null);
    }

    [Fact]
    public async Task RegisteredUserCanSignInAsObserverWithoutPasswordMigration()
    {
        var store = new MemoryStore();
        var session = new UserSession();
        var result = await new RegistrationService(store).RegisterAsync(ValidRequest());
        Assert.True(result.Success);
        Assert.NotNull(store.Account);
        Assert.Equal("Тестов", store.Account.LastName);
        Assert.Equal("Иван", store.Account.FirstName);
        Assert.Null(store.Account.MiddleName);
        Assert.Equal("test.user", store.Account.Login);
        Assert.NotEqual(Password, store.Account.Password);
        Assert.True(PasswordHasher.Verify(Password, store.Account.Password));
        Assert.Null(session.Current);

        Assert.True(await new AuthenticationService(store, session).SignInAsync("test.user", Password));
        Assert.Equal("Наблюдатель", session.Current!.Role);
        Assert.False(new AccessService(session).Can(Permission.ManageUsers));
        Assert.False(new AccessService(session).Can(Permission.ManageVehicles));
    }

    public static IEnumerable<object[]> InvalidRequests()
    {
        yield return new object[] { ValidRequest() with { LastName = " " } };
        yield return new object[] { ValidRequest() with { FirstName = "" } };
        yield return new object[] { ValidRequest() with { MiddleName = new string('а', 101) } };
        yield return new object[] { ValidRequest() with { LastName = new string('а', 101) } };
        yield return new object[] { ValidRequest() with { FirstName = new string('а', 101) } };
        yield return new object[] { ValidRequest() with { FirstName = "Иван\n" + "Иван" } };
        yield return new object[] { ValidRequest() with { Login = "ab" } };
        yield return new object[] { ValidRequest() with { Login = new string('a', 101) } };
        yield return new object[] { ValidRequest() with { Login = "test user" } };
        yield return new object[] { ValidRequest() with { Login = "test'user" } };
        yield return new object[] { ValidRequest() with { Password = "1234567", PasswordConfirmation = "1234567" } };
        yield return new object[] { ValidRequest() with { Password = new string('a', 129), PasswordConfirmation = new string('a', 129) } };
        yield return new object[] { ValidRequest() with { Password = "        ", PasswordConfirmation = "        " } };
        yield return new object[] { ValidRequest() with { PasswordConfirmation = Password + " " } };
        foreach (var weak in new[] { "password123!", "PASSWORD123!", "Password123", "Password!!!", "Aa1!test\n" })
            yield return new object[] { ValidRequest() with { Password = weak, PasswordConfirmation = weak } };
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task InvalidDataNeverReachesStorage(RegistrationRequest request)
    {
        var store = new MemoryStore();
        var result = await new RegistrationService(store).RegisterAsync(request);
        Assert.False(result.Success);
        Assert.NotEmpty(result.Message);
        Assert.Equal(0, store.Calls);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(128)]
    public async Task PasswordLengthBoundariesAreAccepted(int length)
    {
        var password = "Яя1!" + new string('я', length - 4);
        var store = new MemoryStore();
        var request = ValidRequest() with { Password = password, PasswordConfirmation = password };
        Assert.True((await new RegistrationService(store).RegisterAsync(request)).Success);
        Assert.True(PasswordHasher.Verify(password, store.Account!.Password));
    }

    [Fact]
    public async Task DuplicateLoginDoesNotOverwriteOriginalAccount()
    {
        var store = new MemoryStore();
        var service = new RegistrationService(store);
        Assert.True((await service.RegisterAsync(ValidRequest())).Success);
        var originalHash = store.Account!.Password;
        var result = await service.RegisterAsync(ValidRequest() with { Login = "TEST.USER" });
        Assert.False(result.Success);
        Assert.Contains("занят", result.Message);
        Assert.Equal(originalHash, store.Account.Password);
    }

    [Theory]
    [InlineData(CreateAccountResult.DuplicateLogin, "занят")]
    [InlineData(CreateAccountResult.ObserverRoleUnavailable, "роль")]
    public async Task StorageConflictsBecomeUsefulMessages(CreateAccountResult outcome, string expected)
    {
        var store = new MemoryStore { Result = outcome };
        var result = await new RegistrationService(store).RegisterAsync(ValidRequest());
        Assert.False(result.Success);
        Assert.Contains(expected, result.Message);
        Assert.Null(store.Account);
    }

    [Fact]
    public async Task DatabaseStoreRejectsPlaintextBeforeConnecting()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => new RegistrationStore()
            .CreateObserverAsync("Тестов", "Иван", null, "test", Password));
    }

    private sealed class FailingService : IRegistrationService
    {
        public Task<RegistrationResult> RegisterAsync(RegistrationRequest request) =>
            throw new Exception("Server=private;Password=secret");
    }

    [Fact]
    public async Task ErrorsDoNotExposeDatabaseDetailsAndAllowRetry()
    {
        var vm = new RegisterViewModel(new FailingService());
        Assert.False(await vm.RegisterAsync(Password, Password));
        Assert.DoesNotContain("secret", vm.Message);
        Assert.Contains("SQL Server", vm.Message);
        Assert.True(vm.CanSubmit);
        Assert.False(await vm.RegisterAsync(Password, Password));
        Assert.True(vm.CanSubmit);
    }

    private sealed class PendingService : IRegistrationService
    {
        public TaskCompletionSource<RegistrationResult> Result { get; } = new();
        public int Calls { get; private set; }
        public Task<RegistrationResult> RegisterAsync(RegistrationRequest request)
        {
            Calls++;
            return Result.Task;
        }
    }

    [Fact]
    public async Task BusyFormDoesNotSubmitTwice()
    {
        var service = new PendingService();
        var vm = new RegisterViewModel(service) { Login = " new.user " };
        var first = vm.RegisterAsync(Password, Password);
        Assert.True(vm.IsBusy);
        Assert.False(await vm.RegisterAsync(Password, Password));
        Assert.Equal(1, service.Calls);
        service.Result.SetResult(new RegistrationResult(true, "Создано"));
        Assert.True(await first);
        Assert.Equal("new.user", vm.Login);
        Assert.True(vm.CanSubmit);
    }

    private sealed class UnusedAuthentication : IAuthenticationService
    {
        public Task<bool> SignInAsync(string login, string password) => throw new InvalidOperationException();
    }

    [Fact]
    public void CompletedRegistrationPrefillsLoginWithoutAutomaticallySigningIn()
    {
        var vm = new LoginViewModel(new UnusedAuthentication());
        vm.SetRegisteredLogin("new.user");
        Assert.Equal("new.user", vm.Login);
        Assert.Contains("создана", vm.Notice);
        Assert.Empty(vm.Error);
    }
}
