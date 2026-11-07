using Ausiannikau.Utilities.Results;
using FluentValidation.Results;

namespace ApiGateway.BusinessLogic.Validation;

internal static class ValidationResultExtensions
{
    public static Result ToResult(this ValidationResult validationResult)
    {
        return validationResult.IsValid
            ? Result.Succeed()
            : Result.Fail(string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage)));
    }
}
