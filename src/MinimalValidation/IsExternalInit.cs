#if !NET6_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    // Required to make records work on .NET Standard 2.0
    internal static class IsExternalInit { }
}
#endif