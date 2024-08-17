namespace InlineMethod.Tests.AssemblyToProcess;

class ArgBranch
{
    [Inline]
    private int Callee(int x, int y)
    {
        return x + y;
    }

    public int Caller(bool y)
    {
        return Callee(555, y ? 55 : 56);
    }

    public int Inlined(bool y)
    {
        return 555 + (y ? 55 : 56);
    }
}
