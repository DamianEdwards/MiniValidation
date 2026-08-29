using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace MiniValidation;

/// <summary>
/// Provides a way for an object to be validated asynchronously by an external validator.
/// </summary>
/// <typeparam name="TTarget">The type of object to validate.</typeparam>
public interface IAsyncValidate<in TTarget>
{
    /// <summary>
    /// Determines whether the specified object is valid.
    /// </summary>
    /// <param name="target">The object to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <returns>A collection that holds failed-validation information.</returns>
    Task<IEnumerable<ValidationResult>> ValidateAsync(TTarget target, ValidationContext validationContext);
}
