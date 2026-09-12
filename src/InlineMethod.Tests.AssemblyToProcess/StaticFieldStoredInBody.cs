namespace InlineMethod.Tests.AssemblyToProcess;

// A store to a mutable static field in this method body does not give the value of a later load
// either: anything the method calls in between can reassign the field.
class StaticFieldStoredInBody
{
    public static bool Ready;

    private static void Flip()
    {
        Ready = false;
    }

    [Inline]
    private static int Callee(int x)
    {
        return x * 2;
    }

    public static int Caller(int x)
    {
        Ready = true;
        Flip();
        if (!Ready)
        {
            return 0;
        }

        return Callee(x);
    }

    public static int Inlined(int x)
    {
        Ready = true;
        Flip();
        if (!Ready)
        {
            return 0;
        }

        return x * 2;
    }
}
