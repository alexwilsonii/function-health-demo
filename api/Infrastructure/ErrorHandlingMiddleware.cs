namespace TaskManager.Api.Infrastructure;

/// <summary>
/// Converts request-body binding failures (malformed JSON, invalid enum / wrong type) into the SAME
/// 400 ValidationProblemDetails envelope used by the validators, and any unhandled exception into a
/// 500 ProblemDetails — so the frontend only ever has to consume two shapes.
/// </summary>
public sealed class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BadHttpRequestException ex) when (!context.Response.HasStarted)
        {
            _logger.LogInformation(ex, "Malformed request body.");
            var errors = new Dictionary<string, string[]>
            {
                ["request"] = new[] { "The request body is malformed or contains an invalid value." }
            };
            await Results.ValidationProblem(errors).ExecuteAsync(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            _logger.LogError(ex, "Unhandled exception.");
            await Results.Problem(
                title: "An unexpected error occurred.",
                statusCode: StatusCodes.Status500InternalServerError).ExecuteAsync(context);
        }
    }
}
