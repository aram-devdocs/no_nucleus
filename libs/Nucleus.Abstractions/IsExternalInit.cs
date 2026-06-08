// Polyfill so C# `init`-only property accessors compile on netstandard2.1 (the type that the compiler
// requires for `init` ships only in .NET 5+). Internal + compiler-only; no runtime footprint.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
