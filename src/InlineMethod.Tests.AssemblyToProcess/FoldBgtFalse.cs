namespace InlineMethod.Tests.AssemblyToProcess;

class FoldBgtFalse
{
    [Inline]
    private int Callee(int x, int y)
    {
        return x > y ? 1 : 2;
    }

    public int Caller(int y)
    {
        return Callee(3, 4);
    }

    public int Inlined(int y)
    {
        return 2;
    }
}
