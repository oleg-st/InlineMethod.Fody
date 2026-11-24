namespace InlineMethod.Tests.AssemblyToProcess;

class StaticFieldBeforeFieldInit
{
    public static int Count;

    private static class Holder
    {
        public static readonly bool Ready = true;
        private static readonly int Initialization = Initialize();

        private static int Initialize() => ++Count;
    }

    [Inline]
    private static int Callee(int x) => x * 2;

    public static int Caller(int x) => Holder.Ready ? Callee(x) : 0;
}
