using System.Net.Http.Headers;
using System.Threading;

namespace InlineMethod.Tests.AssemblyToProcess;

unsafe class TestDup
{
    public static Struct Caller()
    {
        Struct s = default;
        return s.Value = Struct.Default;
    }

    public readonly struct Struct
    {
        public static readonly Struct Default = default;

        public Struct Value
        {
            [Inline]
            set { }
        }
    }

    public static Struct Inlined()
    {
        Struct s = default;
        Struct v = Struct.Default;
        return v;
    }
}
