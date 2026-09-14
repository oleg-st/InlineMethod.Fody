namespace InlineMethod.Tests.AssemblyToProcess;

// Both fields are readonly on a beforefieldinit type, but Early's initializer reads Ready before
// Ready's own initializer has run, so Early stays false. The static constructor's stores carry no
// order among them, so the weaver must not answer the earlier load with the later store.
class StaticFieldStaticConstructorOrder
{
    public static readonly bool Early = Ready;

    public static readonly bool Ready = true;

    [Inline]
    private static int Callee(int x)
    {
        return x * 2;
    }

    public static int Caller(int x)
    {
        if (!Early)
        {
            return 0;
        }

        return Callee(x);
    }

    public static int Inlined(int x)
    {
        if (!Early)
        {
            return 0;
        }

        return x * 2;
    }
}
