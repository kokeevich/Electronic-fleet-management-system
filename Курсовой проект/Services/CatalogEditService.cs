using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Курсовой_проект.Models;

namespace Курсовой_проект.Services;

public sealed class CatalogEditService : ICatalogEditService
{
    private readonly AccessService access;
    private readonly UserSession session;
    private readonly Func<OzvsKursachContext> createContext;
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");
    public CatalogEditService(AccessService access, UserSession session) : this(access, session, DatabaseFactory.Create) { }
    internal CatalogEditService(AccessService access, UserSession session, Func<OzvsKursachContext> createContext)
    {
        this.access = access; this.session = session; this.createContext = createContext;
    }
    private async Task CheckActorAsync(OzvsKursachContext db, CatalogSection section)
    {
        access.Require(CatalogEditRules.PermissionFor(section));
        var role = await db.Users.Where(x => x.UserId == session.Current!.UserId).Select(x => x.Role.Name).SingleOrDefaultAsync();
        if (role != session.Current?.Role) throw new UnauthorizedAccessException("Права изменились. Войдите заново.");
    }
    public async Task<CatalogEditData> LoadAsync(CatalogSection section, int id)
    {
        access.Require(CatalogEditRules.PermissionFor(section));
        var actor = session.Current;
        await using var db = createContext();
        await CheckActorAsync(db, section);
        if (id == 0 && section == CatalogSection.Roles) throw new UnauthorizedAccessException();
        var entity = (id == 0 ? NewEntity(section) : await FindAsync(db, section, id)) ?? throw new InvalidOperationException("Запись удалена. Обновите список.");
        var specs = CatalogEditRules.Fields(section);
        var original = Snapshot(entity, specs);
        if (id == 0 && section == CatalogSection.Drivers)
        {
            original["LicenseIssueDate"] = "";
            original["LicenseExpiryDate"] = "";
        }
        var fields = new List<EditField>();
        foreach (var spec in specs)
            fields.Add(new EditField(spec, original[spec.Key], await OptionsAsync(db, spec)));
        if (!ReferenceEquals(actor, session.Current)) throw new UnauthorizedAccessException();
        if (id == 0 && section == CatalogSection.Users)
        {
            fields.Add(new EditField(new("Password", "Пароль (8–128 символов)", MaxLength:128), "", []));
            fields.Add(new EditField(new("PasswordConfirmation", "Повторите пароль", MaxLength:128), "", []));
        }
        var title = (id == 0 ? "Добавление · " : "Изменение · ") + CatalogDefinition.All.Single(x => x.Section == section).Title;
        if (entity is Role role) title += " · " + role.Name;
        return new(section, id, title, fields, original);
    }

