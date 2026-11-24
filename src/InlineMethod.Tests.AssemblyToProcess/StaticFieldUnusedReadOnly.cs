namespace InlineMethod.Tests.AssemblyToProcess;

class StaticFieldUnusedReadOnly
{
    public static int Count;

    private static class Holder
    {
        public static readonly int Value = 42;
        private static readonly int Initialization = Initialize();

        private static int Initialize() => ++Count;
    }

    [Inline]
    private static int Callee(int unused, int x) => x * 2;

    public static int Caller(int x) => Callee(Holder.Value, x);
}
