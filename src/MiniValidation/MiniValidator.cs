using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace MiniValidation;

/// <summary>
/// Contains methods and properties for performing validation operations with <see cref="Validator"/> on objects whos properties
/// are decorated with <see cref="ValidationAttribute"/>s.
/// </summary>
public static class MiniValidator
{
    private static readonly TypeDetailsCache _typeDetailsCache = new();
    private static readonly IDictionary<string, string[]> _emptyErrors = new ReadOnlyDictionary<string, string[]>(new Dictionary<string, string[]>());

    /// <summary>
    /// Gets or sets the maximum depth allowed when validating an object with recursion enabled.
    /// Defaults to 32.
    /// </summary>
    public static int MaxDepth { get; set; } = 32;

    /// <summary>
    /// Determines if the specified <see cref="Type"/> has anything to validate.
    /// </summary>
    /// <remarks>
    /// Objects of types with nothing to validate will always return <c>true</c> when passed to <see cref="TryValidate{TTarget}(TTarget, bool, out IDictionary{string, string[]})"/>.
    /// </remarks>
    /// <param name="targetType">The <see cref="Type"/>.</param>
    /// <param name="recurse"><c>true</c> to recursively check descendant types; if <c>false</c> only simple values directly on the target type are checked.</param>
    /// <returns><c>true</c> if <paramref name="targetType"/> has anything to validate, <c>false</c> if not.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="targetType"/> is <c>null</c>.</exception>
    public static bool RequiresValidation(Type targetType, bool recurse = true)
    {
        if (targetType is null)
        {
            throw new ArgumentNullException(nameof(targetType));
        }

        return typeof(IValidatableObject).IsAssignableFrom(targetType)
            || typeof(IAsyncValidatableObject).IsAssignableFrom(targetType)
            || (recurse && typeof(IEnumerable).IsAssignableFrom(targetType))
            || _typeDetailsCache.Get(targetType).Properties.Any(p => p.HasValidationAttributes || recurse);
    }

    /// <summary>
    /// Determines whether the specific object is valid. This method recursively validates descendant objects.
    /// </summary>
    /// <param name="target">The object to validate.</param>
    /// <param name="errors">A dictionary that contains details of each failed validation.</param>
    /// <returns><c>true</c> if <paramref name="target"/> is valid; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="target"/> is <c>null</c>.</exception>
    public static bool TryValidate<TTarget>(TTarget target, out IDictionary<string, string[]> errors)
    {
        return TryValidateImpl(target, null, recurse: true, allowAsync: false, out errors);
    }

    /// <summary>
    /// Determines whether the specific object is valid. This method recursively validates descendant objects.
    /// </summary>
    /// <param name="target">The object to validate.</param>
    /// <param name="serviceProvider">The service provider to use when creating ValidationContext.</param>
    /// <param name="errors">A dictionary that contains details of each failed validation.</param>
    /// <returns><c>true</c> if <paramref name="target"/> is valid; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="target"/> is <c>null</c>.</exception>
    public static bool TryValidate<TTarget>(TTarget target, IServiceProvider serviceProvider, out IDictionary<string, string[]> errors)
    {
        if (serviceProvider is null)
        {
            throw new ArgumentNullException(nameof(serviceProvider));
        }

        return TryValidateImpl(target, serviceProvider, recurse: true, allowAsync: false, out errors);
    }

    /// <summary>
    /// Determines whether the specific object is valid.
    /// </summary>
    /// <typeparam name="TTarget">The type of the target of validation.</typeparam>
    /// <param name="target">The object to validate.</param>
    /// <param name="recurse"><c>true</c> to recursively validate descendant objects; if <c>false</c> only simple values directly on <paramref name="target"/> are validated.</param>
    /// <param name="errors">A dictionary that contains details of each failed validation.</param>
    /// <returns><c>true</c> if <paramref name="target"/> is valid; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="target"/> is <c>null</c>.</exception>
    public static bool TryValidate<TTarget>(TTarget target, bool recurse, out IDictionary<string, string[]> errors)
    {
        return TryValidateImpl(target, null, recurse, allowAsync: false, out errors);
    }

