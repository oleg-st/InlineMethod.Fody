namespace InlineMethod.Tests.AssemblyToProcess;

// Same as StaticFieldMutable, but initialized to the value that makes the guard
// look always taken: the code after the guard must survive.
class StaticFieldMutableFalse
{
    public static bool Ready = false;

    // Roslyn omits an initializer that stores a default value, so `Ready = false` on its own
    // emits no static constructor and there is no store for the weaver to find. It takes a
    // second field with an initializer to emit one. Do not remove this as unused: without it the
    // case has nothing to fold and passes whatever the weaver does. ModuleWeaverTests calls
    // CheckStaticConstructorStores to fail loudly rather than silently if that ever happens.
    public static int Count = 1;

    [Inline]
    private static int Callee(int x)
    {
        return x * 2;
    }

    public static int Caller(int x)
    {
        if (!Ready)
        {
            return 0;
        }

        return Callee(x);
    }

    public static int Inlined(int x)
    {
        if (!Ready)
        {
            return 0;
        }

        return x * 2;
    }
}
