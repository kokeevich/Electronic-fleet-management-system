using System.Windows;
using Курсовой_проект.ViewModels;

namespace Курсовой_проект;

public partial class RegisterWindow : Window
{
    private readonly RegisterViewModel viewModel;
    public string RegisteredLogin => viewModel.Login;

    public RegisterWindow(RegisterViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        Loaded += (_, _) => LastNameTextBox.Focus();
        // Не закрываем окно в момент сохранения: результат операции должен быть виден.
        Closing += (_, e) => e.Cancel = viewModel.IsBusy;
        Closed += (_, _) => { PasswordBox.Clear(); ConfirmationBox.Clear(); };
    }

    private async void RegisterButton_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.IsBusy) return;
        if (await viewModel.RegisterAsync(PasswordBox.Password, ConfirmationBox.Password))
        {
            PasswordBox.Clear();
            ConfirmationBox.Clear();
            DialogResult = true;
        }
    }
}
