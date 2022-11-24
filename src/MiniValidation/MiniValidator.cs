using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Resources;
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
    /// Objects of types with nothing to validate will always return <c>true</c> when passed to <see cref="TryValidate(object, out IDictionary{string, string[]})"/>.
    /// </remarks>
    /// <param name="targetType">The <see cref="Type"/>.</param>
    /// <param name="recurse"><c>true</c> to recursively check descendant types; if <c>false</c> only simple values directly on the target type are checked.</param>
    /// <returns><c>true</c> if the <see cref="Type"/> has anything to validate, <c>false</c> if not.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static bool RequiresValidation(Type targetType, bool recurse = true)
    {
        if (targetType == null)
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
    /// <returns><c>true</c> if the target object validates; otherwise <c>false</c>.</returns>
    public static bool TryValidate(object target, out IDictionary<string, string[]> errors)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        return TryValidate(target, recurse: true, out errors);
    }

    /// <summary>
    /// Determines whether the specific object is valid.
    /// </summary>
    /// <param name="target">The object to validate.</param>
    /// <param name="recurse"><c>true</c> to recursively validate descendant objects; if <c>false</c> only simple values directly on the target object are validated.</param>
    /// <param name="errors">A dictionary that contains details of each failed validation.</param>
    /// <returns><c>true</c> if the target object validates; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <c>target</c> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Throw when <c>target</c> requires async validation.</exception>
    public static bool TryValidate(object target, bool recurse, out IDictionary<string, string[]> errors)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        if (!RequiresValidation(target.GetType(), recurse))
        {
            errors = _emptyErrors;

            // Return true for types with nothing to validate
            return true;
        }

        if (_typeDetailsCache.Get(target.GetType()).RequiresAsync)
        {
            throw new ArgumentException($"The target type {target.GetType().Name} requires async validation. Call the '{nameof(TryValidateAsync)}' method instead.", nameof(target));
        }

        var validatedObjects = new Dictionary<object, bool?>();
        var workingErrors = new Dictionary<string, List<string>>();
        var validateTask = TryValidateImpl(target, recurse, workingErrors, validatedObjects);

        if (!validateTask.IsCompleted)
        {
            throw new InvalidOperationException("Validation task was incomplete in synchronous path. This should never happen.");
        }

        var isValid = validateTask.GetAwaiter().GetResult();

        errors = MapToFinalErrorsResult(workingErrors);

        return isValid;
    }

#if NET6_0_OR_GREATER
    public static async ValueTask<(bool IsValid, IDictionary<string, string[]> Errors)> TryValidateAsync(object target, bool recurse)
#else
    public static async Task<(bool IsValid, IDictionary<string, string[]> Errors)> TryValidateAsync(object target, bool recurse)
#endif
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        IDictionary<string, string[]>? errors;

        if (!RequiresValidation(target.GetType(), recurse))
        {
            errors = _emptyErrors;

            // Return true for types with nothing to validate
            return (true, errors);
        }

        var validatedObjects = new Dictionary<object, bool?>();
        var workingErrors = new Dictionary<string, List<string>>();
        var isValid = await TryValidateImpl(target, recurse, workingErrors, validatedObjects);

        errors = MapToFinalErrorsResult(workingErrors);

        return (isValid, errors);
    }

#if NET6_0_OR_GREATER
    private static async ValueTask<bool> TryValidateImpl(
#else
    private static async Task<bool> TryValidateImpl(
#endif
        object target,
        bool recurse,
        Dictionary<string, List<string>> workingErrors,
        Dictionary<object, bool?> validatedObjects,
        List<ValidationResult>? validationResults = null,
        string? prefix = null,
        int currentDepth = 0)
    {
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
        var (typeProperties, requiresAsync) = _typeDetailsCache.Get(targetType);

        var isValid = true;
        var propertiesToRecurse = recurse ? new Dictionary<PropertyDetails, object>() : null;
        ValidationContext propsValidationContext = new(target);

        foreach (var property in typeProperties)
        {
            var propertyValue = property.GetValue(target);
            var propertyType = propertyValue?.GetType();
            var propertyTypeCache = _typeDetailsCache.Get(propertyType);

            if (property.HasValidationAttributes)
            {
                propsValidationContext.MemberName = property.Name;
                propsValidationContext.DisplayName = GetDisplayName(property);
                validationResults ??= new();
                var propertyIsValid = Validator.TryValidateValue(propertyValue!, propsValidationContext, validationResults, property.ValidationAttributes);
                if (!propertyIsValid)
                {
                    ProcessValidationResults(property.Name, validationResults, workingErrors, prefix);
                    isValid = false;
                }
            }
            if (recurse && propertyValue is not null &&
                (property.Recurse || typeof(IValidatableObject).IsAssignableFrom(propertyType) || propertyTypeCache.Properties.Any(p => p.Recurse)))
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
                isValid = await TryValidateEnumerable(target, recurse, workingErrors, validatedObjects, validationResults, prefix, currentDepth) && isValid;
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
                            isValid = await TryValidateEnumerable(propertyValue, recurse, workingErrors, validatedObjects, validationResults, thePrefix, currentDepth) && isValid;
                        }
                        else
                        {
                            var thePrefix = $"{prefix}{propertyDetails.Name}."; // <-- Note trailing '.' here
                            isValid = await TryValidateImpl(propertyValue, recurse, workingErrors, validatedObjects, validationResults, thePrefix, currentDepth + 1) && isValid;
                        }
                    }
                }
            }
        }

        if (isValid && typeof(IValidatableObject).IsAssignableFrom(targetType))
        {
            var validatable = (IValidatableObject)target;
            ValidationContext validatableValidationContext = new(target);
            var validatableResults = validatable.Validate(validatableValidationContext);
            if (validatableResults is not null)
            {
                ProcessValidationResults(validatableResults, workingErrors, prefix);
                isValid = workingErrors.Count == 0 && isValid;
            }
        }

        if (isValid && typeof(IAsyncValidatableObject).IsAssignableFrom(targetType))
        {
            var validatable = (IAsyncValidatableObject)target;
            ValidationContext validatableValidationContext = new(target);
            var validatableResults = await validatable.ValidateAsync(validatableValidationContext);
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

#if NET6_0_OR_GREATER
    private static async ValueTask<bool> TryValidateEnumerable(
#else
    private static async Task<bool> TryValidateEnumerable(
#endif
        object target,
        bool recurse,
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

                isValid = await TryValidateImpl(item, recurse, workingErrors, validatedObjects, validationResults, itemPrefix, currentDepth + 1);

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
        var result = new Dictionary<string, string[]>(workingErrors.Count);

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
