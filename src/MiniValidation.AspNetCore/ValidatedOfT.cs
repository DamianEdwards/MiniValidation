using System.Reflection;
using Microsoft.AspNetCore.Http;

namespace MiniValidation.AspNetCore;

/// <summary>
/// Represents a validated object of the type specified by <typeparamref name="TValue"/> as a parameter to an ASP.NET Core route handler delegate.
/// </summary>
/// <typeparam name="TValue">The type of the object being validated.</typeparam>
public class Validated<TValue>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Validated{TValue}"/> class.
    /// </summary>
    /// <param name="value">The object to validate.</param>
    /// <param name="initialErrors">Any initial object-level errors to populate the <see cref="Errors"/> collection with.</param>
    public Validated(TValue? value, string[]? initialErrors)
    {
        var isValid = true;
        Value = value;

        if (Value != null)
        {
            isValid = MiniValidator.TryValidate(Value, out var errors);
            Errors = errors;
        }
        else
        {
            Errors = new Dictionary<string, string[]>();
        }

        if (initialErrors != null)
        {
            isValid = false;
            Errors.Add("", initialErrors);
        }

        IsValid = isValid;
    }

    /// <summary>
    /// The validated object.
    /// </summary>
    public TValue? Value { get; }

    /// <summary>
    /// Indicates whether the object is valid or not. <c>true</c> if the object is valid; <c>false</c> if it is not.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// A dictionary that contains details of each failed validation.
    /// </summary>
    public IDictionary<string, string[]> Errors { get; }

    /// <summary>
    /// Gets the response status code set by the default binding logic if there were any binding issues. This value will
    /// be <c>null</c> if the default binding logic did not detect an issue.
    /// </summary>
    public int? DefaultBindingResultStatusCode { get; init; }

    /// <summary>
    /// Deconstructs the <see cref="Value"/> and <see cref="IsValid"/> properties.
    /// </summary>
    /// <param name="value">The value of <see cref="Value"/>.</param>
    /// <param name="isValid">The value of <see cref="IsValid"/>.</param>
    public void Deconstruct(out TValue? value, out bool isValid)
    {
        value = Value;
        isValid = IsValid;
    }

    /// <summary>
    /// Deconstructs the <see cref="Value"/>, <see cref="IsValid"/>, and <see cref="Errors"/> properties.
    /// </summary>
    /// <param name="value">The value of <see cref="Value"/>.</param>
    /// <param name="isValid">The value of <see cref="IsValid"/>.</param>
    /// <param name="errors">The value of <see cref="Errors"/>.</param>
    public void Deconstruct(out TValue? value, out bool isValid, out IDictionary<string, string[]> errors)
    {
        value = Value;
        isValid = IsValid;
        errors = Errors;
    }

    /// <summary>
    /// Binds the specified parameter from <see cref="HttpContext.Request"/>. This method is called by the framework on your behalf
    /// when populating parameters of a mapped route handler.
    /// </summary>
    /// <param name="context">The <see cref="HttpContext"/> to bind the parameter from.</param>
    /// <param name="parameter">The route handler parameter being bound to.</param>
    /// <returns>An instance of <see cref="Validated{TValue}"/> if one is deserialized from the request, otherwise <c>null</c>.</returns>
    /// <exception cref="BadHttpRequestException">Thrown when the request Content-Type header is not a recognized JSON media type.</exception>
    public static async ValueTask<Validated<TValue>?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));
        ArgumentNullException.ThrowIfNull(parameter, nameof(parameter));

        var (value, statusCode) = await DefaultBinder<TValue>.GetValueAsync(context);

        if (statusCode != StatusCodes.Status200OK)
        {
            // Binding issue, add an error
            return new Validated<TValue>(default, new[] { $"An error occurred while processing the request." })
                { DefaultBindingResultStatusCode = statusCode };
        }

        return value == null ? null : new Validated<TValue>(value, null);
    }
}