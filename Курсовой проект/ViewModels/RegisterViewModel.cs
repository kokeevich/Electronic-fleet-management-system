using System.ComponentModel;
using System.Runtime.CompilerServices;
using Курсовой_проект.Services;

namespace Курсовой_проект.ViewModels;

public sealed class RegisterViewModel(IRegistrationService registration) : INotifyPropertyChanged
{
    private string lastName = "", firstName = "", middleName = "", login = "", message = "";
    private bool busy;

    public string LastName { get => lastName; set { lastName = value; Changed(); } }
    public string FirstName { get => firstName; set { firstName = value; Changed(); } }
    public string MiddleName { get => middleName; set { middleName = value; Changed(); } }
    public string Login { get => login; set { login = value; Changed(); } }
    public string Message { get => message; private set { message = value; Changed(); } }
    public bool IsBusy
    {
        get => busy;
        private set { busy = value; Changed(); Changed(nameof(CanSubmit)); }
    }
    public bool CanSubmit => !IsBusy;

    public async Task<bool> RegisterAsync(string password, string confirmation)
    {
        if (IsBusy) return false;
        IsBusy = true;
        Message = "";
        try
        {
            var result = await registration.RegisterAsync(
                new RegistrationRequest(LastName, FirstName, MiddleName, Login, password, confirmation));
            Message = result.Message;
            if (result.Success) Login = Login.Trim();
            return result.Success;
        }
        catch (Exception)
        {
            // При обрыве ответа SQL операция могла успеть завершиться.
            Message = "Не удалось подтвердить регистрацию. Проверьте подключение к SQL Server. Если связь оборвалась при сохранении, попробуйте войти с указанными данными.";
            return false;
        }
        finally { IsBusy = false; }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
