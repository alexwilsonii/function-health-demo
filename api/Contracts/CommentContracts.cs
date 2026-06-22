namespace TaskManager.Api.Contracts;

public record CreateCommentRequest(string Body);

public record CommentResponse(Guid Id, Guid TaskId, string Body, string AuthorEmail, DateTimeOffset CreatedAt);
