using System.Windows;
using Курсовой_проект.Services;

namespace Курсовой_проект.Views;

public partial class RecordDetailsWindow : Window
{
    public RecordDetailsWindow(CatalogRow row)
    {
        InitializeComponent();
        DataContext = row;
    }
}
