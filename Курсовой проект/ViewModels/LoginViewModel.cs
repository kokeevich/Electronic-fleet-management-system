using System.ComponentModel;
using System.Runtime.CompilerServices;
using Курсовой_проект.Services;

namespace Курсовой_проект.ViewModels;

public sealed class LoginViewModel(IAuthenticationService authentication) : INotifyPropertyChanged
{
    private string login = "";
    private string error = "";
    private bool busy;
    private string notice = "";
    public string Notice { get => notice; private set { notice = value; Changed(); } }
    public string Login { get => login; set { login = value; Changed(); } }
    public string Error { get => error; private set { error = value; Changed(); } }
    public bool IsBusy
    {
        get => busy;
        private set { busy = value; Changed(); Changed(nameof(CanSubmit)); }
    }
    public bool CanSubmit => !IsBusy;

    public void SetRegisteredLogin(string registeredLogin)
    {
        Login = registeredLogin;
        Error = "";
        Notice = "Учётная запись создана. Введите пароль, чтобы войти.";
    }

    public async Task<bool> SignInAsync(string password)
    {
        if (IsBusy) return false;
        Error = "";
        Notice = "";
        if (string.IsNullOrWhiteSpace(Login) || string.IsNullOrEmpty(password))
        {
            Error = "Введите логин и пароль.";
            return false;
        }
        IsBusy = true;
        try
        {
            if (await authentication.SignInAsync(Login, password)) return true;
            Error = "Не удалось войти. Проверьте логин и пароль или обратитесь к администратору.";
        }
        catch (Exception)
        {
            Error = "Не удалось подключиться к базе данных. Проверьте подключение к SQL Server и повторите вход.";
        }
        finally { IsBusy = false; }
        return false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
