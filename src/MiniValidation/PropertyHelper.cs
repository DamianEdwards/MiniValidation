// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Code in this file is taken from https://github.dev/dotnet/aspnetcore

using System;
using System.Diagnostics;
using System.Reflection;

namespace MiniValidation
{
    internal static class PropertyHelper
    {
        private const BindingFlags DeclaredOnlyLookup = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        // Delegate type for a by-ref property getter
        private delegate TValue ByRefFunc<TDeclaringType, TValue>(ref TDeclaringType arg);

        private static readonly MethodInfo CallPropertyGetterOpenGenericMethod =
            typeof(PropertyHelper).GetMethod(nameof(CallPropertyGetter), DeclaredOnlyLookup)!;

        private static readonly MethodInfo CallPropertyGetterByReferenceOpenGenericMethod =
            typeof(PropertyHelper).GetMethod(nameof(CallPropertyGetterByReference), DeclaredOnlyLookup)!;

        private static readonly MethodInfo CallNullSafePropertyGetterOpenGenericMethod =
            typeof(PropertyHelper).GetMethod(nameof(CallNullSafePropertyGetter), DeclaredOnlyLookup)!;

        private static readonly MethodInfo CallNullSafePropertyGetterByReferenceOpenGenericMethod =
            typeof(PropertyHelper).GetMethod(nameof(CallNullSafePropertyGetterByReference), DeclaredOnlyLookup)!;

        public static Func<object, object?> MakeNullSafeFastPropertyGetter(PropertyInfo propertyInfo)
        {
            Debug.Assert(propertyInfo != null);

            return MakeFastPropertyGetter(
                propertyInfo!,
                CallNullSafePropertyGetterOpenGenericMethod,
                CallNullSafePropertyGetterByReferenceOpenGenericMethod);
        }

        private static Func<object, object?> MakeFastPropertyGetter(
            PropertyInfo propertyInfo,
            MethodInfo propertyGetterWrapperMethod,
            MethodInfo propertyGetterByRefWrapperMethod)
        {
            Debug.Assert(propertyInfo != null);

            // Must be a generic method with a Func<,> parameter
            Debug.Assert(propertyGetterWrapperMethod != null);
            Debug.Assert(propertyGetterWrapperMethod!.IsGenericMethodDefinition);
            Debug.Assert(propertyGetterWrapperMethod.GetParameters().Length == 2);

            // Must be a generic method with a ByRefFunc<,> parameter
            Debug.Assert(propertyGetterByRefWrapperMethod != null);
            Debug.Assert(propertyGetterByRefWrapperMethod!.IsGenericMethodDefinition);
            Debug.Assert(propertyGetterByRefWrapperMethod.GetParameters().Length == 2);

            var getMethod = propertyInfo!.GetMethod;
            Debug.Assert(getMethod != null);
            Debug.Assert(!getMethod!.IsStatic);
            Debug.Assert(getMethod.GetParameters().Length == 0);

            // Instance methods in the CLR can be turned into static methods where the first parameter
            // is open over "target". This parameter is always passed by reference, so we have a code
            // path for value types and a code path for reference types.
            if (getMethod.DeclaringType!.IsValueType)
            {
                // Create a delegate (ref TDeclaringType) -> TValue
                return MakeFastPropertyGetter(
                    typeof(ByRefFunc<,>),
                    getMethod,
                    propertyGetterByRefWrapperMethod);
            }
            else
            {
                // Create a delegate TDeclaringType -> TValue
                return MakeFastPropertyGetter(
                    typeof(Func<,>),
                    getMethod,
                    propertyGetterWrapperMethod);
            }
        }

        private static Func<object, object?> MakeFastPropertyGetter(
            Type openGenericDelegateType,
            MethodInfo propertyGetMethod,
            MethodInfo openGenericWrapperMethod)
        {
            var typeInput = propertyGetMethod.DeclaringType!;
            var typeOutput = propertyGetMethod.ReturnType;

            var delegateType = openGenericDelegateType.MakeGenericType(typeInput, typeOutput);
            var propertyGetterDelegate = propertyGetMethod.CreateDelegate(delegateType);

            var wrapperDelegateMethod = openGenericWrapperMethod.MakeGenericMethod(typeInput, typeOutput);
            var accessorDelegate = wrapperDelegateMethod.CreateDelegate(
                typeof(Func<object, object?>),
                propertyGetterDelegate);

            return (Func<object, object?>)accessorDelegate;
        }

        // Called via reflection
        private static object? CallPropertyGetter<TDeclaringType, TValue>(
            Func<TDeclaringType, TValue> getter,
            object target)
        {
            return getter((TDeclaringType)target);
        }

        // Called via reflection
        private static object? CallPropertyGetterByReference<TDeclaringType, TValue>(
            ByRefFunc<TDeclaringType, TValue> getter,
            object target)
        {
            var unboxed = (TDeclaringType)target;
            return getter(ref unboxed);
        }

        // Called via reflection
        private static object? CallNullSafePropertyGetter<TDeclaringType, TValue>(
            Func<TDeclaringType, TValue> getter,
            object target)
        {
            if (target == null)
            {
                return null;
            }

            return getter((TDeclaringType)target);
        }

        // Called via reflection
        private static object? CallNullSafePropertyGetterByReference<TDeclaringType, TValue>(
            ByRefFunc<TDeclaringType, TValue> getter,
            object target)
        {
            if (target == null)
            {
                return null;
            }

            var unboxed = (TDeclaringType)target;
            return getter(ref unboxed);
        }
    }
}
