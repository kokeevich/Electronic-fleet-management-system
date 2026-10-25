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
    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (await viewModel.SaveAsync()) DialogResult = true;
    }
}
