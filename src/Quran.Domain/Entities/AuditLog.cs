namespace Quran.Domain.Entities;

public class AuditLog
{
    public long Id { get; set; }
    public string UserId { get; set; } = "system";
    public string? UserEmail { get; set; }
    public string Action { get; set; } = null!;
    public string EntityType { get; set; } = null!;
    public string EntityId { get; set; } = null!;
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? CorrelationId { get; set; }
}