    /// <summary>
    /// Determines whether the specific object is valid.
    /// </summary>
    /// <typeparam name="TTarget">The type of the target of validation.</typeparam>
    /// <param name="target">The object to validate.</param>
    /// <param name="serviceProvider">The service provider to use when creating ValidationContext.</param>
    /// <param name="recurse"><c>true</c> to recursively validate descendant objects; if <c>false</c> only simple values directly on <paramref name="target"/> are validated.</param>
    /// <param name="errors">A dictionary that contains details of each failed validation.</param>
    /// <returns><c>true</c> if <paramref name="target"/> is valid; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="target"/> is <c>null</c>.</exception>
    public static bool TryValidate<TTarget>(TTarget target, IServiceProvider serviceProvider, bool recurse, out IDictionary<string, string[]> errors)
    {
        if (serviceProvider is null)
        {
            throw new ArgumentNullException(nameof(serviceProvider));
        }

        return TryValidateImpl(target, serviceProvider, recurse, allowAsync: false, out errors);
    }

    /// <summary>
    /// Determines whether the specific object is valid.
    /// </summary>
    /// <typeparam name="TTarget"></typeparam>
    /// <param name="target">The object to validate.</param>
    /// <param name="recurse"><c>true</c> to recursively validate descendant objects; if <c>false</c> only simple values directly on <paramref name="target"/> are validated.</param>
    /// <param name="allowAsync"><c>true</c> to allow asynchronous validation if an object in the graph requires it.</param>
    /// <param name="errors">A dictionary that contains details of each failed validation.</param>
    /// <returns><c>true</c> if <paramref name="target"/> is valid; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="target"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Throw when <paramref name="target"/> requires async validation and <paramref name="allowAsync"/> is <c>false</c>.</exception>
    public static bool TryValidate<TTarget>(TTarget target, bool recurse, bool allowAsync, out IDictionary<string, string[]> errors)
    {
        return TryValidateImpl(target, null, recurse, allowAsync, out errors);
    }

    /// <summary>
    /// Determines whether the specific object is valid.
    /// </summary>
    /// <typeparam name="TTarget"></typeparam>
    /// <param name="target">The object to validate.</param>
    /// <param name="serviceProvider">The service provider to use when creating ValidationContext.</param>
    /// <param name="recurse"><c>true</c> to recursively validate descendant objects; if <c>false</c> only simple values directly on <paramref name="target"/> are validated.</param>
    /// <param name="allowAsync"><c>true</c> to allow asynchronous validation if an object in the graph requires it.</param>
    /// <param name="errors">A dictionary that contains details of each failed validation.</param>
    /// <returns><c>true</c> if <paramref name="target"/> is valid; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="target"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Throw when <paramref name="target"/> requires async validation and <paramref name="allowAsync"/> is <c>false</c>.</exception>
    public static bool TryValidate<TTarget>(TTarget target, IServiceProvider? serviceProvider, bool recurse, bool allowAsync, out IDictionary<string, string[]> errors)
    {
        return TryValidateImpl(target, serviceProvider, recurse, allowAsync, out errors);
    }

