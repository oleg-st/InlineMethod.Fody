using System.Collections.Generic;
using System;

namespace InlineMethod.Tests.AssemblyToProcess;

class ExceptionHandler2
{
    [Inline]
    private static void Callee()
        => Console.WriteLine("Hello, World!");

    public void Caller(IEnumerable<int> values)
    {
        Callee();
        while (true)
        {
            using var output = Console.OpenStandardOutput();
            // will be removed during inlining
            int x = 1;
            break;
        }
    }

    public void Inlined(IEnumerable<int> values)
    {
        Console.WriteLine("Hello, World!");
        while (true)
        {
            using var output = Console.OpenStandardOutput();
            break;
        }
    }
}
