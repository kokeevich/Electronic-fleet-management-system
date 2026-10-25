using System.Windows;
using System.Windows.Controls;
using Курсовой_проект.ViewModels;

namespace Курсовой_проект.Views;

public partial class CatalogView : UserControl
{
    public CatalogView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is CatalogViewModel vm && !vm.HasLoaded && !vm.IsBusy)
            await vm.LoadAsync();
    }

    private void OpenRecord_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is CatalogViewModel { CanOpenRecord: true, SelectedRow: { } row })
            new RecordDetailsWindow(row) { Owner = Window.GetWindow(this) }.ShowDialog();
    }

    private async void EditRecord_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CatalogViewModel catalog) return;
        var editor = catalog.CreateEditor();
        if (editor == null) return;
        if (new CatalogEditWindow(editor) { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await catalog.LoadAsync();
    }
}
