using System;

namespace InlineMethod.Tests.AssemblyToProcess;

class WithException
{
    [Inline]
    private int Callee(int x)
    {
        try
        {
            return x;
        }
        catch (Exception)
        {
            return x;
        }
    }

    public int Caller(uint y)
    {
        return Callee(55);
    }

    public int Inlined(uint y)
    {
        try
        {
            return 55;
        }
        catch (Exception)
        {
            return 55;
        }
    }
}
