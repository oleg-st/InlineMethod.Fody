using System;
using System.Runtime.CompilerServices;

namespace InlineMethod.Tests.AssemblyToProcess;

class DelegateSafety
{
    public static Func<int> Callback = () => 1;
    public static int GeneratedLocalCount;
    public static int GeneratedArgumentCount;
    public static int CreatedCount;

    [Inline]
    private static int Apply([ResolveDelegate] Func<int> callback) => callback();

    [Inline]
    private static int Ignore(Action callback) => 42;

    public static int MutableCaller() => Apply(Callback);

    [CompilerGenerated]
    private sealed class CacheHolder
    {
        public static readonly CacheHolder Singleton = new();
        public static Func<int>? Cache;

        public int One() => 1;
        public int Two() => 2;
    }

    public static int GeneratedCacheCaller() =>
        Apply(CacheHolder.Cache ??= new Func<int>(CacheHolder.Singleton.One));

    public static void ReplaceGeneratedCache() =>
        CacheHolder.Cache = new Func<int>(CacheHolder.Singleton.Two);

    [CompilerGenerated]
    private sealed class LocalHolder
    {
        public static Action Callback = () => { };

        static LocalHolder() => GeneratedLocalCount++;
    }

    [CompilerGenerated]
    private sealed class ArgumentHolder
    {
        public static Action Callback = () => { };

        static ArgumentHolder() => GeneratedArgumentCount++;
    }

    public static int GeneratedLocal()
    {
        var callback = LocalHolder.Callback;
        Ignore(callback);
        return Ignore(callback);
    }

    public static int GeneratedArgument() => Ignore(ArgumentHolder.Callback);

    private sealed class Receiver
    {
        public void Run() { }
    }

    private static Receiver Create()
    {
        CreatedCount++;
        return new Receiver();
    }

    public static int CreatedLocal()
    {
        Action callback = Create().Run;
        Ignore(callback);
        return Ignore(callback);
    }
}
