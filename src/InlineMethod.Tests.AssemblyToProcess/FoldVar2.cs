namespace InlineMethod.Tests.AssemblyToProcess;

class FoldVar2
{
    [Inline]
    private int Callee(int y, int z)
    {
        var a = z == 1 ? 1 : 0;
        var b = z == 2 ? 1 : 0;
        var x = a == 0 || b == 0 ? y + 56 : 55;
        return x != 0 ? x : 55;
    }

    public int Caller(int y)
    {
        return Callee(y, 0);
    }

    public int Inlined(int y)
    {
        var x = y + 56;
        return x != 0 ? x : 55;
    }
}
