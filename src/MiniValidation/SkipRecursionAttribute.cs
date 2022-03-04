using System;

namespace MiniValidation
{
    /// <summary>
    /// Indicates that a property should be ignored during recursive validation when using <see cref="MiniValidator.TryValidate(object, bool, out System.Collections.Generic.IDictionary{string, string[]})"/>.
    /// Note that any validation attributes on the property itself will still be validated.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class SkipRecursionAttribute : Attribute
    {

    }
}
