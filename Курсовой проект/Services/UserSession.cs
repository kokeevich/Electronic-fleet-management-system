namespace Курсовой_проект.Services;

public sealed record SessionUser(int UserId, string FullName, string Role);

public sealed class UserSession
{
    public SessionUser? Current { get; private set; }
    public int Version { get; private set; }
    internal void SignIn(SessionUser user) => Current = user;
    public void SignOut()
    {
        Current = null;
        Version++;
    }
}

public enum Permission
{
    ViewVehicles, ManageVehicles, ViewDrivers, ManageDrivers, ViewDocuments,
    ManageDocuments, ViewMaintenance, ManageMaintenance, ViewParts, ManageParts,
    ViewNotifications, FleetReports, MaintenanceReports, ManageUsers, ManageSettings
}

public sealed class AccessService(UserSession session)
{
    public static bool IsKnownRole(string role) =>
        role is "Администратор" or "Диспетчер" or "Механик" or "Наблюдатель";

    public bool Can(Permission permission)
    {
        if (!Enum.IsDefined(permission)) return false;
        return session.Current?.Role switch
        {
            "Администратор" => true,
            "Диспетчер" => permission is Permission.ViewVehicles or Permission.ManageVehicles
                or Permission.ViewDrivers or Permission.ManageDrivers or Permission.ViewDocuments
                or Permission.ManageDocuments or Permission.ViewMaintenance or Permission.ViewParts
                or Permission.ViewNotifications or Permission.FleetReports,
            "Механик" => permission is Permission.ViewVehicles or Permission.ViewDrivers
                or Permission.ViewDocuments or Permission.ViewMaintenance or Permission.ManageMaintenance
                or Permission.ViewParts or Permission.ManageParts or Permission.ViewNotifications
                or Permission.MaintenanceReports,
            "Наблюдатель" => permission is Permission.ViewVehicles or Permission.ViewDrivers
                or Permission.ViewDocuments or Permission.ViewMaintenance or Permission.ViewParts,
            _ => false
        };
    }

    public void Require(Permission permission)
    {
        if (!Can(permission))
            throw new UnauthorizedAccessException("Недостаточно прав для выполнения операции.");
    }
}
