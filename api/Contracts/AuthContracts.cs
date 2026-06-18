namespace TaskManager.Api.Contracts;

public record RegisterRequest(string Email, string Password);

public record LoginRequest(string Email, string Password);

public record UserResponse(Guid Id, string Email);
