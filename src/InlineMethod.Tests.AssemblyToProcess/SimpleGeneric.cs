using System;

namespace InlineMethod.Tests.AssemblyToProcess;

class SimpleGeneric
{
    [InlineMethod.Inline]
    private Type Callee<T>()
    {
        return typeof(T);
    }

    public Type Caller(int y)
    {
        return Callee<object>();
    }

    public Type Inlined(int y)
    {
        return typeof(object);
    }
}
