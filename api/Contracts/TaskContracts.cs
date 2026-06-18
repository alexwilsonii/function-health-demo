using TaskManager.Api.Domain;

namespace TaskManager.Api.Contracts;

/// <summary>Create payload. Id is server-generated (see design doc §6j). Status/Priority default
/// to Todo/Medium when omitted.</summary>
public record CreateTaskRequest(
    string Title,
    string? Notes,
    TaskState? Status,
    TaskPriority? Priority,
    DateTimeOffset? DueAt);

/// <summary>Edit payload. PATCH carries the full editable state (the form is pre-populated with
/// current values), so null Notes/DueAt clear those fields. Title is always required.</summary>
public record UpdateTaskRequest(
    string Title,
    string? Notes,
    TaskState Status,
    TaskPriority Priority,
    DateTimeOffset? DueAt,
    bool IsPinned);

public record TaskResponse(
    Guid Id,
    string Title,
    string? Notes,
    TaskState Status,
    TaskPriority Priority,
    DateTimeOffset? DueAt,
    bool IsPinned,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);
