using Xunit;
using Курсовой_проект.Models;
using Курсовой_проект.Services;
using Курсовой_проект.ViewModels;

namespace FleetManagement.Tests;

public class VehicleViewModelTests
{
    private sealed class Fleet : IFleetService
    {
        public int Saves;
        public Func<Task<VehicleResult>> Save = () => Task.FromResult(new VehicleResult(true, "Сохранено"));
        public Task<List<Vehicle>> GetVehiclesAsync() => Task.FromResult(new List<Vehicle>
        {
            new() { VehicleId = 1, LicensePlate = "А123БВ196", Brand = "Kia", Model = "Rio", Status = "В эксплуатации" },
            new() { VehicleId = 2, LicensePlate = "В456ГД196", Brand = "Skoda", Model = "Rapid", Status = "В ремонте" }
        });
        public Task<VehicleResult> SaveAsync(VehicleData? original, VehicleData edited) { Saves++; return Save(); }
        public Task<VehicleResult> DeleteAsync(VehicleData original) => Task.FromResult(new VehicleResult(false, "Есть связанные записи"));
    }

    [Fact]
    public async Task SearchAndStatusAreCombinedAndPreservedAfterRefresh()
    {
        var session = new UserSession();
        session.SignIn(new SessionUser(1, "Тест", "Диспетчер"));
        var vm = new MainViewModel(new Fleet(), session, new AccessService(session));
        await vm.LoadAsync();
        vm.SelectedVehicle = vm.Vehicles.First();
        vm.SearchText = " rapid ";
        Assert.Equal(2, Assert.Single(vm.Vehicles).VehicleId);
        Assert.Null(vm.SelectedVehicle);
        vm.SelectedStatus = "В эксплуатации";
        Assert.Empty(vm.Vehicles);
        vm.SelectedStatus = "В ремонте";
        await vm.LoadAsync();
        Assert.Single(vm.Vehicles);
        vm.SearchText = "в456";
        Assert.Single(vm.Vehicles);
        vm.SearchText = "SKODA";
        Assert.Single(vm.Vehicles);
    }

    [Theory]
    [InlineData("Механик")]
    [InlineData("Наблюдатель")]
    public async Task ReadOnlyRolesCannotOpenAnEditableCard(string role)
    {
        var session = new UserSession();
        session.SignIn(new SessionUser(1, "Тест", role));
        var fleet = new Fleet();
        var vm = new MainViewModel(fleet, session, new AccessService(session));
        await vm.LoadAsync();
        vm.SelectedVehicle = vm.Vehicles.First();
        Assert.Null(vm.CreateEditor(true));
        var editor = vm.CreateEditor(false);
        Assert.NotNull(editor);
        Assert.True(editor.IsReadOnly);
        Assert.False(await editor.SaveAsync());
        Assert.Equal(0, fleet.Saves);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("99999999999")]
    public async Task InvalidNumericInputDoesNotReachService(string mileage)
    {
        var fleet = new Fleet();
        var editor = new VehicleEditorViewModel(fleet, null, false) { Mileage = mileage };
        Assert.False(await editor.SaveAsync());
        Assert.Equal(0, fleet.Saves);
    }

    [Fact]
    public async Task SaveErrorHidesTechnicalDetailsAndEnablesRetry()
    {
        var fleet = new Fleet { Save = () => throw new Exception("Password=secret;Server=internal") };
        var editor = new VehicleEditorViewModel(fleet, null, false);
        Assert.False(await editor.SaveAsync());
        Assert.DoesNotContain("secret", editor.Message);
        Assert.True(editor.CanEdit);
        Assert.True(editor.CanClose);
    }

    [Fact]
    public async Task ConcurrentSaveDoesNotSubmitTwice()
    {
        var pending = new TaskCompletionSource<VehicleResult>();
        var fleet = new Fleet { Save = () => pending.Task };
        var editor = new VehicleEditorViewModel(fleet, null, false);
        var first = editor.SaveAsync();
        Assert.False(editor.CanClose);
        Assert.False(await editor.SaveAsync());
        Assert.Equal(1, fleet.Saves);
        pending.SetResult(new VehicleResult(true, "Сохранено"));
        Assert.True(await first);
    }

    [Fact]
    public async Task RefusedDeletionKeepsListAndShowsReason()
    {
        var session = new UserSession();
        session.SignIn(new SessionUser(1, "Тест", "Диспетчер"));
        var vm = new MainViewModel(new Fleet(), session, new AccessService(session));
        await vm.LoadAsync();
        vm.SelectedVehicle = vm.Vehicles.First();
        await vm.DeleteSelectedAsync();
        Assert.Equal(2, vm.Vehicles.Count);
        Assert.Equal("Есть связанные записи", vm.Status);
        Assert.True(vm.IsIdle);
    }
}
