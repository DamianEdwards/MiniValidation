﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace System.ComponentModel.DataAnnotations
{
    public static class MinimalValidation
    {
        public static bool TryValidate<T>(T target, out IDictionary<string, string[]> errors) where T : class
        {
            return TryValidate(target, recurse: true, out errors);
        }

        public static bool TryValidate<T>(T target, bool recurse, out IDictionary<string, string[]> errors) where T : class
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

        private static int _maxDepth = 3; // Who'd ever need more than 3
        private static ConcurrentDictionary<Type, PropertyInfo[]> _typeCache = new();

        private static bool TryValidateImpl(object target, bool recurse, IDictionary<string, string[]> errors, Dictionary<object, bool?> validatedObjects, string prefix = "", int currentDepth = 0)
        {
            if (validatedObjects.ContainsKey(target))
            {
                var result = validatedObjects[target];
                return !result.HasValue || result == true;
            }

            validatedObjects.Add(target, null);

            var validationContext = new ValidationContext(target);
            var validationResults = new List<ValidationResult>();

            // Validate the simple properties on the target first (Validator.TryValidateObject is non-recursive)
            var isValid = Validator.TryValidateObject(target, validationContext, validationResults, validateAllProperties: true);

            var errorsList = new Dictionary<string, List<string>>();
            foreach (var result in validationResults)
            {
                foreach (var name in result.MemberNames)
                {
                    List<string> fieldErrors;
                    if (errorsList.ContainsKey(name))
                    {
                        fieldErrors = errorsList[name];
                    }
                    else
                    {
                        fieldErrors = new List<string>();
                        errorsList.Add(name, fieldErrors);
                    }
                    if (!string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        fieldErrors.Add(result.ErrorMessage);
                    }
                }
            }

            foreach (var error in errorsList)
            {
                errors.Add($"{prefix}{error.Key}", error.Value.ToArray());
            }

            if (recurse && isValid && currentDepth < _maxDepth)
            {
                // Validate complex properties
                var complexProperties = _typeCache.GetOrAdd(target.GetType(), t =>
                        t.GetProperties().Where(p => IsComplexType(p.PropertyType)).ToArray());

                foreach (var property in complexProperties)
                {
                    var propertyName = property.Name;
                    var propertyType = property.PropertyType;

                    if (property.GetIndexParameters().Length == 0)
                    {
                        var propertyValue = property.GetValue(target);
                        if (propertyValue != null)
                        {
                            isValid = TryValidateImpl(propertyValue, recurse, errors, validatedObjects, prefix: $"{propertyName}.", currentDepth + 1);
                        }

                        if (!isValid)
                        {
                            break;
                        }
                    }

                    if (typeof(IEnumerable).IsAssignableFrom(propertyType))
                    {
                        // Validate each instance in the collection
                        if (property.GetValue(target) is IEnumerable items)
                        {
                            var index = 0;
                            foreach (var item in items)
                            {
                                if (item is not object) continue;

                                var itemPrefix = $"{propertyName}[{index}].";
                                isValid = TryValidateImpl(item, recurse, errors, validatedObjects, prefix: itemPrefix, currentDepth + 1);

                                if (!isValid)
                                {
                                    break;
                                }
                                index++;
                            }
                        }
                    }
                }
            }

            validatedObjects[target] = isValid;

            return isValid;
        }

        private static bool IsComplexType(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                // Nullable type, check if the nested type is complex
                return IsComplexType(type.GetGenericArguments()[0]);
            }

            return !(type.IsPrimitive
                || type.IsEnum
                || type.Equals(typeof(string))
                || type.Equals(typeof(decimal)));
        }
    }
}
