using Xunit;
using Курсовой_проект.Services;

namespace FleetManagement.Tests;

public class AccessTests
{
    [Theory]
    [InlineData("Администратор", Permission.ManageUsers, true)]
    [InlineData("Диспетчер", Permission.ManageVehicles, true)]
    [InlineData("Диспетчер", Permission.ManageMaintenance, false)]
    [InlineData("Диспетчер", Permission.ManageUsers, false)]
    [InlineData("Механик", Permission.ManageParts, true)]
    [InlineData("Механик", Permission.ManageMaintenance, true)]
    [InlineData("Механик", Permission.ManageVehicles, false)]
    [InlineData("Механик", Permission.ManageSettings, false)]
    [InlineData("Наблюдатель", Permission.ViewVehicles, true)]
    [InlineData("Наблюдатель", Permission.ManageVehicles, false)]
    [InlineData("Неизвестная роль", Permission.ViewVehicles, false)]
    public void PermissionsAreEnforcedOutsideTheInterface(string role, Permission permission, bool allowed)
    {
        var session = new UserSession();
        session.SignIn(new SessionUser(1, "Тест", role));
        var access = new AccessService(session);
        Assert.Equal(allowed, access.Can(permission));
        if (allowed) access.Require(permission);
        else Assert.Throws<UnauthorizedAccessException>(() => access.Require(permission));
    }

    [Fact]
    public async Task FleetServiceRejectsUnauthenticatedRequestsBeforeAccessingDatabase()
    {
        var session = new UserSession();
        var access = new AccessService(session);
        var service = new FleetService(access, session);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetVehiclesAsync());
        session.SignIn(new SessionUser(1, "Тест", "Диспетчер"));
        session.SignOut();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetVehiclesAsync());
    }

    [Fact]
    public void ObserverHasNoWritePermissions()
    {
        var session = new UserSession();
        session.SignIn(new SessionUser(1, "Тест", "Наблюдатель"));
        var access = new AccessService(session);
        foreach (var permission in Enum.GetValues<Permission>().Where(p => p.ToString().StartsWith("Manage")))
            Assert.False(access.Can(permission));
        Assert.False(access.Can((Permission)999));
    }

    [Fact]
    public void AdministratorHasAllDefinedPermissionsAndLogoutRevokesThem()
    {
        var session = new UserSession();
        var access = new AccessService(session);
        session.SignIn(new SessionUser(1, "Тест", "Администратор"));
        foreach (var permission in Enum.GetValues<Permission>())
            Assert.True(access.Can(permission));
        Assert.False(access.Can((Permission)999));
        session.SignOut();
        foreach (var permission in Enum.GetValues<Permission>())
            Assert.False(access.Can(permission));
    }
}
