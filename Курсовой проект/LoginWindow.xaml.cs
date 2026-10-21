using System.Windows;
using Курсовой_проект.ViewModels;

namespace Курсовой_проект;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel viewModel;
    private bool closed;
    public event EventHandler? Authenticated;
    public event EventHandler? RegistrationRequested;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        Loaded += (_, _) => LoginTextBox.Focus();
        Closed += (_, _) => { closed = true; PasswordBox.Clear(); };
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        // Пароль не хранится во ViewModel и не записывается в настройки.
        if (viewModel.IsBusy) return;
        var password = PasswordBox.Password;
        PasswordBox.Clear();
        var success = await viewModel.SignInAsync(password);
        password = "";
        if (closed) return;
        if (success) Authenticated?.Invoke(this, EventArgs.Empty);
        else PasswordBox.Focus();
    }

    private void RegisterButton_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.IsBusy) return;
        PasswordBox.Clear();
        RegistrationRequested?.Invoke(this, EventArgs.Empty);
    }

    public void CompleteRegistration(string login)
    {
        viewModel.SetRegisteredLogin(login);
        PasswordBox.Clear();
        PasswordBox.Focus();
    }
}
