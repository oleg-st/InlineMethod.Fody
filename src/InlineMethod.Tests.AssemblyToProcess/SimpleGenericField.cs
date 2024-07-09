using System.Collections.Generic;

namespace InlineMethod.Tests.AssemblyToProcess;

class SimpleGenericField
{
    private static class Helper<T>
    {
        public static readonly IEqualityComparer<T> Comparer = EqualityComparer<T>.Default;
    }

    [InlineMethod.Inline]
    private IEqualityComparer<T> Callee<T>()
    {
        return Helper<T>.Comparer;
    }

    public IEqualityComparer<object> Caller()
    {
        return Callee<object>();
    }

    public IEqualityComparer<object> Inlined()
    {
        return Helper<object>.Comparer;
    }
}
