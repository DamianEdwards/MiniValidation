using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Resources;
using System.Runtime.CompilerServices;

namespace MiniValidation
{
    /// <summary>
    /// Contains methods and properties for performing validation operations with <see cref="Validator"/> on objects whos properties
    /// are decorated with <see cref="ValidationAttribute"/>s.
    /// </summary>
    public static class MiniValidator
    {
        private static readonly TypeDetailsCache _typeDetailsCache = new();

        /// <summary>
        /// Gets or sets the maximum depth allowed when validating an object with recursion enabled.
        /// Defaults to 32.
        /// </summary>
        public static int MaxDepth { get; set; } = 32;

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
        /// Determines whether the specific object is valid using a value indicating whether to recursively validate descendant objects.
        /// </summary>
        /// <param name="target">The object to validate.</param>
        /// <param name="recurse"><c>true</c> to recursively validate descendant objects; if <c>false</c> only simple values directly on the target object are validated.</param>
        /// <param name="errors">A dictionary that contains details of each failed validation.</param>
        /// <returns><c>true</c> if the target object validates; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool TryValidate(object target, bool recurse, out IDictionary<string, string[]> errors)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var validatedObjects = new Dictionary<object, bool?>();
            var workingErrors = new Dictionary<string, List<string>>();
            var isValid = TryValidateImpl(target, recurse, workingErrors, validatedObjects);

            errors = MapToFinalErrorsResult(workingErrors);

            return isValid;
        }

        private static bool TryValidateImpl(
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
            var typeProperties = _typeDetailsCache.Get(targetType);

            var isValid = true;
            var propertiesToRecurse = recurse ? new Dictionary<PropertyDetails, object>() : null;
            ValidationContext propsValidationContext = new(target);

            foreach (var property in typeProperties)
            {
                var propertyValue = property.GetValue(target);
                var propertyType = propertyValue?.GetType();
                var propertyTypeProperties = _typeDetailsCache.Get(propertyType);

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
                        (property.Recurse || typeof(IValidatableObject).IsAssignableFrom(propertyType) || propertyTypeProperties.Any(p => p.Recurse)))
                {
                    propertiesToRecurse!.Add(property, propertyValue);
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
                    isValid = workingErrors.Count == 0;
                }
            }

            if (isValid && recurse && currentDepth <= MaxDepth)
            {
                // Validate IEnumerable
                if (target is IEnumerable)
                {
                    RuntimeHelpers.EnsureSufficientExecutionStack();
                    isValid = TryValidateEnumerable(target, recurse, workingErrors, validatedObjects, validationResults, prefix, currentDepth);
                }

                // Validate complex properties
                if (isValid && propertiesToRecurse!.Count > 0)
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
                                isValid = TryValidateEnumerable(propertyValue, recurse, workingErrors, validatedObjects, validationResults, thePrefix, currentDepth);
                            }
                            else
                            {
                                var thePrefix = $"{prefix}{propertyDetails.Name}."; // <-- Note trailing '.' here
                                isValid = TryValidateImpl(propertyValue, recurse, workingErrors, validatedObjects, validationResults, thePrefix, currentDepth + 1);
                            }
                        }

                        if (!isValid)
                        {
                            break;
                        }
                    }
                }
            }

            // Update state of target in tracking dictionary
            validatedObjects[target] = isValid;  

            return isValid;

            static string GetDisplayName(PropertyDetails property)
            {
                string? displayName = null;

                if (property.DisplayAttribute?.ResourceType == null)
                {
                    displayName = property.DisplayAttribute?.Name;
                }
                else
                {
                    var resourceManager = new ResourceManager(property.DisplayAttribute.ResourceType);
                    displayName = resourceManager.GetString(property.DisplayAttribute.Name!) ?? property.DisplayAttribute.Name;
                }

                return displayName ?? property.Name;
            }
        }

        private static bool TryValidateEnumerable(
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

                    isValid = TryValidateImpl(item, recurse, workingErrors, validatedObjects, validationResults, prefix: itemPrefix, currentDepth + 1);

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
}
