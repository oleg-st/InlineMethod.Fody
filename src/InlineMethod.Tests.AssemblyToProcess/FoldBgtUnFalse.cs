namespace InlineMethod.Tests.AssemblyToProcess;

class FoldBgtUnFalse
{
    [Inline]
    private int Callee(uint x, uint y)
    {
        return x > y ? 1 : 2;
    }

    public int Caller(uint y)
    {
        return Callee(3, 4);
    }

    public int Inlined(uint y)
    {
        return 2;
    }
}
