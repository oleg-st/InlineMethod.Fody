namespace InlineMethod.Tests.AssemblyToProcess;

class StaticFieldNotBeforeFieldInit
{
    public static int Count;

    private static class Holder
    {
        public static readonly bool Ready;

        static Holder()
        {
            Ready = true;
            Count++;
        }
    }

    [Inline]
    private static int Callee(int x) => x * 2;

    // Calling this method does not itself initialize Holder.
    public static int Caller(int x) => Holder.Ready ? Callee(x) : 0;
}
