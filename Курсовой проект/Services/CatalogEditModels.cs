namespace Курсовой_проект.Services;

public sealed record EditOption(string Value, string Label);
public sealed record EditSpec(string Key, string Label, bool Required = true, int MaxLength = 0, string? Choice = null);
public sealed class EditField(EditSpec spec, string value, IReadOnlyList<EditOption> options)
{
    public string Key => spec.Key;
    public string Label => spec.Label + (spec.Required ? " *" : "");
    public bool IsPassword => spec.Key is "Password" or "PasswordConfirmation";
    public bool IsChoice => spec.Choice != null;
    public bool IsMultiline => spec.Key == "Description";
    public int MaxLength => spec.MaxLength > 0 ? spec.MaxLength : 100;
    public IReadOnlyList<EditOption> Options { get; } = options;
    public string Value { get; set; } = value;
}
public sealed record CatalogEditData(CatalogSection Section, int Id, string Title,
    IReadOnlyList<EditField> Fields, IReadOnlyDictionary<string, string> Original);
public sealed record CatalogEditResult(bool Success, string Message);

public interface ICatalogEditService
{
    Task<CatalogEditData> LoadAsync(CatalogSection section, int id);
    Task<CatalogEditResult> DeleteAsync(CatalogEditData original);
    Task<CatalogEditResult> SaveAsync(CatalogEditData original, IReadOnlyDictionary<string, string> values);
}

public static class CatalogEditRules
{
    public static Permission PermissionFor(CatalogSection section) => section switch
    {
        CatalogSection.Drivers or CatalogSection.Assignments => Permission.ManageDrivers,
        CatalogSection.Documents => Permission.ManageDocuments,
        CatalogSection.Maintenance or CatalogSection.WorkParts => Permission.ManageMaintenance,
        CatalogSection.Parts => Permission.ManageParts,
        CatalogSection.Users or CatalogSection.Roles => Permission.ManageUsers,
        _ => throw new UnauthorizedAccessException("Редактирование раздела недоступно.")
    };

    public static EditSpec[] Fields(CatalogSection section) => section switch
    {
        CatalogSection.Drivers => [
            new("LastName", "Фамилия", MaxLength:100), new("FirstName", "Имя", MaxLength:100),
            new("MiddleName", "Отчество", false,100), new("Phone", "Телефон", false,20),
            new("LicenseNumber", "Номер удостоверения", MaxLength:50), new("LicenseCategory", "Категории", MaxLength:20),
            new("LicenseIssueDate", "Выдано (дд.мм.гггг)"), new("LicenseExpiryDate", "Действует до (дд.мм.гггг)")],
        CatalogSection.Assignments => [
            new("VehicleId", "Автомобиль", Choice:"Vehicles"), new("DriverId", "Водитель", Choice:"Drivers"),
            new("StartDate", "Начало (дд.мм.гггг)"), new("EndDate", "Окончание (дд.мм.гггг)", false)],
        CatalogSection.Documents => [
            new("DocumentType", "Вид документа", Choice:"DocumentTypes"), new("DocumentNumber", "Номер", MaxLength:100),
            new("IssueDate", "Выдан (дд.мм.гггг)"), new("ExpiryDate", "Действует до (дд.мм.гггг)", false),
            new("IssuingOrganization", "Организация", false,200), new("VehicleId", "Автомобиль", false, Choice:"Vehicles"),
            new("DriverId", "Водитель", false, Choice:"Drivers"), new("Cost", "Стоимость, ₽", false)],
        CatalogSection.Maintenance => [
            new("VehicleId", "Автомобиль", Choice:"Vehicles"), new("WorkDate", "Дата работ (дд.мм.гггг)"),
            new("WorkType", "Вид работ", MaxLength:200), new("Mileage", "Пробег, км"), new("Cost", "Общая стоимость, ₽"),
            new("Description", "Описание", false,1000), new("NextMaintenanceDate", "Следующее ТО (дд.мм.гггг)", false),
            new("NextMaintenanceMileage", "Пробег следующего ТО, км", false)],
        CatalogSection.Parts => [
            new("Name", "Наименование", MaxLength:200), new("ArticleNumber", "Артикул", MaxLength:100),
            new("Cost", "Цена, ₽"), new("Quantity", "Остаток, шт."), new("Category", "Категория", false,100)],
        CatalogSection.WorkParts => [
            new("WorkId", "Работа", Choice:"Works"), new("PartId", "Запчасть", Choice:"Parts"), new("Quantity", "Количество, шт.")],
        CatalogSection.Users => [
            new("LastName", "Фамилия", MaxLength:100), new("FirstName", "Имя", MaxLength:100),
            new("MiddleName", "Отчество", false,100), new("Login", "Логин", MaxLength:100),
            new("RoleId", "Роль", Choice:"Roles")],
        CatalogSection.Roles => [new("Description", "Описание роли", false,500)],
        _ => throw new UnauthorizedAccessException()
    };
}
