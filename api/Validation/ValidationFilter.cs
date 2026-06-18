using FluentValidation;

namespace TaskManager.Api.Validation;

/// <summary>
/// Runs the FluentValidation validator for <typeparamref name="T"/> before the handler and returns a
/// 400 ValidationProblemDetails (field-keyed, camelCased) on failure — the single error envelope the
/// whole API uses.
/// </summary>
public class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();
        if (validator is not null)
        {
            var model = context.Arguments.OfType<T>().FirstOrDefault();
            if (model is not null)
            {
                var result = await validator.ValidateAsync(model);
                if (!result.IsValid)
                {
                    var errors = result.Errors
                        .GroupBy(e => ToCamelCase(e.PropertyName))
                        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
                    return Results.ValidationProblem(errors);
                }
            }
        }

        return await next(context);
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) || char.IsLower(name[0])
            ? name
            : char.ToLowerInvariant(name[0]) + name[1..];
}