    /// <summary>
    /// Determines whether the specific object is valid.
    /// </summary>
    /// <typeparam name="TTarget"></typeparam>
    /// <param name="target">The object to validate.</param>
    /// <param name="serviceProvider">The service provider to use when creating ValidationContext.</param>
    /// <param name="recurse"><c>true</c> to recursively validate descendant objects; if <c>false</c> only simple values directly on <paramref name="target"/> are validated.</param>
    /// <param name="allowAsync"><c>true</c> to allow asynchronous validation if an object in the graph requires it.</param>
    /// <param name="errors">A dictionary that contains details of each failed validation.</param>
    /// <returns><c>true</c> if <paramref name="target"/> is valid; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="target"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Throw when <paramref name="target"/> requires async validation and <paramref name="allowAsync"/> is <c>false</c>.</exception>
    private static bool TryValidateImpl<TTarget>(TTarget target, IServiceProvider? serviceProvider, bool recurse, bool allowAsync, out IDictionary<string, string[]> errors)
    {
        if (target is null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        if (!RequiresValidation(target.GetType(), recurse))
        {
            errors = _emptyErrors;

            // Return true for types with nothing to validate
            return true;
        }

        if (_typeDetailsCache.Get(target.GetType()).RequiresAsync && !allowAsync)
        {
            throw new ArgumentException($"The target type {target.GetType().Name} requires async validation. Call the '{nameof(TryValidateAsync)}' method instead.", nameof(target));
        }

        var validatedObjects = new Dictionary<object, bool?>();
        var workingErrors = new Dictionary<string, List<string>>();

        var validateTask = TryValidateImpl(target, serviceProvider, recurse, allowAsync, workingErrors, validatedObjects);

        bool isValid;

        if (validateTask.IsCompleted)
        {
            isValid = validateTask.GetAwaiter().GetResult();
        }
        else
        {
            // This is a backstop check as TryValidateImpl and the methods it calls should all be doing this check as the object
            // graph is walked during validation.
            try
            {
                ThrowIfAsyncNotAllowed(validateTask.IsCompleted, allowAsync);
            }
            catch (Exception)
            {
#if NET6_0_OR_GREATER
                // Always observe the ValueTask
                _ = validateTask.AsTask().GetAwaiter().GetResult();
#else
                _ = validateTask.GetAwaiter().GetResult();
#endif
                throw;
            }

#if NET6_0_OR_GREATER
            isValid = validateTask.AsTask().GetAwaiter().GetResult();
#else
            isValid = validateTask.GetAwaiter().GetResult();
#endif
        }

        errors = MapToFinalErrorsResult(workingErrors);

        return isValid;
    }

    /// <summary>
    /// Determines whether the specific object is valid.
    /// </summary>
    /// <param name="target">The object to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="target"/> is <c>null</c>.</exception>
#if NET6_0_OR_GREATER
    public static ValueTask<(bool IsValid, IDictionary<string, string[]> Errors)> TryValidateAsync<TTarget>(TTarget target)
#else
    public static Task<(bool IsValid, IDictionary<string, string[]> Errors)> TryValidateAsync<TTarget>(TTarget target)
#endif
    {
        return TryValidateImplAsync(target, null, recurse: true);
    }

    /// <summary>
    /// Determines whether the specific object is valid.
    /// </summary>
    /// <param name="target">The object to validate.</param>
    /// <param name="serviceProvider">The service provider to use when creating ValidationContext.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="target"/> is <c>null</c>.</exception>
#if NET6_0_OR_GREATER
    public static ValueTask<(bool IsValid, IDictionary<string, string[]> Errors)> TryValidateAsync<TTarget>(TTarget target, IServiceProvider serviceProvider)
#else
    public static Task<(bool IsValid, IDictionary<string, string[]> Errors)> TryValidateAsync<TTarget>(TTarget target, IServiceProvider serviceProvider)
#endif
    {
        if (serviceProvider is null)
        {
            throw new ArgumentNullException(nameof(serviceProvider));
        }

        return TryValidateImplAsync(target, serviceProvider, recurse: true);
    }

    /// <summary>
    /// Determines whether the specific object is valid.
    /// </summary>
    /// <param name="target">The object to validate.</param>
    /// <param name="recurse"><c>true</c> to recursively validate descendant objects; if <c>false</c> only simple values directly on <paramref name="target"/> are validated.</param>
    /// <returns><c>true</c> if <paramref name="target"/> is valid; otherwise <c>false</c> and the validation errors.</returns>
    /// <exception cref="ArgumentNullException"></exception>
#if NET6_0_OR_GREATER
    public static ValueTask<(bool IsValid, IDictionary<string, string[]> Errors)> TryValidateAsync<TTarget>(TTarget target, bool recurse)
#else
    public static Task<(bool IsValid, IDictionary<string, string[]> Errors)> TryValidateAsync<TTarget>(TTarget target, bool recurse)
#endif
    {
        return TryValidateImplAsync(target, null, recurse);
    }

    /// <summary>
    /// Determines whether the specific object is valid.
    /// </summary>
    /// <param name="target">The object to validate.</param>
    /// <param name="serviceProvider">The service provider to use when creating ValidationContext.</param>
    /// <param name="recurse"><c>true</c> to recursively validate descendant objects; if <c>false</c> only simple values directly on <paramref name="target"/> are validated.</param>
    /// <returns><c>true</c> if <paramref name="target"/> is valid; otherwise <c>false</c> and the validation errors.</returns>
    /// <exception cref="ArgumentNullException"></exception>
#if NET6_0_OR_GREATER
    public static ValueTask<(bool IsValid, IDictionary<string, string[]> Errors)> TryValidateAsync<TTarget>(TTarget target, IServiceProvider? serviceProvider, bool recurse)
#else
    public static Task<(bool IsValid, IDictionary<string, string[]> Errors)> TryValidateAsync<TTarget>(TTarget target, IServiceProvider? serviceProvider, bool recurse)
#endif
    {
        return TryValidateImplAsync(target, serviceProvider, recurse);
    }

    /// <summary>
    /// Determines whether the specific object is valid.
    /// </summary>
    /// <param name="target">The object to validate.</param>
    /// <param name="serviceProvider">The service provider to use when creating ValidationContext.</param>
    /// <param name="recurse"><c>true</c> to recursively validate descendant objects; if <c>false</c> only simple values directly on <paramref name="target"/> are validated.</param>
    /// <returns><c>true</c> if <paramref name="target"/> is valid; otherwise <c>false</c> and the validation errors.</returns>
    /// <exception cref="ArgumentNullException"></exception>
#if NET6_0_OR_GREATER
    private static ValueTask<(bool IsValid, IDictionary<string, string[]> Errors)> TryValidateImplAsync<TTarget>(TTarget target, IServiceProvider? serviceProvider, bool recurse)
#else
    private static Task<(bool IsValid, IDictionary<string, string[]> Errors)> TryValidateImplAsync<TTarget>(TTarget target, IServiceProvider? serviceProvider, bool recurse)
#endif
    {
        if (target is null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        IDictionary<string, string[]>? errors;

        if (!RequiresValidation(target.GetType(), recurse))
        {
            errors = _emptyErrors;

            // Return true for types with nothing to validate
#if NET6_0_OR_GREATER
            return ValueTask.FromResult((true, errors));
#else
            return Task.FromResult((true, errors));
#endif
        }

        var validatedObjects = new Dictionary<object, bool?>();
        var workingErrors = new Dictionary<string, List<string>>();
        var validationTask = TryValidateImpl(target, serviceProvider, recurse, allowAsync: true, workingErrors, validatedObjects);

        if (validationTask.IsCompleted)
        {
            var isValid = validationTask.GetAwaiter().GetResult();
            errors = MapToFinalErrorsResult(workingErrors);

#if NET6_0_OR_GREATER
            return ValueTask.FromResult((isValid, errors));
#else
            return Task.FromResult((isValid, errors));
#endif
        }

        // Handle async completion
        return HandleTryValidateAsyncResult(validationTask, workingErrors);
    }

#if NET6_0_OR_GREATER
    private static async ValueTask<(bool IsValid, IDictionary<string, string[]> Errors)> HandleTryValidateAsyncResult(ValueTask<bool> validationTask, Dictionary<string, List<string>> workingErrors)
#else
    private static async Task<(bool IsValid, IDictionary<string, string[]> Errors)> HandleTryValidateAsyncResult(Task<bool> validationTask, Dictionary<string, List<string>> workingErrors)
#endif
    {
        var isValid = await validationTask.ConfigureAwait(false);

        var errors = MapToFinalErrorsResult(workingErrors);

        return (isValid, errors);
    }

#if NET6_0_OR_GREATER
    private static async ValueTask<bool> TryValidateImpl(
#else
    private static async Task<bool> TryValidateImpl(
#endif
        object target,
        IServiceProvider? serviceProvider,
        bool recurse,
        bool allowAsync,
        Dictionary<string, List<string>> workingErrors,
        Dictionary<object, bool?> validatedObjects,
        List<ValidationResult>? validationResults = null,
        string? prefix = null,
        int currentDepth = 0)
    {
        if (target is null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        // Once we get to this point we have to box the target in order to track whether we've validated it or not
        if (validatedObjects.ContainsKey(target))
        {
            var result = validatedObjects[target];
            // If there's a null result it means this object is the one currently being validated
            // so just skip this reference to it by returning true. If there is a result it means
            // we already validated this object as part of this validation operation.
            return !result.HasValue || result == true;
        }

        // Add current target to tracking dictionary in null (validating) state
        validatedObjects.Add(target, null);

        var targetType = target.GetType();
        var (typeProperties, _) = _typeDetailsCache.Get(targetType);

        var isValid = true;
        var propertiesToRecurse = recurse ? new Dictionary<PropertyDetails, object>() : null;
        var validationContext = new ValidationContext(target, serviceProvider: serviceProvider, items: null);

        foreach (var property in typeProperties)
        {
            var propertyValue = property.GetValue(target);
            var propertyValueType = propertyValue?.GetType();
            var (properties, _) = _typeDetailsCache.Get(propertyValueType);

            if (property.HasValidationAttributes)
            {
                validationContext.MemberName = property.Name;
                validationContext.DisplayName = GetDisplayName(property);
                validationResults ??= new();
                var propertyIsValid = Validator.TryValidateValue(propertyValue!, validationContext, validationResults, property.ValidationAttributes);

                if (!propertyIsValid)
                {
                    ProcessValidationResults(property.Name, validationResults, workingErrors, prefix);
                    isValid = false;
                }
            }

            if (recurse && propertyValue is not null &&
                (property.Recurse
                 || typeof(IValidatableObject).IsAssignableFrom(propertyValueType)
                 || typeof(IAsyncValidatableObject).IsAssignableFrom(propertyValueType)
                 || properties.Any(p => p.Recurse)))
            {
                propertiesToRecurse!.Add(property, propertyValue);
            }
        }

        if (recurse && currentDepth <= MaxDepth)
        {
            // Validate IEnumerable
            if (target is IEnumerable)
            {
                RuntimeHelpers.EnsureSufficientExecutionStack();

                var validateTask = TryValidateEnumerable(target, serviceProvider, recurse, allowAsync, workingErrors, validatedObjects, validationResults, prefix, currentDepth);

                try
                {
                    ThrowIfAsyncNotAllowed(validateTask.IsCompleted, allowAsync);
                }
                catch (Exception)
                {
                    // Always observe the ValueTask
                    _ = await validateTask.ConfigureAwait(false);
                    throw;
                }

                isValid = await validateTask.ConfigureAwait(false) && isValid;
            }

            // Validate complex properties
            if (propertiesToRecurse!.Count > 0)
            {
                foreach (var property in propertiesToRecurse)
                {
                    var propertyDetails = property.Key;
                    var propertyValue = property.Value;

                    if (propertyValue != null)
                    {
                        RuntimeHelpers.EnsureSufficientExecutionStack();

                        if (propertyDetails.IsEnumerable)
                        {
                            var thePrefix = $"{prefix}{propertyDetails.Name}";

                            var validateTask = TryValidateEnumerable(propertyValue, serviceProvider, recurse, allowAsync, workingErrors, validatedObjects, validationResults, thePrefix, currentDepth);
                            try
                            {
                                ThrowIfAsyncNotAllowed(validateTask.IsCompleted, allowAsync);
                            }
                            catch (Exception)
                            {
                                // Always observe the ValueTask
                                _ = await validateTask.ConfigureAwait(false);
                                throw;
                            }

                            isValid = await validateTask.ConfigureAwait(false) && isValid;
                        }
                        else
                        {
                            var thePrefix = $"{prefix}{propertyDetails.Name}."; // <-- Note trailing '.' here

                            var validateTask = TryValidateImpl(propertyValue, serviceProvider, recurse, allowAsync, workingErrors, validatedObjects, validationResults, thePrefix, currentDepth + 1);
                            try
                            {
                                ThrowIfAsyncNotAllowed(validateTask.IsCompleted, allowAsync);
                            }
                            catch (Exception)
                            {
                                // Always observe the ValueTask
                                _ = await validateTask.ConfigureAwait(false);
                                throw;
                            }

                            isValid = await validateTask.ConfigureAwait(false) && isValid;
                        }
                    }
                }
            }
        }

        if (isValid && typeof(IValidatableObject).IsAssignableFrom(targetType))
        {
            var validatable = (IValidatableObject)target;

            // Reset validation context
            validationContext.MemberName = null;
            validationContext.DisplayName = validationContext.ObjectType.Name;

            var validatableResults = validatable.Validate(validationContext);
            if (validatableResults is not null)
            {
                ProcessValidationResults(validatableResults, workingErrors, prefix);
                isValid = workingErrors.Count == 0 && isValid;
            }
        }

        if (isValid && typeof(IAsyncValidatableObject).IsAssignableFrom(targetType))
        {
            var validatable = (IAsyncValidatableObject)target;

            // Reset validation context
            validationContext.MemberName = null;
            validationContext.DisplayName = validationContext.ObjectType.Name;

            var validateTask = validatable.ValidateAsync(validationContext);
            ThrowIfAsyncNotAllowed(validateTask.IsCompleted, allowAsync);

            var validatableResults = await validateTask.ConfigureAwait(false);
            if (validatableResults is not null)
            {
                ProcessValidationResults(validatableResults, workingErrors, prefix);
                isValid = workingErrors.Count == 0 && isValid;
            }
        }

        // Update state of target in tracking dictionary
        validatedObjects[target] = isValid;

        return isValid;

        static string GetDisplayName(PropertyDetails property)
        {
            return property.DisplayAttribute?.GetName() ?? property.Name;
        }
    }

    private static void ThrowIfAsyncNotAllowed(bool taskCompleted, bool allowAsync)
    {
        if (!allowAsync & !taskCompleted)
        {
            throw new InvalidOperationException($"An object in the validation graph requires async validation. Call the '{nameof(TryValidateAsync)}' method instead.");
        }
    }

#if NET6_0_OR_GREATER
    private static async ValueTask<bool> TryValidateEnumerable(
#else
    private static async Task<bool> TryValidateEnumerable(
#endif
        object target,
        IServiceProvider? serviceProvider,
        bool recurse,
        bool allowAsync,
        Dictionary<string, List<string>> workingErrors,
        Dictionary<object, bool?> validatedObjects,
        List<ValidationResult>? validationResults,
        string? prefix = null,
        int currentDepth = 0)
    {
        var isValid = true;
        if (target is IEnumerable items)
        {
            // Validate each instance in the collection
            var index = 0;
            foreach (var item in items)
            {
                if (item is null)
                {
                    continue;
                }

                var itemPrefix = $"{prefix}[{index}].";

                var validateTask = TryValidateImpl(item, serviceProvider, recurse, allowAsync, workingErrors, validatedObjects, validationResults, itemPrefix, currentDepth + 1);
                try
                {
                    ThrowIfAsyncNotAllowed(validateTask.IsCompleted, allowAsync);
                }
                catch (Exception)
                {
                    // Always observe the ValueTask
                    _ = await validateTask.ConfigureAwait(false);
                    throw;
                }

                isValid = await validateTask.ConfigureAwait(false);

                if (!isValid)
                {
                    break;
                }
                index++;
            }
        }
        return isValid;
    }

    private static IDictionary<string, string[]> MapToFinalErrorsResult(Dictionary<string, List<string>> workingErrors)
    {
#if NET6_0_OR_GREATER
        var result = new AdaptiveCapacityDictionary<string, string[]>(workingErrors.Count);
#else
        var result = new Dictionary<string, string[]>(workingErrors.Count);
#endif
        foreach (var fieldError in workingErrors)
        {
            if (!result.ContainsKey(fieldError.Key))
            {
                result.Add(fieldError.Key, fieldError.Value.ToArray());
            }
            else
            {
                var existingFieldErrors = result[fieldError.Key];
                result[fieldError.Key] = existingFieldErrors.Concat(fieldError.Value).ToArray();
            }
        }

        return result;
    }

    private static void ProcessValidationResults(IEnumerable<ValidationResult> validationResults, Dictionary<string, List<string>> errors, string? prefix)
    {
        foreach (var result in validationResults)
        {
            var hasMemberNames = false;
            foreach (var memberName in result.MemberNames)
            {
                var key = $"{prefix}{memberName}";
                if (!errors.ContainsKey(key))
                {
                    errors.Add(key, new());
                }
                errors[key].Add(result.ErrorMessage ?? "");
                hasMemberNames = true;
            }

            if (!hasMemberNames)
            {
                // Class level error message
                var key = "";
                if (!errors.ContainsKey(key))
                {
                    errors.Add(key, new());
                }
                errors[key].Add(result.ErrorMessage ?? "");
            }
        }
    }

    private static void ProcessValidationResults(string propertyName, ICollection<ValidationResult> validationResults, Dictionary<string, List<string>> errors, string? prefix)
    {
        if (validationResults.Count == 0)
        {
            return;
        }

        var errorsList = new List<string>(validationResults.Count);

        foreach (var result in validationResults)
        {
            errorsList.Add(result.ErrorMessage ?? "");
        }

        errors.Add($"{prefix}{propertyName}", errorsList);
        validationResults.Clear();
    }
}
