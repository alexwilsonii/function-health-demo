namespace TaskManager.Api.Contracts;

public record CreateTeamRequest(string Name);

public record AddMemberRequest(string Email);

public record TeamResponse(Guid Id, string Name, bool IsPersonal, int MemberCount);

public record MemberResponse(Guid UserId, string Email, DateTimeOffset JoinedAt);

public record TeamDetailResponse(Guid Id, string Name, bool IsPersonal, IReadOnlyList<MemberResponse> Members);
