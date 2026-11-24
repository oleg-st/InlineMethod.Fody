using System;
using System.Collections.Generic;
using System.Linq;
using Fody;
using InlineMethod.Fody;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace InlineMethod.Tests;

public partial class ModuleWeaverTests(ITestOutputHelper testOutputHelper)
{
    private static readonly ModuleDefinition ModuleDefinition;
    private static readonly TestResult TestResult;
    private const string Namespace = "InlineMethod.Tests.AssemblyToProcess";

    static ModuleWeaverTests()
    {
        var weavingTask = new ModuleWeaver();
        TestResult = weavingTask.ExecuteTestRun("InlineMethod.Tests.AssemblyToProcess.dll", false);

        var readerParameters = new ReaderParameters
        {
            AssemblyResolver = weavingTask.AssemblyResolver,
            SymbolReaderProvider = new DefaultSymbolReaderProvider(false),
            ReadWrite = false,
            ReadSymbols = true,
        };
        ModuleDefinition = ModuleDefinition.ReadModule(TestResult.AssemblyPath, readerParameters);
    }

    private MethodDefinition? GetMethod(TypeDefinition type, string name) 
        => type.Methods.SingleOrDefault(m => m.Name == name);

    private class InstructionComparer : IEqualityComparer<Instruction>
    {
        public bool Equals(Instruction? x, Instruction? y)
        {
            // todo
            return x?.ToString() == y?.ToString();
        }

        public int GetHashCode(Instruction obj)
        {
            return HashCode.Combine(obj.OpCode, obj.Operand);
        }
    }

    private void PrintMethod(MethodDefinition method)
    {
        testOutputHelper.WriteLine($"Method {method.FullName}");
        foreach (var instruction in method.Body.Instructions)
        {
            testOutputHelper.WriteLine($"{instruction}");
        }
    }


    private void CheckSimpleClass(TypeDefinition type, bool isEndsWith = false)
    {
        var simpleCaller = GetMethod(type, "Caller");
        var simpleCallerInlined = GetMethod(type, "Inlined");
        Assert.NotNull(simpleCaller);
        Assert.NotNull(simpleCallerInlined);

        var callerInstructions = simpleCaller.Body.Instructions;
        var inlinedInstructions = simpleCallerInlined.Body.Instructions;
        if (isEndsWith)
        {
            // cut first instructions
            var sliced = callerInstructions
                .Skip(Math.Max(0, callerInstructions.Count - inlinedInstructions.Count))
                .ToArray();

            // adjust offsets
            if (sliced.Length > 0)
            {
                var startOffset = sliced[0].Offset;
                foreach (var instruction in sliced)
                {
                    instruction.Offset -= startOffset;
                }
            }

            callerInstructions = new ReadOnlyCollection<Instruction>(sliced);
        }

        var isSame = callerInstructions.SequenceEqual(inlinedInstructions, new InstructionComparer());
        if (!isSame)
        {
            PrintMethod(simpleCaller);
            PrintMethod(simpleCallerInlined);
        }
        Assert.True(isSame);
    }

