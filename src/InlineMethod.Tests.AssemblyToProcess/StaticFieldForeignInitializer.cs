namespace InlineMethod.Tests.AssemblyToProcess;

class StaticFieldForeignInitializer
{
    // Loading another type's field runs its initializer before Ready is assigned.
    public static readonly int DuringInitialization = Other.Value;
    public static readonly bool Ready = true;

    private static class Other
    {
        public static readonly int Value;

        static Other()
        {
            Value = Caller(21);
        }
    }

    [Inline]
    private static int Callee(int x) => x * 2;

    public static int Caller(int x) => Ready ? Callee(x) : 0;
}
