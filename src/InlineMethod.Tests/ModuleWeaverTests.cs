using System;
using System.Collections.Generic;
using System.Linq;
using Fody;
using InlineMethod.Fody;
using Mono.Cecil;
using Mono.Cecil.Cil;
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
        var testResult = weavingTask.ExecuteTestRun("InlineMethod.Tests.AssemblyToProcess.dll");

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

    private void CheckSimpleClass(TypeDefinition type)
    {
        var simpleCaller = GetMethod(type, "Caller");
        var simpleCallerInlined = GetMethod(type, "Inlined");
        Assert.NotNull(simpleCaller);
        Assert.NotNull(simpleCallerInlined);

        var isSame = simpleCaller.Body.Instructions.SequenceEqual(simpleCallerInlined.Body.Instructions, new InstructionComparer());
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
    public void SimpleGeneric()
    {
        var type = ModuleDefinition.GetType($"{Namespace}.SimpleGeneric");
        CheckSimpleClass(type);
    }
}
