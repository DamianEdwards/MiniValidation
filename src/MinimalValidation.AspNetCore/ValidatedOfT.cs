using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// Represents a validated object of the type specified by <typeparamref name="TValue"/> as a parameter to an ASP.NET Core route handler delegate.
/// The object will be deserialized from the request body using the configured <see cref="JsonOptions"/> instance
/// from the host's <see cref="IServiceProvider"/>. If the result of deserializing the request body to <typeparamref name="TValue"/>
/// is <c>null</c>, the value of the parameter will also be <c>null</c>.
/// </summary>
/// <typeparam name="TValue">The type of the object being validated.</typeparam>
public class Validated<TValue>
{
    private static readonly JsonSerializerOptions _defaultJsonSerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Initializes a new instance of the <see cref="Validated{TValue}"/> class.
    /// </summary>
    /// <param name="value">The object to validate.</param>
    public Validated(TValue value)
    {
        ArgumentNullException.ThrowIfNull(value, nameof(value));

        Value = value;
        IsValid = MinimalValidation.TryValidate(Value, out var errors);
        Errors = errors;
    }

    /// <summary>
    /// The validated object.
    /// </summary>
    public TValue Value { get; }

    /// <summary>
    /// Indicates whether the object is valid or not. <c>true</c> if the object is valid; <c>false</c> if it is not.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// A dictionary that contains details of each failed validation.
    /// </summary>
    public IDictionary<string, string[]> Errors { get; }

    /// <summary>
    /// Deconstructs the <see cref="Value"/> and <see cref="IsValid"/> properties.
    /// </summary>
    /// <param name="value">The value of <see cref="Value"/>.</param>
    /// <param name="isValid">The value of <see cref="IsValid"/>.</param>
    public void Deconstruct(out TValue value, out bool isValid)
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
    public void Deconstruct(out TValue value, out bool isValid, out IDictionary<string, string[]> errors)
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
#pragma warning disable IDE0060 // Remove unused parameter
    public static async ValueTask<Validated<TValue>?> BindAsync(HttpContext context, ParameterInfo parameter)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));
        ArgumentNullException.ThrowIfNull(parameter, nameof(parameter));

        if (!context.Request.HasJsonContentType())
        {
            throw new BadHttpRequestException(
                "Request Content-Type header was not a recognized JSON media type.",
                StatusCodes.Status415UnsupportedMediaType);
        }

        var jsonOptions = context.RequestServices.GetService<JsonOptions>();
        var jsonSerializerOptions = jsonOptions?.SerializerOptions ?? _defaultJsonSerializerOptions;

        var value = await context.Request.ReadFromJsonAsync(typeof(TValue), jsonSerializerOptions, context.RequestAborted);

        return value == null ? null : new Validated<TValue>((TValue)value);
    }
}