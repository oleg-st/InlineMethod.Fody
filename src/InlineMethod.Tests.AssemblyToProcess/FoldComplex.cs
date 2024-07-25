namespace InlineMethod.Tests.AssemblyToProcess;

class FoldComplex
{
    [Inline]
    private int Callee(int x)
    {
        return (x + 55) / (x == 55 ? 2 : 3) * (x == 110 ? 2 : 3) * x + (x | 3) + (x ^ 3) + (x & 3) - (x >> 1) * (x << 1) == 
               (55 + 55) / 2 * 3 * 55 + (55 | 3) + (55 ^ 3) + (55 & 3) - (55 >> 1) * (55 << 1) ? 5 : 10;
    }

    public int Caller(uint y)
    {
        return Callee(55);
    }

    public int Inlined(uint y)
    {
        return 5;
    }
}
