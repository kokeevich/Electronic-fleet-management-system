using Курсовой_проект.Models;
using Курсовой_проект.Services;

namespace FleetManagement.PasswordMigration;

public static class MigrationPlan
{
    // Создаёт план в памяти: при ошибке ни одна исходная сущность не изменяется.
    public static Dictionary<int, string> Prepare(IReadOnlyList<User> users)
    {
        var pending = users.Where(user => !PasswordHasher.IsSupportedHash(user.Password)).ToList();
        if (pending.Any(user => string.IsNullOrEmpty(user.Password) ||
            user.Password.StartsWith("PBKDF2", StringComparison.OrdinalIgnoreCase) ||
            user.Password.StartsWith('$')))
            throw new InvalidOperationException("Обнаружен неподдерживаемый формат пароля.");

        return pending.ToDictionary(user => user.UserId, user => PasswordHasher.Hash(user.Password));
    }
}
