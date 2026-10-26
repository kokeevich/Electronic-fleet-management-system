using System.ComponentModel;
using System.Runtime.CompilerServices;
using Курсовой_проект.Services;

namespace Курсовой_проект.ViewModels;

public enum WorkspacePageKind { Vehicles, Catalog }
public sealed record WorkspacePage(string Title, WorkspacePageKind Kind, CatalogDefinition? Definition = null);

public sealed class WorkspaceViewModel : INotifyPropertyChanged
{
    private WorkspacePage selectedPage;
    public WorkspaceViewModel(MainViewModel fleet, CatalogViewModel catalog)
    {
        Fleet = fleet;
        Catalog = catalog;
        var pages = new List<WorkspacePage> { new("Транспорт", WorkspacePageKind.Vehicles) };
        pages.AddRange(catalog.Sections.Select(x => new WorkspacePage(x.Title, WorkspacePageKind.Catalog, x)));
        Pages = pages;
        selectedPage = pages[0];
    }
    public MainViewModel Fleet { get; }
    public CatalogViewModel Catalog { get; }
    public IReadOnlyList<WorkspacePage> Pages { get; }
    public WorkspacePage SelectedPage
    {
        get => selectedPage;
        set
        {
            if (value == selectedPage || !Pages.Contains(value)) return;
            selectedPage = value;
            Changed(); Changed(nameof(IsVehicles)); Changed(nameof(IsCatalog));
            if (value.Kind == WorkspacePageKind.Catalog)
            {
                if (Catalog.SelectedSection != value.Definition)
                    Catalog.SelectedSection = value.Definition;
                else if (!Catalog.HasLoaded && !Catalog.IsBusy)
                    _ = Catalog.LoadAsync();
            }
        }
    }
    public bool IsVehicles => SelectedPage.Kind == WorkspacePageKind.Vehicles;
    public bool IsCatalog => SelectedPage.Kind == WorkspacePageKind.Catalog;
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
