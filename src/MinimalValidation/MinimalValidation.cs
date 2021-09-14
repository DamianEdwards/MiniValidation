using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MinimalValidationLib;

namespace System.ComponentModel.DataAnnotations
{
    /// <summary>
    /// Contains methods and properties for performing validation operations with <see cref="Validator"/> on objects whos properties
    /// are decorated with <see cref="ValidationAttribute"/>s.
    /// </summary>
    public static class MinimalValidation
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
            errors = new Dictionary<string, string[]>();
            var isValid = TryValidateImpl(target, recurse, errors, validatedObjects);

            return isValid;
        }

        private static bool TryValidateImpl(
            object target,
            bool recurse,
            IDictionary<string, string[]> errors,
            Dictionary<object, bool?> validatedObjects,
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

            var propertiesToRecurse = recurse ? new List<PropertyDetails>() : null;

            foreach (var property in typeProperties)
            {
                if (property.HasValidationAttributes)
                {
                    var validationContext = new ValidationContext(target) { MemberName = property.Name };
                    var validationResults = new List<ValidationResult>();
                    var propertyValue = property.GetValue(target);
                    var propertyIsValid = Validator.TryValidateValue(propertyValue, validationContext, validationResults, property.ValidationAttributes);
                    if (!propertyIsValid)
                    {
                        ProcessValidationResults(property.Name, validationResults, errors, prefix);
                        isValid = false;
                    }
                }
                if (recurse && property.Recurse)
                {
                    propertiesToRecurse!.Add(property);
                }
            }

            if (isValid && recurse && currentDepth <= MaxDepth)
            {
                // Validate IEnumerable
                if (target is IEnumerable)
                {
                    RuntimeHelpers.EnsureSufficientExecutionStack();
                    isValid = TryValidateEnumerable(target, recurse, errors, validatedObjects, prefix, currentDepth);
                }

                // Validate complex properties
                if (isValid && propertiesToRecurse!.Count > 0)
                {
                    foreach (var property in propertiesToRecurse)
                    {
                        var propertyValue = property.GetValue(target);

                        if (propertyValue != null)
                        {
                            RuntimeHelpers.EnsureSufficientExecutionStack();

                            if (property.IsEnumerable)
                            {
                                var thePrefix = $"{prefix}{property.Name}";
                                isValid = TryValidateEnumerable(propertyValue, recurse, errors, validatedObjects, thePrefix, currentDepth);
                            }
                            else
                            {
                                var thePrefix = $"{prefix}{property.Name}.";
                                isValid = TryValidateImpl(propertyValue, recurse, errors, validatedObjects, thePrefix, currentDepth + 1);
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
        }

        private static bool TryValidateEnumerable(
            object target,
            bool recurse,
            IDictionary<string, string[]> errors,
            Dictionary<object, bool?> validatedObjects,
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
                    if (item is null) continue;

                    var itemPrefix = $"{prefix}[{index}].";

                    isValid = TryValidateImpl(item, recurse, errors, validatedObjects, prefix: itemPrefix, currentDepth + 1);

                    if (!isValid)
                    {
                        break;
                    }
                    index++;
                }
            }
            return isValid;
        }

        private static void ProcessValidationResults(string propertyName, ICollection<ValidationResult> validationResults, IDictionary<string, string[]> errors, string? prefix)
        {
            if (validationResults.Count == 0)
            {
                return;
            }

            var errorsList = new string[validationResults.Count];
            var i = 0;

            foreach (var result in validationResults)
            {
                errorsList[i] = result.ErrorMessage ?? "";
                i++;
            }

            errors.Add($"{prefix}{propertyName}", errorsList);
        }
    }
}
