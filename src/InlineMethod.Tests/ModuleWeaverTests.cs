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
    private const string Namespace = "InlineMethod.Tests.AssemblyToProcess";

    static ModuleWeaverTests()
    {
        var weavingTask = new ModuleWeaver();
        var testResult = weavingTask.ExecuteTestRun("InlineMethod.Tests.AssemblyToProcess.dll", false);

        var readerParameters = new ReaderParameters
        {
            AssemblyResolver = weavingTask.AssemblyResolver,
            SymbolReaderProvider = new DefaultSymbolReaderProvider(false),
            ReadWrite = false,
            ReadSymbols = true,
        };
        ModuleDefinition = ModuleDefinition.ReadModule(testResult.AssemblyPath, readerParameters);
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
        CheckSimpleClass(type, true);
    }
}
