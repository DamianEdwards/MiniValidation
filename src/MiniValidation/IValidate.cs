using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MiniValidation;

/// <summary>
/// Provides a way for an object to be validated by an external validator.
/// </summary>
/// <typeparam name="TTarget">The type of object to validate.</typeparam>
public interface IValidate<in TTarget>
{
    /// <summary>
    /// Determines whether the specified object is valid.
    /// </summary>
    /// <param name="target">The object to validate.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <returns>A collection that holds failed-validation information.</returns>
    IEnumerable<ValidationResult> Validate(TTarget target, ValidationContext validationContext);
}
