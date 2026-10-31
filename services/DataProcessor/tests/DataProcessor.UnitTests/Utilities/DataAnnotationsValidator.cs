using System.ComponentModel.DataAnnotations;

namespace DataProcessor.UnitTests.Utilities;

internal static class DataAnnotationsValidator
{
    // The same check that ValidateDataAnnotations() runs on bound options.
    public static IReadOnlyList<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);

        return results;
    }
}
