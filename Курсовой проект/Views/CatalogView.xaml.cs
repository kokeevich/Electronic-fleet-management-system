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

    private async void AddRecord_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CatalogViewModel catalog) return;
        var editor = catalog.CreateNewEditor();
        if (editor != null && new CatalogEditWindow(editor) { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await catalog.LoadAsync();
    }

    private async void DeleteRecord_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CatalogViewModel catalog) return;
        var data = await catalog.PrepareDeleteAsync();
        if (data == null) return;
        var details = string.Join("\n", data.Fields.Select(x => x.Label + ": " +
            (x.IsChoice ? x.Options.FirstOrDefault(o => o.Value == x.Value)?.Label : x.Value)));
        if (MessageBox.Show(Window.GetWindow(this), $"Удалить запись из раздела «{data.Title}»?\n\n{details}\n\nЭто действие нельзя отменить.",
            "Удаление записи", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes)
            await catalog.DeleteAsync(data);
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
