using System;
using Xunit;

namespace InlineMethod.Tests;

public partial class ModuleWeaverTests
{
    [Fact]
    public void DelegateSafetyGeneratedCacheReassignmentRuntime()
    {
        var type = TestResult.Assembly.GetType($"{Namespace}.DelegateSafety");
        Assert.NotNull(type);
        var caller = type.GetMethod("GeneratedCacheCaller");
        var replace = type.GetMethod("ReplaceGeneratedCache");
        Assert.NotNull(caller);
        Assert.NotNull(replace);

        Assert.Equal(1, caller.Invoke(null, null));
        replace.Invoke(null, null);
        Assert.Equal(2, caller.Invoke(null, null));
    }

    [Fact]
    public void DelegateSafetyMutableFieldRuntime()
    {
        var type = TestResult.Assembly.GetType($"{Namespace}.DelegateSafety");
        Assert.NotNull(type);
        var caller = type.GetMethod("MutableCaller");
        var callback = type.GetField("Callback");
        Assert.NotNull(caller);
        Assert.NotNull(callback);

        Assert.Equal(1, caller.Invoke(null, null));
        callback.SetValue(null, (Func<int>)(() => 2));
        Assert.Equal(2, caller.Invoke(null, null));
    }

    [Theory]
    [InlineData("GeneratedLocal", "GeneratedLocalCount")]
    [InlineData("GeneratedArgument", "GeneratedArgumentCount")]
    [InlineData("CreatedLocal", "CreatedCount")]
    public void DelegateSafetyUnusedValueRuntime(string methodName, string countName)
    {
        var type = TestResult.Assembly.GetType($"{Namespace}.DelegateSafety");
        Assert.NotNull(type);
        var caller = type.GetMethod(methodName);
        var count = type.GetField(countName);
        Assert.NotNull(caller);
        Assert.NotNull(count);

        Assert.Equal(42, caller.Invoke(null, null));
        Assert.Equal(1, count.GetValue(null));
    }
}
