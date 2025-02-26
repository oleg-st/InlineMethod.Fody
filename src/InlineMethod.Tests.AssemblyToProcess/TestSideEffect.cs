namespace InlineMethod.Tests.AssemblyToProcess;

class TestSideEffect
{
    [Inline]
    private int Callee(int x)
    {
        return 2;
    }

    private int V() => 1;

    public int Caller(uint y)
    {
        return Callee(V());
    }

    public int Inlined(uint y)
    {
        V();
        return 2;
    }
}
