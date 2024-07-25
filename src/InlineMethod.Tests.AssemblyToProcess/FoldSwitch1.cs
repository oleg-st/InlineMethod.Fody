namespace InlineMethod.Tests.AssemblyToProcess;

class FoldSwitch1
{
    [Inline]
    private int Callee(int x)
    {
        switch (x)
        {
            case 1:
                return 11;
            case 2:
                return 22;
            case 3:
                return 33;
            default:
                return -1;
        }
    }

    public int Caller(uint y)
    {
        return Callee(3);
    }

    public int Inlined(uint y)
    {
        return 33;
    }
}
