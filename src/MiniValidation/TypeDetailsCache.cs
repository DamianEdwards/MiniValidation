using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace MiniValidation
{
    internal class TypeDetailsCache
    {
        private static readonly PropertyDetails[] _emptyPropertyDetails = Array.Empty<PropertyDetails>();
        private readonly ConcurrentDictionary<Type, PropertyDetails[]> _cache = new();

        public PropertyDetails[] Get(Type type)
        {
            if (!_cache.ContainsKey(type))
            {
                Visit(type);
            }

            return _cache[type];
        }

        private void Visit(Type type)
        {
            var visited = new HashSet<Type>();
            Visit(type, visited);
        }

        private void Visit(Type type, HashSet<Type> visited)
        {
            if (_cache.ContainsKey(type))
            {
                return;
            }

            if (!visited.Add(type))
            {
                return;
            }

            List<PropertyDetails>? propertiesToValidate = null;
            var hasPropertiesOfOwnType = false;
            var hasValidatableProperties = false;

            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.GetIndexParameters().Length > 0)
                {
                    // Ignore indexer properties
                    continue;
                }

                var validationAttributes = property.GetCustomAttributes().OfType<ValidationAttribute>();
                var hasValidationOnProperty = validationAttributes.Any();
                var hasSkipRecursionOnProperty = property.GetCustomAttributes().OfType<SkipRecursionAttribute>().Any();
                var enumerableType = GetEnumerableType(property.PropertyType);
                if (enumerableType != null)
                {
                    Visit(enumerableType, visited);
                }

                // Defer fully checking properties that are of the same type we're currently building the cache for.
                // We'll remove them at the end if any other validatable properties are present.
                if (type == property.PropertyType && !hasSkipRecursionOnProperty)
                {
                    propertiesToValidate ??= new List<PropertyDetails>();
                    propertiesToValidate.Add(new (property.Name, property.PropertyType, PropertyHelper.MakeNullSafeFastPropertyGetter(property), validationAttributes.ToArray(), true, enumerableType));
                    hasPropertiesOfOwnType = true;
                    continue;
                }

                Visit(property.PropertyType, visited);
                var propertyTypeHasProperties = _cache.TryGetValue(property.PropertyType, out var properties) && properties.Length > 0;
                var enumerableTypeHasProperties = enumerableType != null
                    && _cache.TryGetValue(enumerableType, out var enumProperties)
                    && enumProperties.Length > 0;
                var recurse = (enumerableTypeHasProperties || propertyTypeHasProperties) && !hasSkipRecursionOnProperty;

                if (recurse || hasValidationOnProperty)
                {
                    propertiesToValidate ??= new List<PropertyDetails>();
                    propertiesToValidate.Add(new(property.Name, property.PropertyType, PropertyHelper.MakeNullSafeFastPropertyGetter(property), validationAttributes.ToArray(), recurse, enumerableTypeHasProperties ? enumerableType : null));
                    hasValidatableProperties = true;
                }
            }

            if (hasPropertiesOfOwnType && propertiesToValidate != null)
            {
                // Remove properties of same type if there's nothing to validate on them
                for (int i = propertiesToValidate.Count - 1; i >= 0; i--)
                {
                    var property = propertiesToValidate[i];
                    var enumerableTypeHasProperties = property.EnumerableType != null
                        && _cache.TryGetValue(property.EnumerableType, out var enumProperties)
                        && enumProperties.Length > 0;
                    var keepProperty = property.Type != type || (hasValidatableProperties || enumerableTypeHasProperties);
                    if (!keepProperty)
                    {
                        propertiesToValidate.RemoveAt(i);
                    }
                }
            }

            _cache[type] = propertiesToValidate?.ToArray() ?? _emptyPropertyDetails;
        }

        private static Type? GetEnumerableType(Type type)
        {
            if (type.IsInterface && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                return type.GetGenericArguments()[0];
            }

            foreach (Type intType in type.GetInterfaces())
            {
                if (intType.IsGenericType
                    && intType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    return intType.GetGenericArguments()[0];
                }
            }

            return null;
        }
    }

    internal record PropertyDetails(string Name, Type Type, Func<object, object?> PropertyGetter, ValidationAttribute[] ValidationAttributes, bool Recurse, Type? EnumerableType)
    {
        public object? GetValue(object target) => PropertyGetter(target);
        public bool IsEnumerable => EnumerableType != null;
        public bool HasValidationAttributes => ValidationAttributes.Length > 0;
    }
}
