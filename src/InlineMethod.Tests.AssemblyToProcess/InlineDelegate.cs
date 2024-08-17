using System;

namespace InlineMethod.Tests.AssemblyToProcess;

class InlineDelegate
{
    [Inline]
    private int Callee(int[] span, [ResolveDelegate] Predicate<int> predicate)
    {
        for (var i = 0; i < span.Length; i++)
            if (predicate(span[i]))
                return i;

        return -1;
    }

    public int Caller(int[] span)
    {
       return Callee(span, x => x is 5);
    }

    public int Inlined(int[] span)
    {
        for (var i = 0; i < span.Length; i++)
            if (span[i] is 5)
                return i;

        return -1;
    }
}
