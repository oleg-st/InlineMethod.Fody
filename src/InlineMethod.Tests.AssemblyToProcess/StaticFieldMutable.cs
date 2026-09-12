namespace InlineMethod.Tests.AssemblyToProcess;

// A mutable static field is not a constant: its initializer must not be folded
// into a branch condition, even when the initializing store is a constant.
class StaticFieldMutable
{
    public static bool Ready = true;

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
        if (!Ready)
        {
            return 0;
        }

        return x * 2;
    }
}
