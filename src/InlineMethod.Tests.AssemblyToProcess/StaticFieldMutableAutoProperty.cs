using System;
using System.Runtime.CompilerServices;

namespace InlineMethod.Tests.AssemblyToProcess;

[CompilerGenerated]
sealed class StaticFieldMutableAutoProperty
{
    // The compiler-generated <Callback>k__BackingField is mutable, not a lambda cache.
    public static Action? Callback { [Inline] get; set; } = null;

    // Keep the null store in the emitted type initializer.
    public static int Marker = 1;

    [Inline]
    private static int Callee(int x) => x * 2;

    public static int Caller(int x) => Callback != null ? Callee(x) : 0;
}
