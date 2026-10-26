using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Курсовой_проект.Services;

namespace Курсовой_проект.ViewModels;

public sealed class CatalogEditorViewModel(ICatalogEditService service, CatalogSection section, int id, bool resetPassword = false) : INotifyPropertyChanged
{
    private CatalogEditData? original;
    private bool busy;
    private string message = "", title = "Редактирование записи";
    public ObservableCollection<EditField> Fields { get; } = new();
    public string Title { get => title; private set { title = value; Changed(); } }
    public string Message { get => message; private set { message = value; Changed(); } }
    public bool CanSave => !IsBusy && original != null;
    public bool CanClose => !IsBusy;
    public bool IsBusy
    {
        get => busy;
        private set { busy = value; Changed(); Changed(nameof(CanSave)); Changed(nameof(CanClose)); }
    }
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        Message = "Загрузка записи…";
        original = null;
        Fields.Clear();
        try
        {
            original = await service.LoadAsync(section, id);
            Title = original.Title;
            if (resetPassword)
            {
                Title = "Сброс пароля · " + original.Original.GetValueOrDefault("Login");
                Fields.Add(new EditField(new("Password", "Новый пароль (8–128 символов)", MaxLength:128), "", []));
                Fields.Add(new EditField(new("PasswordConfirmation", "Повторите пароль", MaxLength:128), "", []));
                Message = "После сохранения сообщите пользователю новый пароль. Уже открытые сеансы останутся активными.";
                return;
            }
            foreach (var field in original.Fields) Fields.Add(field);
            Message = section == CatalogSection.Roles ? "Название роли закреплено правами доступа. Можно изменить описание."
                : section == CatalogSection.Users && id != 0 ? "Пароль не изменяется. Изменённая роль применяется при следующем входе."
                : "";
        }
        catch (UnauthorizedAccessException) { Message = "Недостаточно прав или роль изменилась. Войдите заново."; }
        catch (InvalidOperationException) { Message = "Не удалось открыть запись. Обновите список и проверьте подключение."; }
        catch (Exception) { Message = "Не удалось загрузить запись. Проверьте подключение к БД."; }
        finally { IsBusy = false; }
    }
    public async Task<bool> SaveAsync()
    {
        if (!CanSave) return false;
        IsBusy = true;
        try
        {
            var result = resetPassword
                ? await ((IPasswordResetService)service).ResetPasswordAsync(id,
                    Fields.Single(x => x.Key == "Password").Value, Fields.Single(x => x.Key == "PasswordConfirmation").Value)
                : await service.SaveAsync(original!, Fields.ToDictionary(x => x.Key, x => x.Value));
            Message = result.Message;
            return result.Success;
        }
        catch (UnauthorizedAccessException) { Message = "Недостаточно прав или роль изменилась. Войдите заново."; }
        catch (Exception)
        {
            Message = "Не удалось подтвердить сохранение. Проверьте подключение и обновите список перед повтором: запись могла сохраниться.";
        }
        finally { IsBusy = false; }
        return false;
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
