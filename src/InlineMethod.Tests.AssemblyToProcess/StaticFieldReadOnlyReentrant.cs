namespace InlineMethod.Tests.AssemblyToProcess;

class StaticFieldReadOnlyReentrant
{
    public static int DuringInitialization = -1;

    private static class Holder
    {
        private static readonly int Observation = Observe();
        public static readonly bool Ready = true;

        private static int Observe()
        {
            // Reenter before Ready is assigned: its observable value is still false.
            DuringInitialization = Caller(21);
            return 0;
        }
    }

    [Inline]
    private static int Callee(int x) => x * 2;

    public static int Caller(int x) => Holder.Ready ? Callee(x) : 0;
}
