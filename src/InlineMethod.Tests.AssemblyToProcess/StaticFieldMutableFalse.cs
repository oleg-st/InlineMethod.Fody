namespace InlineMethod.Tests.AssemblyToProcess;

class StaticFieldMutableFalse
{
    public static bool Ready = false;
    // Force a type initializer: Roslyn can omit a lone default-value initializer.
    public static int Count = 1;

    [Inline]
    private static int Callee(int x) => x * 2;

    public static int Caller(int x) => Ready ? Callee(x) : 0;
}
