using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace MiniValidation;

internal class TypeDetailsCache
{
    private static readonly MemberDetails[] _emptyMemberDetails = Array.Empty<MemberDetails>();
    private readonly ConcurrentDictionary<Type, (MemberDetails[] Members, bool RequiresAsync)> _cache = new();

    public TypeDetailsCache()
    {
        TypeDescriptor.Refreshed += args =>
        {
            if (args.TypeChanged is { } type)
            {
                _cache.TryRemove(type, out _);
            }
            else
            {
                _cache.Clear();
            }
        };
    }

    public (MemberDetails[] Members, bool RequiresAsync) Get(Type? type)
    {
        if (type is null)
        {
            return (_emptyMemberDetails, false);
        }

        (MemberDetails[] Members, bool RequiresAsync) details;
        while (!_cache.TryGetValue(type, out details))
        {
            Visit(type);
        }

        return details;
    }

    private void Visit(Type type)
    {
        var visited = new HashSet<Type>();
        bool requiresAsync = false;
        Visit(type, visited, ref requiresAsync);
    }

    private void Visit(Type type, HashSet<Type> visited, ref bool requiresAsync)
    {
        if (_cache.ContainsKey(type))
        {
            return;
        }

        if (!visited.Add(type))
        {
            return;
        }

        if (DoNotRecurseIntoMembersOf(type) || IsNonValidatableType(type))
        {
            _cache[type] = (_emptyMemberDetails, false);
            return;
        }

        if (typeof(IAsyncValidatableObject).IsAssignableFrom(type))
        {
            requiresAsync = true;
        }

        // Find a constructor that matches the Deconstruct method (this will be the primary constuctor for record types)
        ParameterInfo[]? primaryCtorParams = null;
        foreach (var ctor in type.GetConstructors())
        {
            if (ctor.DeclaringType != type) continue;

            // Parameters to Deconstruct are 'byref' so need to call MakeByRefType()
            var deconstructParams = ctor.GetParameters().Select(p => p.ParameterType.IsByRef ? p.ParameterType : p.ParameterType.MakeByRefType()).ToArray();
            if (type.GetMethod("Deconstruct", deconstructParams) is { } deconstruct && deconstruct.DeclaringType == type)
            {
                primaryCtorParams = ctor.GetParameters();
            }
        }

        List<MemberDetails>? membersToValidate = null;
        var hasMembersOfOwnType = false;
        var hasValidatableMembers = false;

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy))
        {
            if (property.GetIndexParameters().Length > 0)
            {
                // Ignore indexer properties
                continue;
            }

            var (validationAttributes, displayAttribute, skipRecursionAttribute) = TypeDetailsCache.GetPropertyAttributes(primaryCtorParams, property);
            VisitMember(
                property.Name,
                property.PropertyType,
                PropertyHelper.MakeNullSafeFastPropertyGetter(property),
                validationAttributes,
                displayAttribute,
                skipRecursionAttribute,
                ref requiresAsync);
        }

        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy))
        {
            var (validationAttributes, displayAttribute, skipRecursionAttribute) = TypeDetailsCache.GetFieldAttributes(field);
            VisitMember(
                field.Name,
                field.FieldType,
                PropertyHelper.MakeNullSafeFastFieldGetter(field),
                validationAttributes,
                displayAttribute,
                skipRecursionAttribute,
                ref requiresAsync);
        }

        if (hasMembersOfOwnType && membersToValidate != null)
        {
            // Remove members of same type if there's nothing to validate on them
            for (var i = membersToValidate.Count - 1; i >= 0; i--)
            {
                var member = membersToValidate[i];
                var enumerableTypeHasMembers = member.EnumerableType != null
                    && _cache.TryGetValue(member.EnumerableType, out var typeCache)
                    && typeCache.Members.Length > 0;
                var keepMember = member.Type != type || (hasValidatableMembers || enumerableTypeHasMembers);
                if (!keepMember)
                {
                    membersToValidate.RemoveAt(i);
                }
            }
        }

        _cache[type] = (membersToValidate?.ToArray() ?? _emptyMemberDetails, requiresAsync);

        void VisitMember(
            string memberName,
            Type memberType,
            Func<object, object?> memberGetter,
            ValidationAttribute[]? validationAttributes,
            DisplayAttribute? displayAttribute,
            SkipRecursionAttribute? skipRecursionAttribute,
            ref bool requiresAsync)
        {
            validationAttributes ??= Array.Empty<ValidationAttribute>();
            var hasValidationOnMember = validationAttributes.Length > 0;
            var hasSkipRecursionOnMember = skipRecursionAttribute is not null;
            var memberTypeIsNonValidatable = IsNonValidatableType(memberType);
            var enumerableType = GetEnumerableType(memberType);
            if (enumerableType != null)
            {
                Visit(enumerableType, visited, ref requiresAsync);
            }

            // Defer fully checking members that are of the same type we're currently building the cache for.
            // We'll remove them at the end if any other validatable members are present.
            if (type == memberType && !hasSkipRecursionOnMember)
            {
                membersToValidate ??= new List<MemberDetails>();
                membersToValidate.Add(new(memberName, displayAttribute, memberType, memberGetter, validationAttributes, true, enumerableType));
                hasMembersOfOwnType = true;
                return;
            }

            Visit(memberType, visited, ref requiresAsync);
            var memberTypeHasMembers = _cache.TryGetValue(memberType, out var typeCache) && typeCache.Members.Length > 0;
            var memberTypeIsValidatableObject = typeof(IValidatableObject).IsAssignableFrom(memberType)
                                                || typeof(IAsyncValidatableObject).IsAssignableFrom(memberType);
            var memberTypeSupportsPolymorphism = !memberTypeIsNonValidatable && !memberType.IsSealed;
            var enumerableTypeHasMembers = enumerableType != null
                && _cache.TryGetValue(enumerableType, out var enumMembers)
                && enumMembers.Members.Length > 0;
            var recurse = !memberTypeIsNonValidatable
                && (enumerableTypeHasMembers || memberTypeHasMembers
                || memberTypeIsValidatableObject
                || memberTypeSupportsPolymorphism)
                && !hasSkipRecursionOnMember;

            if (recurse || hasValidationOnMember)
            {
                membersToValidate ??= new List<MemberDetails>();
                membersToValidate.Add(new(memberName, displayAttribute, memberType, memberGetter, validationAttributes, recurse, enumerableTypeHasMembers ? enumerableType : null));
                hasValidatableMembers = true;
            }
        }
    }

    private static bool DoNotRecurseIntoMembersOf(Type type) =>
        type == typeof(object)
        || type.IsPrimitive
        || type.IsArray
        || type.IsPointer
        || type.IsEnum
        || type == typeof(string)
        || type == typeof(decimal)
        || type == typeof(DateTime)
        || type == typeof(DateTimeOffset)
