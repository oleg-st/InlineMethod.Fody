namespace InlineMethod.Tests.AssemblyToProcess;

class FoldBrTrue
{
    [Inline]
    private int Callee(bool x)
    {
        return x ? 1 : 2;
    }

    public int Caller(int y)
    {
        return Callee(true);
    }

    public int Inlined(int y)
    {
        return 1;
    }
}
