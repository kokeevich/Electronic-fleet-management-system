using System.Windows;
using Курсовой_проект.ViewModels;

namespace Курсовой_проект.Views;

public partial class CatalogEditWindow : Window
{
    private readonly CatalogEditorViewModel viewModel;
    public CatalogEditWindow(CatalogEditorViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        Loaded += Load;
        Closing += (_, e) => e.Cancel = viewModel.IsBusy;
    }
    private async void Load(object sender, RoutedEventArgs e)
    {
        Loaded -= Load;
        await viewModel.LoadAsync();
    }
    private void ClearDate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: Курсовой_проект.Services.EditField field } && field.CanClearDate)
            field.SelectedDate = null;
    }
    private void Password_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.PasswordBox { DataContext: Курсовой_проект.Services.EditField field } box)
            field.Value = box.Password;
    }
    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (await viewModel.SaveAsync()) DialogResult = true;
    }
}
