namespace InlineMethod.Tests.AssemblyToProcess;

// A static readonly field initialized to a constant in a beforefieldinit type
// can only ever hold that constant, so the guard is still folded away.
class FoldStaticFieldReadOnly
{
    public static readonly bool Ready = true;

    [Inline]
    private static int Callee(int x)
    {
        return x * 2;
    }

    public static int Caller(int x)
    {
        if (!Ready)
        {
            return 0;
        }

        return Callee(x);
    }

    public static int Inlined(int x)
    {
        return x * 2;
    }
}
