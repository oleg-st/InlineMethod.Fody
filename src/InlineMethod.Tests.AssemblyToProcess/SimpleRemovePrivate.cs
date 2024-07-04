namespace InlineMethod.Tests.AssemblyToProcess;

class SimpleRemovePrivate
{
    [InlineMethod.Inline(InlineBehavior.RemovePrivate)]
    private int Callee(int x)
    {
        return x;
    }

    public int Caller(int y)
    {
        return Callee(555 + y);
    }

    public int Inlined(int y)
    {
        return 555 + y;
    }
}
