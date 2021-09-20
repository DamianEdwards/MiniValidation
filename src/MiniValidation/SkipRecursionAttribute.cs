namespace System.ComponentModel.DataAnnotations
{
    /// <summary>
    /// Indicates that a property should be ignored during recursive validation when using <see cref="MiniValidation.TryValidate"/>.
    /// Note that any validation attributes on the property itself will still be validated.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class SkipRecursionAttribute : Attribute
    {

    }
}