    public async Task<CatalogEditResult> SaveAsync(CatalogEditData original, IReadOnlyDictionary<string, string> values)
    {
        access.Require(CatalogEditRules.PermissionFor(original.Section));
        var actor = session.Current;
        await using var db = createContext();
        // Чтение, проверка конфликтов/связей и запись выполняются одной транзакцией.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await CheckActorAsync(db, original.Section);
        var creating = original.Id == 0;
        if (creating && original.Section == CatalogSection.Roles) throw new UnauthorizedAccessException();
        var entity = creating ? NewEntity(original.Section) : await FindAsync(db, original.Section, original.Id);
        if (entity == null) return new(false, "Запись удалена. Обновите список.");
        var specs = CatalogEditRules.Fields(original.Section);
        var current = Snapshot(entity, specs);
        if (!creating && current.Any(x => !original.Original.TryGetValue(x.Key, out var old) || old != x.Value))
            return new(false, "Запись изменена другим пользователем. Закройте карточку, обновите список и откройте её заново.");
        foreach (var spec in specs)
        {
            var raw = values.GetValueOrDefault(spec.Key)?.Trim() ?? "";
            if (spec.Required && raw.Length == 0) return new(false, $"Заполните поле «{spec.Label}».");
            if (spec.MaxLength > 0 && raw.Length > spec.MaxLength) return new(false, $"«{spec.Label}»: максимум {spec.MaxLength} символов.");
            if (raw.Any(c => char.IsControl(c) && !(spec.Key == "Description" && c is '\r' or '\n' or '\t')))
                return new(false, $"«{spec.Label}»: управляющие символы недопустимы.");
            if (spec.Choice != null && !(await OptionsAsync(db, spec)).Any(x => x.Value == raw))
                return new(false, $"Выберите доступное значение поля «{spec.Label}».");
            var property = entity.GetType().GetProperty(spec.Key)!;
            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            object? value;
            if (raw.Length == 0 && !spec.Required) value = null;
            else if (type == typeof(string)) value = raw;
            else if (type == typeof(int))
            {
                if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                    return new(false, $"«{spec.Label}»: введите целое неотрицательное число.");
                value = number;
            }
            else if (type == typeof(decimal))
            {
                if (!decimal.TryParse(raw.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var money) ||
                    money < 0 || money > 9999999999.99m || decimal.Round(money, 2) != money)
                    return new(false, $"«{spec.Label}»: введите сумму от 0 до 9 999 999 999,99 с двумя знаками после запятой.");
                value = money;
            }
            else if (type == typeof(DateOnly))
            {
                if (!DateOnly.TryParseExact(raw, "dd.MM.yyyy", Ru, DateTimeStyles.None, out var date))
                    return new(false, $"«{spec.Label}»: используйте формат дд.мм.гггг.");
                value = date;
            }
            else return new(false, "Тип поля не поддерживается.");
            property.SetValue(entity, value);
        }
        if (creating && entity is User user)
        {
            var password = values.GetValueOrDefault("Password") ?? "";
            if (password.Length < 8 || password.Length > 128 || string.IsNullOrWhiteSpace(password))
                return new(false, "Пароль должен содержать от 8 до 128 символов.");
            if (password != values.GetValueOrDefault("PasswordConfirmation"))
                return new(false, "Пароли не совпадают.");
            user.Password = PasswordHasher.Hash(password);
        }
        var error = await ValidateAsync(db, entity);
        if (error != null) return new(false, error);
        access.Require(CatalogEditRules.PermissionFor(original.Section));
        if (!ReferenceEquals(actor, session.Current)) throw new UnauthorizedAccessException();
        try
        {
            if (creating) db.Add(entity);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return new(true, "Изменения сохранены.");
        }
        catch (DbUpdateConcurrencyException) { return new(false, "Запись изменена. Обновите список."); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { return new(false, "Такой логин, артикул или номер удостоверения уже существует."); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
        { return new(false, "Связанная запись удалена или данные нарушают ограничения БД. Обновите список."); }
    }

    // Новые записи используют ту же форму и те же проверки, что и редактирование.
    private static object NewEntity(CatalogSection section) => section switch
    {
        CatalogSection.Drivers => new Driver(),
        CatalogSection.Assignments => new VehicleAssignment { StartDate = DateOnly.FromDateTime(DateTime.Today) },
        CatalogSection.Documents => new Document { IssueDate = DateOnly.FromDateTime(DateTime.Today) },
        CatalogSection.Maintenance => new MaintenanceRepair { WorkDate = DateOnly.FromDateTime(DateTime.Today) },
        CatalogSection.Parts => new Part(),
        CatalogSection.WorkParts => new WorkPart { Quantity = 1 },
        CatalogSection.Users => new User(),
        _ => throw new UnauthorizedAccessException()
    };

    public async Task<CatalogEditResult> DeleteAsync(CatalogEditData original)
    {
        access.Require(CatalogEditRules.PermissionFor(original.Section));
        if (original.Section == CatalogSection.Roles) return new(false, "Системные роли нельзя удалять.");
        var actor = session.Current;
        await using var db = createContext();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await CheckActorAsync(db, original.Section);
        var entity = await FindAsync(db, original.Section, original.Id);
        if (entity == null) return new(false, "Запись уже удалена. Обновите список.");
        if (Snapshot(entity, CatalogEditRules.Fields(original.Section))
            .Any(x => !original.Original.TryGetValue(x.Key, out var old) || old != x.Value))
            return new(false, "Запись изменена другим пользователем. Обновите список перед удалением.");
        var linked = entity switch
        {
            Driver x => await db.VehicleAssignments.AnyAsync(y => y.DriverId == x.DriverId) ||
                        await db.Documents.AnyAsync(y => y.DriverId == x.DriverId),
            MaintenanceRepair x => await db.WorkParts.AnyAsync(y => y.WorkId == x.WorkId),
            Part x => await db.WorkParts.AnyAsync(y => y.PartId == x.PartId),
            User x => await db.Notifications.AnyAsync(y => y.UserId == x.UserId),
            _ => false
        };
        if (linked) return new(false, "Удаление невозможно: на запись ссылаются другие данные.");
        if (entity is User user)
        {
            if (user.UserId == actor!.UserId) return new(false, "Нельзя удалить собственную учётную запись.");
            if (await db.Roles.AnyAsync(x => x.RoleId == user.RoleId && x.Name == "Администратор") &&
                !await db.Users.AnyAsync(x => x.UserId != user.UserId && x.Role.Name == "Администратор"))
                return new(false, "В системе должен остаться администратор.");
        }
        if (!ReferenceEquals(actor, session.Current)) throw new UnauthorizedAccessException();
        db.Remove(entity);
        try
        {
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return new(true, "Запись удалена.");
        }
        catch (DbUpdateConcurrencyException) { return new(false, "Запись изменена. Обновите список."); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
        { return new(false, "Удаление невозможно: запись используется в других данных."); }
    }

    private async Task<string?> ValidateAsync(OzvsKursachContext db, object entity)
    {
        switch (entity)
        {
            case Driver x:
                if (x.LicenseExpiryDate < x.LicenseIssueDate) return "Срок удостоверения не может закончиться раньше даты выдачи.";
                if (await db.Drivers.AnyAsync(y => y.DriverId != x.DriverId && y.LicenseNumber == x.LicenseNumber))
                    return "Номер удостоверения уже используется.";
                break;
            case VehicleAssignment x:
                if (x.EndDate < x.StartDate) return "Окончание закрепления раньше начала.";
                if (await db.VehicleAssignments.AnyAsync(y => y.AssignmentId != x.AssignmentId &&
                    (y.VehicleId == x.VehicleId || y.DriverId == x.DriverId) &&
                    (!x.EndDate.HasValue || y.StartDate <= x.EndDate.Value) &&
                    (!y.EndDate.HasValue || y.EndDate.Value >= x.StartDate)))
                    return "В этом периоде водитель или автомобиль уже имеет закрепление. Даты окончания и начала включаются в период.";
                break;
            case Document x:
                if (x.ExpiryDate < x.IssueDate) return "Окончание действия документа раньше даты выдачи.";
                if (x.DocumentType == "Водительское удостоверение")
                {
                    if (!x.DriverId.HasValue || x.VehicleId.HasValue) return "Удостоверение должно относиться только к водителю.";
                }
                else if (!x.VehicleId.HasValue || x.DriverId.HasValue) return "Полис или паспорт ТС должен относиться только к автомобилю.";
                if (x.DocumentType is "ОСАГО" or "КАСКО" && !x.ExpiryDate.HasValue) return "Для страховки укажите дату окончания.";
                break;
            case MaintenanceRepair x:
                if (x.NextMaintenanceDate < x.WorkDate || x.NextMaintenanceMileage < x.Mileage)
                    return "Дата и пробег следующего ТО не могут предшествовать выполненным работам.";
                break;
            case Part x:
                if (await db.Parts.AnyAsync(y => y.PartId != x.PartId && y.ArticleNumber == x.ArticleNumber))
                    return "Артикул уже используется.";
                break;
            case WorkPart x:
                if (x.Quantity <= 0) return "Количество запчастей в работе должно быть больше нуля.";
                if (await db.WorkParts.AnyAsync(y => y.CompositionId != x.CompositionId && y.WorkId == x.WorkId && y.PartId == x.PartId))
                    return "Эта запчасть уже включена в выбранную работу.";
                break;
            case User x:
                if (x.Login.Length < 3 || x.Login.Any(c => !char.IsLetterOrDigit(c) && c is not ('.' or '_' or '-')))
                    return "Логин: от 3 символов, буквы, цифры, точка, дефис или подчёркивание.";
                if (await db.Users.AnyAsync(y => y.UserId != x.UserId && y.Login == x.Login)) return "Логин уже занят.";
                var newRole = await db.Roles.Where(y => y.RoleId == x.RoleId).Select(y => y.Name).SingleAsync();
                if (x.UserId == session.Current!.UserId && newRole != session.Current.Role)
                    return "Нельзя изменить собственную роль в текущем сеансе.";
                if (x.UserId == 0) break;
                var oldRoleId = db.Entry(x).Property(y => y.RoleId).OriginalValue;
                var oldRole = await db.Roles.Where(y => y.RoleId == oldRoleId).Select(y => y.Name).SingleAsync();
                if (oldRole == "Администратор" && newRole != "Администратор" &&
                    !await db.Users.AnyAsync(y => y.UserId != x.UserId && y.Role.Name == "Администратор"))
                    return "В системе должен остаться хотя бы один администратор.";
                break;
        }
        return null;
    }

    private static async Task<object?> FindAsync(OzvsKursachContext db, CatalogSection section, int id) => section switch
    {
        CatalogSection.Drivers => await db.Drivers.FindAsync(id),
        CatalogSection.Assignments => await db.VehicleAssignments.FindAsync(id),
        CatalogSection.Documents => await db.Documents.FindAsync(id),
        CatalogSection.Maintenance => await db.MaintenanceRepairs.FindAsync(id),
        CatalogSection.Parts => await db.Parts.FindAsync(id),
        CatalogSection.WorkParts => await db.WorkParts.FindAsync(id),
        CatalogSection.Users => await db.Users.FindAsync(id),
        CatalogSection.Roles => await db.Roles.FindAsync(id),
        _ => throw new UnauthorizedAccessException()
    };

    private static Dictionary<string, string> Snapshot(object entity, EditSpec[] specs) => specs.ToDictionary(
        spec => spec.Key, spec => entity.GetType().GetProperty(spec.Key)!.GetValue(entity) switch
        {
            null => "",
            DateOnly date => date.ToString("dd.MM.yyyy", Ru),
            decimal number => number.ToString("0.##", CultureInfo.InvariantCulture),
            object value => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
        });

    private static async Task<IReadOnlyList<EditOption>> OptionsAsync(OzvsKursachContext db, EditSpec spec)
    {
        List<EditOption> options;
        switch (spec.Choice)
        {
            case null: return Array.Empty<EditOption>();
            case "Vehicles":
                options = (await db.Vehicles.AsNoTracking().OrderBy(x => x.LicensePlate)
                    .Select(x => new { x.VehicleId, x.LicensePlate, x.Brand, x.Model }).ToListAsync())
                    .Select(x => new EditOption(x.VehicleId.ToString(), $"{x.LicensePlate} · {x.Brand} {x.Model}")).ToList(); break;
            case "Drivers":
                options = (await db.Drivers.AsNoTracking().OrderBy(x => x.LastName)
                    .Select(x => new { x.DriverId, x.LastName, x.FirstName, x.MiddleName, x.LicenseNumber }).ToListAsync())
                    .Select(x => new EditOption(x.DriverId.ToString(), $"{x.LastName} {x.FirstName} {x.MiddleName} · {x.LicenseNumber}")).ToList(); break;
            case "Parts":
                options = (await db.Parts.AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.PartId, x.Name, x.ArticleNumber }).ToListAsync())
                    .Select(x => new EditOption(x.PartId.ToString(), $"{x.Name} · {x.ArticleNumber}")).ToList(); break;
            case "Works":
                options = (await db.MaintenanceRepairs.AsNoTracking().OrderByDescending(x => x.WorkDate)
                    .Select(x => new { x.WorkId, x.WorkDate, x.WorkType, x.Vehicle.LicensePlate }).ToListAsync())
                    .Select(x => new EditOption(x.WorkId.ToString(), $"№{x.WorkId} · {x.LicensePlate} · {x.WorkDate:dd.MM.yyyy} · {x.WorkType}")).ToList(); break;
            case "Roles":
                options = (await db.Roles.AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.RoleId, x.Name }).ToListAsync())
                    .Where(x => AccessService.IsKnownRole(x.Name)).Select(x => new EditOption(x.RoleId.ToString(), x.Name)).ToList(); break;
            case "DocumentTypes":
                options = new[] { "ОСАГО", "КАСКО", "Водительское удостоверение", "Паспорт ТС" }.Select(x => new EditOption(x, x)).ToList(); break;
            default: throw new InvalidOperationException();
        }
        if (!spec.Required) options.Insert(0, new EditOption("", "Не указан"));
        return options;
    }
}
