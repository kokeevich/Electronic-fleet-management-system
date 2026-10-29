namespace Курсовой_проект.Services;

public static class PasswordRules
{
    public const string Hint = "8–128 символов: заглавная и строчная буквы, цифра и специальный символ (например, !).";
    public static string? Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length is < 8 or > 128)
            return "Пароль должен содержать от 8 до 128 символов.";
        if (password.Any(char.IsControl)) return "Пароль не должен содержать управляющие символы.";
        if (!password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit) ||
            !password.Any(c => char.IsPunctuation(c) || char.IsSymbol(c)))
            return Hint;
        return null;
    }
}
