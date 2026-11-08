using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace AutoMapSharp.Context;

/// <summary>
/// Compares objects by reference identity across all .NET runtime versions.
/// </summary>
internal sealed class AutoMapSharpReferenceEqualityComparer : IEqualityComparer<object>
{
    public static readonly AutoMapSharpReferenceEqualityComparer Instance = new();

    public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

    public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
}