    [Fact]
    public void FoldRepeatedGetter()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.FoldRepeatedGetter");
        CheckSimpleClass(type);
        Assert.Null(GetMethod(type, "get_Enabled"));
    }

    [Fact]
    public void SimpleRemovePrivate()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.SimpleRemovePrivate");
        CheckSimpleClass(type);
        // removed Callee
        Assert.Null(GetMethod(type, "Callee"));
    }

    [Fact]
    public void SimpleRemove()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.SimpleRemove");
        CheckSimpleClass(type);
        // removed Callee
        Assert.Null(GetMethod(type, "Callee"));
    }

    [Fact]
    public void SimpleKeep()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.SimpleKeep");
        CheckSimpleClass(type);
        // keep Callee
        Assert.NotNull(GetMethod(type, "Callee"));
    }

    [Fact]
    public void SimpleGenericMethod()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.SimpleGenericMethod");
        CheckSimpleClass(type);
    }

    [Fact]
    public void SimpleGenericField()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.SimpleGenericField");
        CheckSimpleClass(type);
    }

    [Fact]
    public void FoldBeqTrue()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.FoldBeqTrue");
        CheckSimpleClass(type);
    }

    [Fact]
    public void FoldBeqFalse()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.FoldBeqFalse");
        CheckSimpleClass(type);
    }

    [Fact]
    public void FoldBrTrue()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.FoldBrTrue");
        CheckSimpleClass(type);
    }

    [Fact]
    public void FoldBrFalse()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.FoldBrFalse");
        CheckSimpleClass(type);
    }

    [Fact]
    public void FoldBgtFalse()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.FoldBgtFalse");
        CheckSimpleClass(type);
    }

    [Fact]
    public void FoldBgtUnFalse()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.FoldBgtUnFalse");
        CheckSimpleClass(type);
    }

    [Fact]
    public void FoldFoldBgtMixed()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.FoldFoldBgtMixed");
        CheckSimpleClass(type);
    }

    [Fact]
    public void FoldComplex()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.FoldComplex");
        CheckSimpleClass(type);
    }

    [Fact]
    public void FoldSwitch1()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.FoldSwitch1");
        CheckSimpleClass(type);
    }

    [Fact]
    public void FoldSwitch2()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.FoldSwitch2");
        CheckSimpleClass(type);
    }

    [Fact]
    public void FoldDeep()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.FoldDeep");
        CheckSimpleClass(type);
    }

    [Fact]
    public void FoldVar()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.FoldVar");
        CheckSimpleClass(type);
    }

    [Fact]
    public void FoldVar2()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.FoldVar2");
        CheckSimpleClass(type);
    }

    [Fact]
    public void ArgBranch()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.ArgBranch");
        CheckSimpleClass(type);
    }

    [Fact]
    public void InlineDelegate()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.InlineDelegate");
        CheckSimpleClass(type);
    }

    [Fact]
    public void InlineDelegate2()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.InlineDelegate2`1");
        CheckSimpleClass(type);
    }

    [Fact]
    public void InlineGeneric()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.InlineGeneric`1");
        CheckSimpleClass(type);
    }

    [Fact]
    public void InlineGeneric2()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.InlineGeneric2`1");
        CheckSimpleClass(type);
    }

    [Fact]
    public void WithException()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.WithException");
        CheckSimpleClass(type);
    }

    [Fact]
    public void LateEvaluation()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.LateEvaluation");
        CheckSimpleClass(type);
    }

    [Fact]
    public void ExceptionHandler()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.ExceptionHandler");
        CheckSimpleClass(type);
    }

    [Fact]
    public void ExceptionHandler2()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.ExceptionHandler2");
        CheckSimpleClass(type);
    }

    [Fact]
    public void FoldCall()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.FoldCall");
        CheckSimpleClass(type);
    }

    [Fact]
    public void TestDup()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.TestDup");
        CheckSimpleClass(type);
    }

    [Fact]
    public void TestDup2()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.TestDup2");
        CheckSimpleClass(type);
    }

    [Fact]
    public void TestDup3()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.TestDup3");
        CheckSimpleClass(type);
    }

    [Fact]
    public void TestSideEffect()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.TestSideEffect");
        CheckSimpleClass(type);
    }

    [Theory]
    [InlineData("StaticFieldMutable", true)]
    [InlineData("StaticFieldMutableFalse", false)]
    public void StaticFieldMutableRuntime(string name, bool initialValue)
    {
        var type = TestResult.Assembly.GetType($"{Namespace}.{name}");
        Assert.NotNull(type);
        var caller = type.GetMethod("Caller");
        var ready = type.GetField("Ready");
        Assert.NotNull(caller);
        Assert.NotNull(ready);

        Assert.Equal(initialValue ? 42 : 0, caller.Invoke(null, [21]));
        ready.SetValue(null, !initialValue);
        Assert.Equal(initialValue ? 0 : 42, caller.Invoke(null, [21]));
        ready.SetValue(null, initialValue);
        Assert.Equal(initialValue ? 42 : 0, caller.Invoke(null, [21]));
    }

    [Fact]
    public void StaticFieldStoredInBodyRuntime()
    {
        var type = TestResult.Assembly.GetType($"{Namespace}.StaticFieldStoredInBody");
        Assert.NotNull(type);
        var caller = type.GetMethod("Caller");
        Assert.NotNull(caller);

        Assert.Equal(0, caller.Invoke(null, [21]));
    }

    [Fact]
    public void StaticFieldGeneratedMutableRuntime()
    {
        var type = TestResult.Assembly.GetType($"{Namespace}.StaticFieldGeneratedMutable");
        Assert.NotNull(type);
        var caller = type.GetMethod("Caller");
        Assert.NotNull(caller);

        Assert.Equal(42, caller.Invoke(null, [21]));
        Assert.Equal(0, caller.Invoke(null, [21]));
    }

    [Theory]
    [InlineData("StaticFieldNotBeforeFieldInit", false)]
    [InlineData("StaticFieldBeforeFieldInit", true)]
    public void StaticFieldInitializationRuntime(string name, bool beforeFieldInit)
    {
        var definition = ModuleDefinition.GetType($"{Namespace}.{name}");
        var holder = definition.NestedTypes.Single(t => t.Name == "Holder");
        Assert.Equal(beforeFieldInit, holder.IsBeforeFieldInit);
        var callerDefinition = GetMethod(definition, "Caller");
        Assert.NotNull(callerDefinition);
        // Also check the load: eager initialization by the JIT could mask its removal.
        Assert.Contains(callerDefinition.Body.Instructions, instruction =>
            instruction.OpCode.Code == Code.Ldsfld &&
            instruction.Operand is FieldReference {Name: "Ready"});

        var type = TestResult.Assembly.GetType($"{Namespace}.{name}");
        Assert.NotNull(type);
        var caller = type.GetMethod("Caller");
        var count = type.GetField("Count");
        Assert.NotNull(caller);
        Assert.NotNull(count);

        Assert.Equal(42, caller.Invoke(null, [21]));
        Assert.Equal(1, count.GetValue(null));
    }

    [Fact]
    public void StaticFieldReadOnlyReentrantRuntime()
    {
        var type = TestResult.Assembly.GetType($"{Namespace}.StaticFieldReadOnlyReentrant");
        Assert.NotNull(type);
        var caller = type.GetMethod("Caller");
        var duringInitialization = type.GetField("DuringInitialization");
        Assert.NotNull(caller);
        Assert.NotNull(duringInitialization);

        Assert.Equal(42, caller.Invoke(null, [21]));
        Assert.Equal(0, duringInitialization.GetValue(null));
    }

    [Theory]
    [InlineData("StaticFieldUnusedReadOnly")]
    [InlineData("StaticFieldUnusedDelegate")]
    public void StaticFieldUnusedArgumentRuntime(string name)
    {
        var definition = ModuleDefinition.GetType($"{Namespace}.{name}");
        var callerDefinition = GetMethod(definition, "Caller");
        Assert.NotNull(callerDefinition);
        Assert.Contains(callerDefinition.Body.Instructions, instruction =>
            instruction.OpCode.Code == Code.Ldsfld);

        var type = TestResult.Assembly.GetType($"{Namespace}.{name}");
        Assert.NotNull(type);
        var caller = type.GetMethod("Caller");
        var count = type.GetField("Count");
        Assert.NotNull(caller);
        Assert.NotNull(count);

        Assert.Equal(42, caller.Invoke(null, [21]));
        Assert.Equal(1, count.GetValue(null));
    }

    [Theory]
    [InlineData("DoubleGreater")]
    [InlineData("DoubleLess")]
    [InlineData("SingleGreater")]
    [InlineData("SingleLess")]
    public void StaticFieldNaNRuntime(string name)
    {
        var type = TestResult.Assembly.GetType($"{Namespace}.StaticFieldNaN");
        Assert.NotNull(type);
        var caller = type.GetMethod(name);
        Assert.NotNull(caller);

        Assert.Equal(2, caller.Invoke(null, null));
    }

    [Fact]
    public void StaticFieldMutableAutoPropertyRuntime()
    {
        var definition = ModuleDefinition.GetType($"{Namespace}.StaticFieldMutableAutoProperty");
        var backingField = definition.Fields.Single(f => f.Name == "<Callback>k__BackingField");
        Assert.True(definition.IsSealed);
        Assert.Contains(definition.CustomAttributes, attribute =>
            attribute.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");
        Assert.True(backingField.IsStatic);
        Assert.False(backingField.IsInitOnly);
        var initializer = definition.Methods.Single(m => m.IsStatic && m.IsConstructor);
        Assert.Contains(initializer.Body.Instructions, instruction =>
            instruction.OpCode.Code == Code.Stsfld && instruction.Operand == backingField);
        var callerDefinition = GetMethod(definition, "Caller");
        Assert.NotNull(callerDefinition);
        Assert.Contains(callerDefinition.Body.Instructions, instruction =>
            instruction.OpCode.Code == Code.Ldsfld && instruction.Operand == backingField);
        Assert.DoesNotContain(callerDefinition.Body.Instructions, instruction =>
            instruction.Operand is MethodReference {Name: "Callee" or "get_Callback"});

        var type = TestResult.Assembly.GetType($"{Namespace}.StaticFieldMutableAutoProperty");
        Assert.NotNull(type);
        var caller = type.GetMethod("Caller");
        var callback = type.GetProperty("Callback");
        Assert.NotNull(caller);
        Assert.NotNull(callback);

        Assert.Equal(0, caller.Invoke(null, [21]));
        callback.SetValue(null, (Action)(() => { }));
        Assert.Equal(42, caller.Invoke(null, [21]));
        callback.SetValue(null, null);
        Assert.Equal(0, caller.Invoke(null, [21]));
    }

    [Fact]
    public void StaticFieldForeignInitializerRuntime()
    {
        var definition = ModuleDefinition.GetType($"{Namespace}.StaticFieldForeignInitializer");
        var other = definition.NestedTypes.Single(t => t.Name == "Other");
        Assert.True(definition.IsBeforeFieldInit);
        Assert.False(other.IsBeforeFieldInit);
        var initializer = definition.Methods.Single(m => m.IsStatic && m.IsConstructor);
        var instructions = initializer.Body.Instructions;
        var foreignLoad = instructions.Single(instruction =>
            instruction.OpCode.Code == Code.Ldsfld &&
            instruction.Operand is FieldReference {Name: "Value"} field &&
            field.DeclaringType.Resolve() == other);
        var readyStore = instructions.Single(instruction =>
            instruction.OpCode.Code == Code.Stsfld &&
            instruction.Operand is FieldReference {Name: "Ready"});
        Assert.True(instructions.IndexOf(foreignLoad) < instructions.IndexOf(readyStore));
        var callerDefinition = GetMethod(definition, "Caller");
        Assert.NotNull(callerDefinition);
        Assert.Contains(callerDefinition.Body.Instructions, instruction =>
            instruction.OpCode.Code == Code.Ldsfld &&
            instruction.Operand is FieldReference {Name: "Ready"});
        Assert.DoesNotContain(callerDefinition.Body.Instructions, instruction =>
            instruction.Operand is MethodReference {Name: "Callee"});

        var type = TestResult.Assembly.GetType($"{Namespace}.StaticFieldForeignInitializer");
        Assert.NotNull(type);
        var duringInitialization = type.GetField("DuringInitialization");
        var caller = type.GetMethod("Caller");
        Assert.NotNull(duringInitialization);
        Assert.NotNull(caller);

        // Trigger initialization through the field before invoking Caller from the test.
        Assert.Equal(0, duringInitialization.GetValue(null));
        Assert.Equal(42, caller.Invoke(null, [21]));
    }

    [Theory]
    [InlineData("DoubleGreater", "DoubleValue")]
    [InlineData("DoubleLess", "DoubleValue")]
    [InlineData("SingleGreater", "SingleValue")]
    [InlineData("SingleLess", "SingleValue")]
    public void StaticFieldNaNAfterCallRuntime(string name, string fieldName)
    {
        var definition = ModuleDefinition.GetType($"{Namespace}.StaticFieldNaNAfterCall");
        var initializer = definition.Methods.Single(m => m.IsStatic && m.IsConstructor);
        var instructions = initializer.Body.Instructions;
        var call = instructions.Single(instruction =>
            instruction.OpCode.Code == Code.Call &&
            instruction.Operand is MethodReference {Name: "Initialize"});
        var store = instructions.Single(instruction =>
            instruction.OpCode.Code == Code.Stsfld &&
            instruction.Operand is FieldReference field && field.Name == fieldName);
        Assert.True(instructions.IndexOf(call) < instructions.IndexOf(store));
        var callerDefinition = GetMethod(definition, name);
        Assert.NotNull(callerDefinition);
        Assert.DoesNotContain(callerDefinition.Body.Instructions, instruction =>
            instruction.Operand is MethodReference {Name: "Callee"});

        var type = TestResult.Assembly.GetType($"{Namespace}.StaticFieldNaNAfterCall");
        Assert.NotNull(type);
        var caller = type.GetMethod(name);
        Assert.NotNull(caller);

        Assert.Equal(2, caller.Invoke(null, null));
    }
}
