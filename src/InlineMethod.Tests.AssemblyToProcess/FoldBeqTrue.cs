namespace InlineMethod.Tests.AssemblyToProcess;

class FoldBeqTrue
{
    [Inline]
    private int Callee(int x)
    {
        return x == 555 ? 1 : 2;
    }

    public int Caller(int y)
    {
        return Callee(555);
    }

    public int Inlined(int y)
    {
        return 1;
    }
}
