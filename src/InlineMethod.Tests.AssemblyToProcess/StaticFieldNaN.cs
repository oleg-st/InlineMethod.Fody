namespace InlineMethod.Tests.AssemblyToProcess;

class StaticFieldNaN
{
    public static readonly double DoubleValue = double.NaN;
    public static readonly float SingleValue = float.NaN;

    [Inline]
    private static int Callee(bool condition)
    {
        if (condition)
        {
            return 1;
        }

        return 2;
    }

    public static int DoubleGreater() => Callee(DoubleValue > 0);
    public static int DoubleLess() => Callee(DoubleValue < 0);
    public static int SingleGreater() => Callee(SingleValue > 0);
    public static int SingleLess() => Callee(SingleValue < 0);
}
