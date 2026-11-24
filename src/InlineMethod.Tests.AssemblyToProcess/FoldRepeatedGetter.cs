namespace InlineMethod.Tests.AssemblyToProcess;

class FoldRepeatedGetter
{
    private static bool Enabled { [Inline] get => false; }

    public static int Caller(int value)
    {
        if (Enabled)
            return 1;
        return Enabled ? 2 : value;
    }

    public static int Inlined(int value) => value;
}
