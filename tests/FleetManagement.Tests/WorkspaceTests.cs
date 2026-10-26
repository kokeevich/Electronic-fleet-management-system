using Xunit;
using Курсовой_проект.Models;
using Курсовой_проект.Services;
using Курсовой_проект.ViewModels;

namespace FleetManagement.Tests;

public class WorkspaceTests
{
    private sealed class FleetStub : IFleetService
    {
        public Task<List<Vehicle>> GetVehiclesAsync() => Task.FromResult(new List<Vehicle>());
        public Task<VehicleResult> SaveAsync(VehicleData? original, VehicleData edited) => throw new NotSupportedException();
        public Task<VehicleResult> DeleteAsync(VehicleData original) => throw new NotSupportedException();
    }
    private sealed class CatalogStub : ICatalogService
    {
        public List<CatalogSection> Requests { get; } = new();
        public Task<List<CatalogRow>> ReadAsync(CatalogSection section)
        {
            Requests.Add(section);
            return Task.FromResult(new List<CatalogRow>
            {
                new(section.ToString(), "Описание", "Полная строка 1\nПолная строка 2", "Действует")
            });
        }
    }
    private static (WorkspaceViewModel Workspace, CatalogStub Service) Create(string role)
    {
        var session = new UserSession();
        session.SignIn(new SessionUser(1, "Тест", role));
        var access = new AccessService(session);
        var service = new CatalogStub();
        var catalog = new CatalogViewModel(service, access);
        return (new WorkspaceViewModel(new MainViewModel(new FleetStub(), session, access), catalog), service);
    }

    [Fact]
    public void UnifiedNavigationOpensCorrectSectionAndPreservesVehicleScreen()
    {
        var (workspace, service) = Create("Администратор");
        Assert.True(workspace.IsVehicles);
        Assert.Equal(11, workspace.Pages.Count); // транспорт, восемь таблиц, журнал, подключение
        workspace.SelectedPage = workspace.Pages.Single(x => x.Definition?.Section == CatalogSection.Documents);
        Assert.True(workspace.IsCatalog);
        Assert.False(workspace.IsVehicles);
        Assert.Equal(CatalogSection.Documents, Assert.Single(service.Requests));
        Assert.Equal("Documents", Assert.Single(workspace.Catalog.Rows).Title);
        workspace.SelectedPage = workspace.Pages.Single(x => x.Kind == WorkspacePageKind.Connection);
        Assert.True(workspace.IsConnection);
        workspace.SelectedPage = workspace.Pages[0];
        Assert.True(workspace.IsVehicles);
        Assert.False(workspace.IsCatalog);
        Assert.True(workspace.Fleet.CanManageVehicles);
    }

    [Fact]
    public void NavigationAndFutureEditPlacesRespectRole()
    {
        var (observer, _) = Create("Наблюдатель");
        Assert.DoesNotContain(observer.Pages, x => x.Kind == WorkspacePageKind.Connection ||
            x.Definition?.Section is CatalogSection.Users or CatalogSection.Roles or CatalogSection.Notifications);
        observer.SelectedPage = observer.Pages.Single(x => x.Definition?.Section == CatalogSection.Drivers);
        Assert.False(observer.Catalog.CanManageSection);
        var before = observer.SelectedPage;
        observer.SelectedPage = new WorkspacePage("Подключение", WorkspacePageKind.Connection);
        Assert.Equal(before, observer.SelectedPage);

        var (mechanic, _) = Create("Механик");
        mechanic.SelectedPage = mechanic.Pages.Single(x => x.Definition?.Section == CatalogSection.Drivers);
        Assert.False(mechanic.Catalog.CanManageSection);
        mechanic.SelectedPage = mechanic.Pages.Single(x => x.Definition?.Section == CatalogSection.Maintenance);
        Assert.True(mechanic.Catalog.CanManageSection);
    }

    [Fact]
    public async Task SelectionIsClearedBySearchRefreshAndNavigationWhileFullDetailsRemainAvailable()
    {
        var (workspace, _) = Create("Администратор");
        workspace.SelectedPage = workspace.Pages.Single(x => x.Definition?.Section == CatalogSection.Drivers);
        var vm = workspace.Catalog;
        var row = Assert.Single(vm.Rows);
        vm.SelectedRow = row;
        Assert.True(vm.CanOpenRecord);
        Assert.Contains("\n", row.Details);
        Assert.DoesNotContain("\n", row.DetailsPreview);
        Assert.Contains("Полная строка 2", row.DetailsPreview);
        vm.Search = "нет совпадений";
        Assert.Null(vm.SelectedRow);
        Assert.False(vm.CanOpenRecord);
        vm.Search = "";
        vm.SelectedRow = Assert.Single(vm.Rows);
        await vm.LoadAsync();
        Assert.Null(vm.SelectedRow);
        vm.SelectedRow = Assert.Single(vm.Rows);
        workspace.SelectedPage = workspace.Pages.Single(x => x.Definition?.Section == CatalogSection.Parts);
        Assert.Null(vm.SelectedRow);
        Assert.False(vm.CanOpenRecord);
    }
}
