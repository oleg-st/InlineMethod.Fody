namespace InlineMethod.Tests.AssemblyToProcess;

class FoldVar
{
    [Inline]
    private int Callee(int x)
    {
        var a = x == 1 ? 1 : 0;
        var b = x == 2 ? 1 : 0;
        var c = a != 0 || b != 0 ? 1 : 0;
        return c != 0 ? 55 : 56;
    }

    public int Caller(uint y)
    {
        return Callee(0);
    }

    public int Inlined(uint y)
    {
        return 56;
    }
}
