namespace InlineMethod.Tests.AssemblyToProcess;

class FoldBeqFalse
{
    [Inline]
    private int Callee(int x)
    {
        return x == 555 ? 1 : 2;
    }

    public int Caller(int y)
    {
        return Callee(666);
    }

    public int Inlined(int y)
    {
        return 2;
    }
}
