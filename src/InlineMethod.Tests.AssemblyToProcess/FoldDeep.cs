namespace InlineMethod.Tests.AssemblyToProcess;

class FoldDeep
{
    [Inline]
    private static int Callee1(int y)
    {
        return y == 8 ? 5 : 6;
    }

    [Inline]
    private static int Callee2(bool x)
    {
        return Callee1(x ? 7 : 8);
    }

    public int Caller(uint y)
    {
        return Callee2(false);
    }

    public int Inlined(uint y)
    {
        return 5;
    }
}
