using System.Data;
using Microsoft.EntityFrameworkCore;
using FleetManagement.PasswordMigration;
using Курсовой_проект.Services;

if (args.Length != 1 || args[0] is not ("--check" or "--confirm"))
{
    Console.WriteLine("Подготовка паролей исходной учебной БД.");
    Console.WriteLine("--check: проверить формат без изменения БД.");
    Console.WriteLine("--confirm: преобразовать открытые пароли после резервного копирования.");
    return args.Length == 1 && args[0] == "--help" ? 0 : 2;
}

try
{
    await using var database = DatabaseFactory.Create();
    if (args[0] == "--check")
    {
        var users = await database.Users.AsNoTracking().ToListAsync();
        var ready = users.Count(user => PasswordHasher.IsSupportedHash(user.Password));
        Console.WriteLine($"Всего пользователей: {users.Count}. Подготовлено: {ready}. Требует проверки: {users.Count - ready}.");
        return users.Count == ready ? 0 : 1;
    }

    // Не допускаем частичного преобразования или одновременного изменения паролей.
    await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
    var accounts = await database.Users.ToListAsync();
    var replacements = await Task.Run(() => MigrationPlan.Prepare(accounts));
    foreach (var account in accounts)
        if (replacements.TryGetValue(account.UserId, out var hash))
            account.Password = hash;
    await database.SaveChangesAsync();
    await transaction.CommitAsync();
    Console.WriteLine($"Подготовлено паролей: {replacements.Count}. Уже подготовлено: {accounts.Count - replacements.Count}.");
    return 0;
}
catch (Exception)
{
    Console.Error.WriteLine("Преобразование не подтверждено. Проверьте подключение, права и исходный формат паролей. Затем выполните --check. Подробности подключения и пароли не выводятся.");
    return 1;
}
