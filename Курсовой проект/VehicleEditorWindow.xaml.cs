using System.Windows;
using Курсовой_проект.ViewModels;

namespace Курсовой_проект;

public partial class VehicleEditorWindow : Window
{
    private readonly VehicleEditorViewModel viewModel;
    public VehicleEditorWindow(VehicleEditorViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        Closing += (_, e) => e.Cancel = viewModel.IsBusy;
        Loaded += (_, _) => { if (!viewModel.IsReadOnly) Plate.Focus(); };
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (await viewModel.SaveAsync()) DialogResult = true;
    }
}
