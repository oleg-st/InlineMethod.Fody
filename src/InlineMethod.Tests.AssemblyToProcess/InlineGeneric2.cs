using System;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace InlineMethod.Tests.AssemblyToProcess;

class InlineGeneric2<TAnimation>
{
    public class Animation<T>
    {
        public void Change(T a)
        {
        }
    }

    private Animation<TAnimation>[] _animations;

    private static class C
    {
        [Inline]
        internal static void Callee<TT>(Animation<TT> a, TT v) where TT : struct
        {
            a.Change(v);
        }
    }

    public void Caller<T>(T value)
        where T : struct
    {
        var animations = _animations.AsSpan();
        ref var start = ref MemoryMarshal.GetReference(animations);
        ref var end = ref Unsafe.Add(ref start, animations.Length);

        for (; Unsafe.IsAddressLessThan(ref start, ref end); start = ref Unsafe.Add(ref start, 1))
            if (start is Animation<T> a)
                C.Callee(a, value);
    }

    public void Inlined<T>(T value)
        where T : struct
    {
        var animations = _animations.AsSpan();
        ref var start = ref MemoryMarshal.GetReference(animations);
        ref var end = ref Unsafe.Add(ref start, animations.Length);

        for (; Unsafe.IsAddressLessThan(ref start, ref end); start = ref Unsafe.Add(ref start, 1))
            if (start is Animation<T> a)
                a.Change(value);

    }
}
