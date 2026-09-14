namespace InlineMethod.Tests.AssemblyToProcess;

// Ready is readonly on a beforefieldinit type, but the static constructor calls out before
// storing it and the method it calls reads it. That load runs while Ready is still false, so the
// store is not the value every load observes and the guard cannot be folded away.
class StaticFieldStaticConstructorCall
{
    // runs first, while Ready still holds its default
    public static readonly int DuringInit = Helper();

    public static readonly bool Ready = true;

    private static int Helper()
    {
        return Caller(21);
    }

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
