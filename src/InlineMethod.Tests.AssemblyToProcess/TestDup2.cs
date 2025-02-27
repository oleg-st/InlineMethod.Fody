using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Threading;

namespace InlineMethod.Tests.AssemblyToProcess;

unsafe class TestDup2
{
    public static void Caller()
    {
        var s = new C();
        _ = s.Value = C.None;
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
        var s = new C();
        C none;
        C value = none = C.None;
        s.Values[s.Offset] = value;
    }
}
