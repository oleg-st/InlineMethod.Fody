using System.Collections.Generic;
using System;

namespace InlineMethod.Tests.AssemblyToProcess;

class ExceptionHandler
{
    [Inline]
    private int Callee(bool x)
    {
        return x ? 5 : 6;
    }

    public void Caller(IEnumerable<int> values)
    {
        try
        {
            Console.WriteLine(0);
        }
        catch (InvalidOperationException)
        {
            Console.WriteLine(1);
        }
        catch (InvalidCastException)
        {
            Console.WriteLine(2);
        }
        finally
        {
            Console.WriteLine(3);
        }

        foreach (var value in values)
        {
            var x = value switch
            {
                1 => 3,
                2 => 4,
                _ => throw new NotSupportedException(),
            };
            Console.WriteLine(Callee(true));
        }
    }

    public void Inlined(IEnumerable<int> values)
    {
        try
        {
            Console.WriteLine(0);
        }
        catch (InvalidOperationException)
        {
            Console.WriteLine(1);
        }
        catch (InvalidCastException)
        {
            Console.WriteLine(2);
        }
        finally
        {
            Console.WriteLine(3);
        }

        foreach (var value in values)
        {
            var x = value switch
            {
                1 => 3,
                2 => 4,
                _ => throw new NotSupportedException(),
            };
            Console.WriteLine(5);
        }
    }
}
