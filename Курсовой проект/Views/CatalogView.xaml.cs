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
}
