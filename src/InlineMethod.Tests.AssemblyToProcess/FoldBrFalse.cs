namespace InlineMethod.Tests.AssemblyToProcess;

class FoldBrFalse
{
    [Inline]
    private int Callee(bool x)
    {
        return x ? 1 : 2;
    }

    public int Caller(int y)
    {
        return Callee(false);
    }

    public int Inlined(int y)
    {
        return 2;
    }
}
