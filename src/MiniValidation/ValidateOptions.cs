using System;
using System.Text.Json;

namespace MiniValidation;

/// <summary>
/// Defines options for <see cref="MiniValidator"/> operations.
/// </summary>
public sealed class ValidateOptions
{
    /// <summary>
    /// Creates a new instance of <see cref="ValidateOptions"/>.
    /// </summary>
    public ValidateOptions()
    {

    }

    /// <summary>
    /// Creates a new instance of <see cref="ValidateOptions"/> with the specified options.
    /// </summary>
    /// <param name="validateOptions">The options to copy the values from.</param>
    public ValidateOptions(ValidateOptions validateOptions)
    {
        MaxDepth = validateOptions.MaxDepth;
        DisableRecursion = validateOptions.DisableRecursion;
        AllowAsyncValidation = validateOptions.AllowAsyncValidation;
        Services = validateOptions.Services;
        JsonNamingPolicy = validateOptions.JsonNamingPolicy;
    }

    /// <summary>
    /// Gets or sets the default <see cref="ValidateOptions"/> to use when none are passed to a validation method on <see cref="MiniValidator"/>.<br />
    /// Modify this property to configure the default options for all validation operations if not passing a <see cref="ValidateOptions"/> or <see cref="IServiceProvider"/>
    /// instance to validation methods.
    /// </summary>
    /// <remarks>
    /// Example:
    /// <code>
    /// <see cref="JsonNamingPolicy"/> = <see cref="JsonNamingPolicy.CamelCase"/>;
    /// var isValid = <see cref="MiniValidator"/>.TryValidate(person, out var errors);
    /// </code>
    /// </remarks>
    public static ValidateOptions Default { get; set; } = new ValidateOptions();

    /// <summary>
    /// Gets or sets the maximum depth allowed when validating an object with recursion enabled.
    /// Defaults to <c>32</c>.
    /// </summary>
    public int MaxDepth { get; set; } = 32;

    /// <summary>
    /// Gets or sets a value that determines whether to disable recursion when validating nested objects. Defaults to <c>false</c>.
    /// </summary>
    public bool DisableRecursion { get; set; }

    /// <summary>
    /// Gets or sets a value that determines whether to allow asynchronous validation via <see cref="IAsyncValidatableObject"/>. Defaults to <c>false</c>.
    /// </summary>
    /// <remarks>
    /// This option is only applicable when calling synchronous validation methods on <see cref="MiniValidator"/>,
    /// e.g. <see cref="MiniValidator.TryValidate{TTarget}(TTarget, bool, out System.Collections.Generic.IDictionary{string, string[]})"/>.
    /// </remarks>
    public bool AllowAsyncValidation { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="IServiceProvider"/> to use when resolving services for validation.
    /// </summary>
    public IServiceProvider? Services { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="System.Text.Json.JsonNamingPolicy"/> to use for error keys in validation messages.
    /// </summary>
    public JsonNamingPolicy? JsonNamingPolicy { get; set; }
}
