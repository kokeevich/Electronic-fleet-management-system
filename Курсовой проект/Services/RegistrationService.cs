using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Курсовой_проект.Models;

namespace Курсовой_проект.Services;

public sealed record RegistrationRequest(string LastName, string FirstName, string MiddleName,
    string Login, string Password, string PasswordConfirmation);

public sealed record RegistrationResult(bool Success, string Message);
public enum CreateAccountResult { Created, DuplicateLogin, ObserverRoleUnavailable }

public interface IRegistrationStore
{
    Task<CreateAccountResult> CreateObserverAsync(string lastName, string firstName,
        string? middleName, string login, string passwordHash);
}

public sealed class RegistrationStore : IRegistrationStore
{
    public async Task<CreateAccountResult> CreateObserverAsync(string lastName, string firstName,
        string? middleName, string login, string passwordHash)
    {
        if (!PasswordHasher.IsSupportedHash(passwordHash))
            throw new ArgumentException("Ожидается хеш пароля.", nameof(passwordHash));

        await using var database = DatabaseFactory.Create();
        if (await database.Users.AnyAsync(user => user.Login == login))
            return CreateAccountResult.DuplicateLogin;

        // Роль определяется здесь, а не передаётся из формы. Числовой ID не фиксируем.
        var roles = await database.Roles.AsNoTracking()
            .Where(role => role.Name == "Наблюдатель").Take(2).ToListAsync();
        if (roles.Count != 1 || roles[0].Name != "Наблюдатель")
            return CreateAccountResult.ObserverRoleUnavailable;

        database.Users.Add(new User
        {
            LastName = lastName, FirstName = firstName, MiddleName = middleName,
            Login = login, Password = passwordHash, RoleId = roles[0].RoleId
        });
        try
        {
            await database.SaveChangesAsync();
            return CreateAccountResult.Created;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // UNIQUE(Login) защищает также от двух одновременных регистраций.
            return CreateAccountResult.DuplicateLogin;
        }
    }
}

public interface IRegistrationService
{
    Task<RegistrationResult> RegisterAsync(RegistrationRequest request);
}

public sealed class RegistrationService(IRegistrationStore store) : IRegistrationService
{
    public async Task<RegistrationResult> RegisterAsync(RegistrationRequest request)
    {
        var lastName = request.LastName?.Trim() ?? "";
        var firstName = request.FirstName?.Trim() ?? "";
        var middleName = request.MiddleName?.Trim() ?? "";
        var login = request.Login?.Trim() ?? "";

        if (lastName.Length == 0 || firstName.Length == 0)
            return new(false, "Введите фамилию и имя.");
        if (new[] { lastName, firstName, middleName }.Any(value =>
            value.Length > 100 || value.Any(char.IsControl)))
            return new(false, "ФИО: не более 100 символов в каждом поле, без управляющих символов.");
        if (login.Length is < 3 or > 100 ||
            login.Any(c => !char.IsLetterOrDigit(c) && c is not ('.' or '_' or '-')))
            return new(false, "Логин: от 3 до 100 символов. Используйте буквы, цифры, точку, дефис или подчёркивание.");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length is < 8 or > 128)
            return new(false, "Пароль должен содержать от 8 до 128 символов и не состоять только из пробелов.");
        if (!string.Equals(request.Password, request.PasswordConfirmation, StringComparison.Ordinal))
            return new(false, "Пароли не совпадают.");

        var hash = await Task.Run(() => PasswordHasher.Hash(request.Password));
        var result = await store.CreateObserverAsync(lastName, firstName,
            middleName.Length == 0 ? null : middleName, login, hash);
        return result switch
        {
            CreateAccountResult.Created => new(true, "Учётная запись создана. Теперь можно войти."),
            CreateAccountResult.DuplicateLogin => new(false, "Этот логин уже занят. Выберите другой."),
            CreateAccountResult.ObserverRoleUnavailable => new(false,
                "Регистрация недоступна: роль «Наблюдатель» не настроена. Обратитесь к администратору."),
            _ => new(false, "Не удалось создать учётную запись.")
        };
    }
}
