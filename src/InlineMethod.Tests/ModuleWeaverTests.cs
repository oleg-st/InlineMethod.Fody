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

public class ModuleWeaverTests(ITestOutputHelper testOutputHelper)
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

    private static bool LoadsField(Instruction instruction, string fieldName) =>
        instruction.OpCode.Code == Code.Ldsfld && (instruction.Operand as FieldReference)?.Name == fieldName;

    private static bool StoresField(Instruction instruction, string fieldName) =>
        instruction.OpCode.Code == Code.Stsfld && (instruction.Operand as FieldReference)?.Name == fieldName;

    // A guard test compares Caller against Inlined, which also holds if the weaver did nothing at
    // all - so pin that the call really was inlined, and that the guard survived the inlining.
    private void CheckGuardKept(TypeDefinition type, string fieldName)
    {
        CheckSimpleClass(type);

        var caller = GetMethod(type, "Caller");
        Assert.NotNull(caller);

        Assert.DoesNotContain(caller.Body.Instructions,
            i => i.OpCode.Code is Code.Call or Code.Callvirt &&
                 (i.Operand as MethodReference)?.Name == "Callee");
        Assert.Contains(caller.Body.Instructions, i => LoadsField(i, fieldName));
    }

    // ...and that there was a store for the weaver to fold from in the first place. Without this
    // a test still passes if the store stops being emitted - Roslyn drops an initializer that
    // writes a default value, so a field whose initializer is edited to one can silently take the
    // whole case with it.
    private void CheckStaticConstructorStores(TypeDefinition type, string fieldName)
    {
        var staticConstructor = type.Methods.SingleOrDefault(m => m.IsStatic && m.IsConstructor);
        Assert.True(staticConstructor != null,
            $"{type.Name} emits no static constructor, so there is no store of {fieldName} to fold " +
            "and the case no longer tests anything. Roslyn omits an initializer that writes a " +
            "default value: keep a field initialized to a non-default one on the type.");
        Assert.True(staticConstructor.Body.Instructions.Any(i => StoresField(i, fieldName)),
            $"{type.Name}'s static constructor does not store {fieldName}, so there is no value " +
            "to fold and the case no longer tests anything.");
    }

    // the opposite pin: the fold that should still happen did happen, and had a store to use
    private void CheckGuardFolded(TypeDefinition type, string fieldName)
    {
        CheckStaticConstructorStores(type, fieldName);

        var caller = GetMethod(type, "Caller");
        Assert.NotNull(caller);
        Assert.DoesNotContain(caller.Body.Instructions, i => LoadsField(i, fieldName));
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

    [Fact]
    public void StaticFieldMutable()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.StaticFieldMutable");
        CheckGuardKept(type, "Ready");
        CheckStaticConstructorStores(type, "Ready");
    }

    [Fact]
    public void StaticFieldMutableFalse()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.StaticFieldMutableFalse");
        CheckGuardKept(type, "Ready");
        // Count exists only to force a static constructor to be emitted; this is what fails if it
        // is ever removed as unused, instead of the case quietly going vacuous
        CheckStaticConstructorStores(type, "Ready");
    }

    // the woven method must still read the field at run time, in both directions
    [Fact]
    public void StaticFieldMutableRuntime()
    {
        var type = TestResult.Assembly.GetType($"{Namespace}.StaticFieldMutable");
        Assert.NotNull(type);
        var caller = type.GetMethod("Caller");
        var ready = type.GetField("Ready");
        Assert.NotNull(caller);
        Assert.NotNull(ready);

        ready.SetValue(null, false);
        Assert.Equal(0, caller.Invoke(null, [21]));

        ready.SetValue(null, true);
        Assert.Equal(42, caller.Invoke(null, [21]));
    }

    [Fact]
    public void StaticFieldStoredInBody()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.StaticFieldStoredInBody");
        CheckGuardKept(type, "Ready");
        // here the store the weaver must not trust is in Caller itself, not a static constructor
        var caller = GetMethod(type, "Caller");
        Assert.NotNull(caller);
        Assert.Contains(caller.Body.Instructions, i => StoresField(i, "Ready"));
    }

    [Fact]
    public void StaticFieldStoredInBodyRuntime()
    {
        var type = TestResult.Assembly.GetType($"{Namespace}.StaticFieldStoredInBody");
        Assert.NotNull(type);
        var caller = type.GetMethod("Caller");
        Assert.NotNull(caller);

        // Flip() clears the flag that Caller just set, so the guard returns 0
        Assert.Equal(0, caller.Invoke(null, [21]));
    }

    [Fact]
    public void StaticFieldNotBeforeFieldInit()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.StaticFieldNotBeforeFieldInit");
        CheckGuardKept(type, "Ready");
        CheckStaticConstructorStores(type, "Ready");
    }

    // the shape of a compiler-generated lambda cache does not make a field single-assignment
    [Fact]
    public void StaticFieldCompilerGeneratedLookalike()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.StaticFieldCompilerGeneratedLookalike");
        CheckGuardKept(type, "Callback");
        CheckStaticConstructorStores(type, "Callback");
    }

    [Fact]
    public void StaticFieldCompilerGeneratedLookalikeRuntime()
    {
        var type = TestResult.Assembly.GetType($"{Namespace}.StaticFieldCompilerGeneratedLookalike");
        Assert.NotNull(type);
        var caller = type.GetMethod("Caller");
        Assert.NotNull(caller);

        // Callback starts out non-null, and Caller clears it on the way out
        Assert.Equal(42, caller.Invoke(null, [21]));
        Assert.Equal(0, caller.Invoke(null, [21]));
    }

    // a readonly field is not settled while the static constructor that stores it is still running
    [Fact]
    public void StaticFieldStaticConstructorCall()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.StaticFieldStaticConstructorCall");
        CheckGuardKept(type, "Ready");
        CheckStaticConstructorStores(type, "Ready");
    }

    [Fact]
    public void StaticFieldStaticConstructorCallRuntime()
    {
        var type = TestResult.Assembly.GetType($"{Namespace}.StaticFieldStaticConstructorCall");
        Assert.NotNull(type);
        var duringInit = type.GetField("DuringInit");
        Assert.NotNull(duringInit);

        // Caller ran from the static constructor, before Ready was stored, so it took the guard
        Assert.Equal(0, duringInit.GetValue(null));
    }

    [Fact]
    public void StaticFieldStaticConstructorOrder()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.StaticFieldStaticConstructorOrder");
        CheckGuardKept(type, "Early");
        CheckStaticConstructorStores(type, "Early");
    }

    [Fact]
    public void StaticFieldStaticConstructorOrderRuntime()
    {
        var type = TestResult.Assembly.GetType($"{Namespace}.StaticFieldStaticConstructorOrder");
        Assert.NotNull(type);
        var caller = type.GetMethod("Caller");
        Assert.NotNull(caller);

        // Early read Ready before Ready was stored, so it is false and the guard is taken
        Assert.Equal(0, caller.Invoke(null, [21]));
    }

    [Fact]
    public void FoldStaticFieldReadOnly()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.FoldStaticFieldReadOnly");
        CheckSimpleClass(type);
        CheckGuardFolded(type, "Ready");
    }

    // the lambda cache fields delegate inlining depends on are still trusted
    [Fact]
    public void InlineDelegateFoldsCacheField()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.InlineDelegate");
        var caller = GetMethod(type, "Caller");
        Assert.NotNull(caller);
        Assert.DoesNotContain(caller.Body.Instructions,
            i => i.OpCode.Code is Code.Callvirt && (i.Operand as MethodReference)?.Name == "Invoke");
    }
}
