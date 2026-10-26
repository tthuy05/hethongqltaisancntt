using System.Text.Json.Serialization;

namespace ItAssetManagement.Application.Mvp;

public sealed class MaintenanceListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? AssetId { get; set; }
    public long? ReporterId { get; set; }
    public long? TechnicianId { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string? Keyword { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string SortBy { get; set; } = "openedAt";
    public string SortDirection { get; set; } = "desc";
}
public class MaintenanceVersionRequest
{
    public string? RowVersion { get; set; }
    public string? Note { get; set; }
}
public sealed class AssignTechnicianRequest : MaintenanceVersionRequest
{ public long TechnicianId { get; set; } }
public sealed class StartMaintenanceRequest : MaintenanceVersionRequest
{ [JsonPropertyName("startedAt")] public DateTime? StartedAtUtc { get; set; } }
public sealed class CancelMaintenanceRequest : MaintenanceVersionRequest
{ public string? Reason { get; set; } }
public sealed class FailMaintenanceRequest : MaintenanceVersionRequest
{
    private decimal? cost;
    public string? Reason { get; set; }
    public decimal? Cost { get => cost; set { cost = value; HasCost = true; } }
    [JsonIgnore] public bool HasCost { get; private set; }
    [JsonPropertyName("completedAt")] public DateTime? CompletedAtUtc { get; set; }
}
public sealed class UpdateMaintenanceRequest : MaintenanceVersionRequest
{
    private decimal? estimatedCost, actualCost;
    private DateTime? dueAt;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Priority { get; set; }
    [JsonPropertyName("dueAt")] public DateTime? DueAtUtc { get => dueAt; set { dueAt = value; HasDueAt = true; } }
    public decimal? EstimatedCost { get => estimatedCost; set { estimatedCost = value; HasEstimatedCost = true; } }
    public decimal? ActualCost { get => actualCost; set { actualCost = value; HasActualCost = true; } }
    [JsonIgnore] public bool HasDueAt { get; private set; }
    [JsonIgnore] public bool HasEstimatedCost { get; private set; }
    [JsonIgnore] public bool HasActualCost { get; private set; }
}
public sealed record MaintenanceHistoryDto(long Id, string EventType, string? FromStatus, string? ToStatus,
    long? FromAssignedToUserId, long? ToAssignedToUserId, string? Comment, decimal? Cost, long? PerformedByUserId,
    DateTime PerformedAtUtc, Guid CorrelationId);
