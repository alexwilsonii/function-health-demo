using TaskManager.Api.Domain;

namespace TaskManager.Api.Contracts;

/// <summary>Create payload. Id is server-generated; TeamId is required (must be a team you're in);
/// AssigneeUserId is optional (must be a member of that team). Status/Priority default to Todo/Medium.</summary>
public record CreateTaskRequest(
    Guid TeamId,
    string Title,
    string? Notes,
    TaskState? Status,
    TaskPriority? Priority,
    DateTimeOffset? DueAt,
    Guid? AssigneeUserId);

/// <summary>Edit payload (full editable state). The team is fixed after creation.</summary>
public record UpdateTaskRequest(
    string Title,
    string? Notes,
    TaskState Status,
    TaskPriority Priority,
    DateTimeOffset? DueAt,
    bool IsPinned,
    Guid? AssigneeUserId);

public record TaskResponse(
    Guid Id,
    Guid TeamId,
    string TeamName,
    string Title,
    string? Notes,
    TaskState Status,
    TaskPriority Priority,
    DateTimeOffset? DueAt,
    bool IsPinned,
    Guid CreatedByUserId,
    string CreatedByEmail,
    Guid? AssigneeUserId,
    string? AssigneeEmail,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);
