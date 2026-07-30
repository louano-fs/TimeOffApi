using FluentValidation;
using TimeOffApi.Infrastructure;

namespace TimeOffApi.Services;

public static class ValidationService
{
    public static async Task ValidateOrThrowAsync<T>(
        this IValidator<T> validator,
        T model,
        CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(model, cancellationToken);
        if (!result.IsValid)
            throw new Infrastructure.ValidationException(
                "VALIDATION_ERROR",
                string.Join(" ", result.Errors.Select(x => x.ErrorMessage).Distinct()));
    }
}
