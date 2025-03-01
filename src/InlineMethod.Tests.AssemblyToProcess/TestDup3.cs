using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Threading;
using static InlineMethod.Tests.AssemblyToProcess.TestDup;

namespace InlineMethod.Tests.AssemblyToProcess;

unsafe class TestDup3
{
    public static void Caller()
    {
        C s = new C();
        var value = C.None;
        _ = s.Value = value;
    }

    public class C
    {
        public static readonly C None = new C();
        public readonly int Offset = 0;

        public readonly List<C> Values = [None];

        public C() { }

        public C Value
        {
            [Inline]
            set => Values[Offset] = value;
        }
    }

    public static void Inlined()
    {
        C s = new C();
        var value = C.None;
        // _ = value
        C v = value;
        s.Values[s.Offset] = value;
    }
}
