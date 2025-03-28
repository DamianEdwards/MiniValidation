// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET6_0_OR_GREATER
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace MiniValidation.Diagnostics;

/// <summary>Allows the debugger to display collections.</summary>
[ExcludeFromCodeCoverage]
internal sealed class CollectionDebugView
{
    public CollectionDebugView(IEnumerable enumeration) => _enumeration = enumeration;

		/// <summary>A reference to the enumeration to display.</summary>
		private readonly IEnumerable _enumeration;

    /// <summary>The array that is shown by the debugger.</summary>
    /// <remarks>
    /// Every time the enumeration is shown in the debugger, a new array is created.
    /// By doing this, it is always in sync with the current state of the enumeration.
    /// </remarks>
    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public object[] Items => _enumeration.Cast<object>().ToArray();
}
#endif
