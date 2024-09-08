using System;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace InlineMethod.Tests.AssemblyToProcess;

class InlineDelegate2<TAnimation>
{
    public class Animation<T>
    {
        public void Change(T a)
        {
        }
    }

    private Animation<TAnimation>[] _animations; 

    public void Caller<T>(T value)
        where T : struct, Enum =>
        Callee<T, T>(value, static (a, v) => a.Change(v));

    [Inline]
    void Callee<T, TState>(
        TState state,
        [ResolveDelegate] Action<Animation<T>, TState> action
    )
        where T : struct, Enum
    {
        var animations = _animations.AsSpan();
        ref var start = ref MemoryMarshal.GetReference(animations);
        ref var end = ref Unsafe.Add(ref start, animations.Length);

        for (; Unsafe.IsAddressLessThan(ref start, ref end); start = ref Unsafe.Add(ref start, 1))
            if (start is Animation<T> a)
                action(a, state);
    }

    public void Inlined<T>(T value)
        where T : struct, Enum
    {
        var animations = _animations.AsSpan();
        ref var start = ref MemoryMarshal.GetReference(animations);
        ref var end = ref Unsafe.Add(ref start, animations.Length);

        for (; Unsafe.IsAddressLessThan(ref start, ref end); start = ref Unsafe.Add(ref start, 1))
            if (start is Animation<T> a)
                a.Change(value);

    }
}
