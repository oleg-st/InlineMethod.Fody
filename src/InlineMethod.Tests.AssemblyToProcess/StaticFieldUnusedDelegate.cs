using System;

namespace InlineMethod.Tests.AssemblyToProcess;

class StaticFieldUnusedDelegate
{
    public static int Count;

    private static class Holder
    {
        public static readonly Action Callback = () => { };
        private static readonly int Initialization = Initialize();

        private static int Initialize() => ++Count;
    }

    [Inline]
    private static int Callee(Action unused, int x) => x * 2;

    public static int Caller(int x) => Callee(Holder.Callback, x);
}
