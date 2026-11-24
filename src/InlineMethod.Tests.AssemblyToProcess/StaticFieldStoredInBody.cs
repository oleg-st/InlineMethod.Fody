namespace InlineMethod.Tests.AssemblyToProcess;

class StaticFieldStoredInBody
{
    private static bool Ready;

    private static void Flip() => Ready = false;

    [Inline]
    private static int Callee(int x) => x * 2;

    public static int Caller(int x)
    {
        Ready = true;
        Flip();
        return Ready ? Callee(x) : 0;
    }
}
