using System;
using System.Runtime.CompilerServices;

namespace InlineMethod.Tests.AssemblyToProcess;

// Generated code can contain ordinary mutable fields as well as lambda caches.
[CompilerGenerated]
sealed class StaticFieldGeneratedMutable
{
    public static Action? Callback = () => { };

    [Inline]
    private static int Callee(int x) => x * 2;

    public static int Caller(int x)
    {
        var result = Callback != null ? Callee(x) : 0;
        Callback = null;
        return result;
    }
}
