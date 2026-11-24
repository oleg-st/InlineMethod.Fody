namespace InlineMethod.Tests.AssemblyToProcess;

class StaticFieldMutable
{
    public static bool Ready = true;

    [Inline]
    private static int Callee(int x) => x * 2;

    public static int Caller(int x) => Ready ? Callee(x) : 0;
}
