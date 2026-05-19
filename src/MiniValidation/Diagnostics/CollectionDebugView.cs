// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET6_0_OR_GREATER
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace MiniValidation.Diagnostics;

/// <summary>
/// Allows the debugger to display collections.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class CollectionDebugView
{
    private readonly IEnumerable _enumeration;

    public CollectionDebugView(IEnumerable enumeration)
    {
        _enumeration = enumeration;
    }

    /// <summary>
    /// Gets the array that is shown by the debugger.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public object[] Items => _enumeration.Cast<object>().ToArray();
}
#endif