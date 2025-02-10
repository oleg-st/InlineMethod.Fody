namespace InlineMethod.Tests.AssemblyToProcess;

class LateEvaluation
{
    [Inline]
    private static bool Implies(bool self, [InlineParameter] bool other)
    {
        if (self)
        {
            return other;
        }

        return true;
    }

    private bool Method()
    {
        return true;
    }

    public bool Caller(bool a)
    {
       return Implies(a, Method());
    }

    public bool Inlined(bool a)
    {
        return !a || Method();
    }
}
