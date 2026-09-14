using System;
using System.Runtime.CompilerServices;

namespace InlineMethod.Tests.AssemblyToProcess;

// A hand-written class with the shape the weaver uses to recognize a compiler-generated lambda
// cache: sealed, [CompilerGenerated], holding a static delegate field. That shape says nothing
// about how often Callback is assigned - it is an ordinary mutable field that any code can set -
// so the store below is not the value the load above reads, and the guard must survive.
[CompilerGenerated]
sealed class StaticFieldCompilerGeneratedLookalike
{
    public static Action Callback = () => { };

    [Inline]
    private static int Callee(int x)
    {
        return x * 2;
    }

    public static int Caller(int x)
    {
        var result = Callback != null ? Callee(x) : 0;
        Callback = null;
        return result;
    }

    public static int Inlined(int x)
    {
        var result = Callback != null ? x * 2 : 0;
        Callback = null;
        return result;
    }
}
