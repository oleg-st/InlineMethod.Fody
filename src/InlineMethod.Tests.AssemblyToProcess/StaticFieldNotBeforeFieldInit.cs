namespace InlineMethod.Tests.AssemblyToProcess;

// A static readonly field of a type that is not beforefieldinit: the load triggers the static
// constructor, so it must be kept even though the field can only hold the constant it is given.
class StaticFieldNotBeforeFieldInit
{
    public static readonly bool Ready;

    // what folding away the load of Ready would leave unset, by skipping the static constructor
    public static int Count;

    static StaticFieldNotBeforeFieldInit()
    {
        Ready = true;
        Count = 1;
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
