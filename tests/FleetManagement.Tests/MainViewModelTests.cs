using Xunit;
using Курсовой_проект.Models;
using Курсовой_проект.Services;
using Курсовой_проект.ViewModels;

namespace FleetManagement.Tests;

public class MainViewModelTests
{
    private sealed class StubService : IFleetService
    {
        public Func<Task<List<Vehicle>>> Load { get; set; } = () =>
            Task.FromResult(new List<Vehicle>());
        public Task<List<Vehicle>> GetVehiclesAsync() => Load();
    }

    [Fact]
    public async Task RefreshReplacesRowsWithoutDuplicates()
    {
        var service = new StubService
        {
            Load = () => Task.FromResult(new List<Vehicle> { new() { VehicleId = 7 } })
        };
        var vm = new MainViewModel(service);
        await vm.LoadAsync();
        await vm.LoadAsync();
        Assert.Equal(7, Assert.Single(vm.Vehicles).VehicleId);
        Assert.Contains("Загружено", vm.Status);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task FailureClearsStaleRowsHidesDetailsAndAllowsRetry()
    {
        var service = new StubService();
        var vm = new MainViewModel(service);
        vm.Vehicles.Add(new Vehicle());
        service.Load = () => throw new Exception("Password=do-not-display");
        await vm.LoadAsync();
        Assert.Empty(vm.Vehicles);
        Assert.Contains("Не удалось", vm.Status);
        Assert.DoesNotContain("do-not-display", vm.Status);
        Assert.False(vm.IsBusy);
        service.Load = () => Task.FromResult(new List<Vehicle>());
        await vm.LoadAsync();
        Assert.Contains("пока нет", vm.Status);
    }

    [Fact]
    public async Task ConcurrentRefreshDoesNotStartAnotherRequest()
    {
        var pending = new TaskCompletionSource<List<Vehicle>>();
        var calls = 0;
        var vm = new MainViewModel(new StubService
        {
            Load = () => { calls++; return pending.Task; }
        });
        var first = vm.LoadAsync();
        Assert.True(vm.IsBusy);
        await vm.LoadAsync();
        Assert.Equal(1, calls);
        pending.SetResult(new List<Vehicle>());
        await first;
        Assert.False(vm.IsBusy);
    }
}
