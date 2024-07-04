namespace InlineMethod.Tests.AssemblyToProcess;

class SimpleKeep
{
    [InlineMethod.Inline(InlineBehavior.Keep)]
    private int Callee(int x, int y)
    {
        return x - y;
    }

    public int Caller(int x, int y)
    {
        return Callee(y, x);
    }

    public int Inlined(int x, int y)
    {
        return y - x;
    }
}
