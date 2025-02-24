namespace InlineMethod.Tests.AssemblyToProcess;

unsafe class FoldCall
{
    private static int Method1(int a, int b, int c)
    {
        return a + b + c;
    }

    private static int Method2(int a, int b, int c)
    {
        return a - b - c;
    }

    [Inline]
    private int Callee(int x)
    {
        delegate*<int, int, int, int> p =  x != 0 ? &Method1 : &Method2;
        return p(55, 56, 57) + p(61, 62, 63);
    }

    public int Caller()
    {
        return Callee(1);
    }

    public int Inlined()
    {
        return Method1(55, 56, 57) + Method1(61, 62, 63); ;
    }
}
