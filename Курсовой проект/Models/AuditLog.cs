namespace Курсовой_проект.Models;

public sealed class AuditLog
{
    public int AuditLogId { get; set; }
    public int ActorUserId { get; set; }
    public string ActorName { get; set; } = null!;
    public DateTime ChangedAt { get; set; }
    public string Action { get; set; } = null!;
    public string EntityName { get; set; } = null!;
    public int EntityId { get; set; }
    public string? BeforeData { get; set; }
    public string? AfterData { get; set; }
}
