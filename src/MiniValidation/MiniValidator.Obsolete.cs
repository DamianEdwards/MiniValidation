using System;

namespace MiniValidation;

public static partial class MiniValidator
{
    /// <summary>
    /// Gets or sets the maximum depth allowed when validating an object with recursion enabled.
    /// Defaults to 32.
    /// </summary>
    [Obsolete("Use MiniValidation.ValidateOptions instead.")]
    public static int MaxDepth { get; set; } = 32;
}
