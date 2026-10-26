using System.Text.Json;
using Курсовой_проект.Models;

namespace Курсовой_проект.Services;

internal static class AuditSnapshot
{
    internal static string EntityName(CatalogSection section) => section switch
    {
        CatalogSection.Drivers => "Водители", CatalogSection.Assignments => "Закрепления",
        CatalogSection.Documents => "Документы", CatalogSection.Maintenance => "ТО и ремонты",
        CatalogSection.Parts => "Запчасти", CatalogSection.WorkParts => "Состав работ",
        CatalogSection.Users => "Пользователи", CatalogSection.Roles => "Роли", _ => throw new UnauthorizedAccessException()
    };

    internal static string Json(object entity, CatalogSection section)
    {
        var values = CatalogEditRules.Fields(section).ToDictionary(x => x.Label,
            x => entity.GetType().GetProperty(x.Key)?.GetValue(entity)?.ToString() ?? "");
        return JsonSerializer.Serialize(values);
    }

    internal static AuditLog Create(SessionUser actor, string action, string entityName, int entityId,
        string? before, string? after) => new()
    {
        ActorUserId = actor.UserId, ActorName = actor.FullName, ChangedAt = DateTime.UtcNow,
        Action = action, EntityName = entityName, EntityId = entityId, BeforeData = before, AfterData = after
    };
}