#if NET6_0_OR_GREATER
        || type == typeof(DateOnly)
        || type == typeof(TimeOnly)
#endif
        ;

    internal static bool IsNonValidatableType(Type type) =>
        typeof(Delegate).IsAssignableFrom(type)
        || typeof(MemberInfo).IsAssignableFrom(type)
        || typeof(ParameterInfo).IsAssignableFrom(type)
        || typeof(Module).IsAssignableFrom(type)
        || typeof(Assembly).IsAssignableFrom(type)
        || IsKnownNonValidatableFrameworkType(type);

    private static bool IsKnownNonValidatableFrameworkType(Type type)
    {
        var @namespace = type.Namespace;
        return @namespace is not null
            && (@namespace == "System.Text.Json"
                || @namespace.StartsWith("System.Text.Json.", StringComparison.Ordinal)
                || @namespace == "Newtonsoft.Json.Linq"
                || @namespace.StartsWith("Newtonsoft.Json.Linq.", StringComparison.Ordinal)
                || @namespace == "Microsoft.AspNetCore.JsonPatch"
                || @namespace.StartsWith("Microsoft.AspNetCore.JsonPatch.", StringComparison.Ordinal)
                || @namespace == "Microsoft.AspNetCore.OData.Deltas"
                || @namespace.StartsWith("Microsoft.AspNetCore.OData.Deltas.", StringComparison.Ordinal));
    }

    private static (ValidationAttribute[]?, DisplayAttribute?, SkipRecursionAttribute?) GetPropertyAttributes(ParameterInfo[]? primaryCtorParameters, PropertyInfo property)
    {
        IEnumerable<Attribute>? paramAttributes = null;
        if (primaryCtorParameters is { } ctorParams)
        {
            foreach (var parameter in ctorParams)
            {
                if (string.Equals(parameter.Name, property.Name, StringComparison.Ordinal)
                    && parameter.ParameterType == property.PropertyType)
                {
                    // Matching parameter found
                    paramAttributes = parameter.GetCustomAttributes();
                    break;
                }
            }
        }

        var propertyAttributes = property.GetCustomAttributes().ToArray();
        var customAttributes = paramAttributes is not null
            ? paramAttributes.Concat(propertyAttributes)
            : propertyAttributes.AsEnumerable();

        if (TryGetAttributesViaTypeDescriptor(property, out var typeDescriptorAttributes))
        {
            customAttributes = customAttributes
                .Concat(typeDescriptorAttributes
                    .Cast<Attribute>()
                    .Where(attr => !IsDuplicateTypeDescriptorAttribute(attr, propertyAttributes)));
        }

        return GetValidationMetadata(customAttributes);
    }

    private static (ValidationAttribute[]?, DisplayAttribute?, SkipRecursionAttribute?) GetFieldAttributes(FieldInfo field)
    {
        return GetValidationMetadata(field.GetCustomAttributes().Cast<Attribute>());
    }

    private static (ValidationAttribute[]?, DisplayAttribute?, SkipRecursionAttribute?) GetValidationMetadata(IEnumerable<Attribute> customAttributes)
    {
        List<ValidationAttribute>? validationAttributes = null;
        DisplayAttribute? displayAttribute = null;
        SkipRecursionAttribute? skipRecursionAttribute = null;

        foreach (var attr in customAttributes)
        {
            if (attr is ValidationAttribute validationAttr)
            {
                validationAttributes ??= new();
                validationAttributes.Add(validationAttr);
            }
            else if (attr is DisplayAttribute displayAttr)
            {
                displayAttribute = displayAttr;
            }
            else if (attr is SkipRecursionAttribute skipRecursionAttr)
            {
                skipRecursionAttribute = skipRecursionAttr;
            }
        }

        return new(validationAttributes?.ToArray(), displayAttribute, skipRecursionAttribute);
    }

    private static bool IsDuplicateTypeDescriptorAttribute(Attribute typeDescriptorAttribute, Attribute[] propertyAttributes)
    {
        foreach (var propertyAttribute in propertyAttributes)
        {
            if (AreEquivalentAttributes(propertyAttribute, typeDescriptorAttribute))
            {
                return true;
            }
        }

        return false;
    }

    private static bool AreEquivalentAttributes(Attribute left, Attribute right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is not ValidationAttribute || right is not ValidationAttribute)
        {
            return false;
        }

        var attributeType = left.GetType();
        if (attributeType != right.GetType() || !Equals(left.TypeId, right.TypeId))
        {
            return false;
        }

        foreach (var property in attributeType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            if (!TryGetPropertyValue(property, left, out var leftValue)
                || !TryGetPropertyValue(property, right, out var rightValue)
                || !AreAttributeValuesEqual(leftValue, rightValue))
            {
                return false;
            }
        }

        if (attributeType.Assembly != typeof(ValidationAttribute).Assembly)
        {
            for (var currentType = attributeType; currentType is not null && currentType != typeof(object); currentType = currentType.BaseType)
            {
                foreach (var field in currentType.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (!AreAttributeValuesEqual(field.GetValue(left), field.GetValue(right)))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private static bool TryGetPropertyValue(PropertyInfo property, Attribute attribute, out object? value)
    {
        try
        {
            value = property.GetValue(attribute);
            return true;
        }
        catch
        {
            value = null;
            return false;
        }
    }

    private static bool AreAttributeValuesEqual(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        if (left is Array leftArray && right is Array rightArray)
        {
            if (leftArray.Length != rightArray.Length)
            {
                return false;
            }

            for (var i = 0; i < leftArray.Length; i++)
            {
                if (!Equals(leftArray.GetValue(i), rightArray.GetValue(i)))
                {
                    return false;
                }
            }

            return true;
        }

        return Equals(left, right);
    }

    private static bool TryGetAttributesViaTypeDescriptor(PropertyInfo property, [NotNullWhen(true)] out IEnumerable<Attribute>? typeDescriptorAttributes)
    {
        var attributes = TypeDescriptor.GetProperties(property.ReflectedType!)
            .Cast<PropertyDescriptor>()
            .FirstOrDefault(x => x.Name == property.Name)
            ?.Attributes;

        if (attributes is { Count: > 0 } tdps)
        {
            typeDescriptorAttributes = tdps.Cast<Attribute>();
            return true;
        }

        typeDescriptorAttributes = null;
        return false;
    }

    private static Type? GetEnumerableType(Type type)
    {
        if (type.IsInterface && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            return type.GetGenericArguments()[0];
        }

        foreach (var intType in type.GetInterfaces())
        {
            if (intType.IsGenericType && intType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                return intType.GetGenericArguments()[0];
            }
        }

        return null;
    }
}

internal record MemberDetails(string Name, DisplayAttribute? DisplayAttribute, Type Type, Func<object, object?> MemberGetter, ValidationAttribute[] ValidationAttributes, bool Recurse, Type? EnumerableType)
{
    public object? GetValue(object target) => MemberGetter(target);

    public bool IsEnumerable => EnumerableType != null;

    public bool HasValidationAttributes => ValidationAttributes.Length > 0;
}
