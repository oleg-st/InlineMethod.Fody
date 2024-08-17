using System.Collections.Generic;
using System.Linq;
using Fody;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace InlineMethod.Fody;

public class ModuleWeaver : BaseModuleWeaver
{
    private CustomAttribute? GetInlineAttribute(MethodDefinition method)
        => method.CustomAttributes.FirstOrDefault(i => i.AttributeType.FullName == "InlineMethod.InlineAttribute");

    private readonly HashSet<string> _visitedMethods = [];

    public void ProcessCallInstruction(Instruction instruction, MethodDefinition method, bool force = false)
    {
        if (instruction.Operand is MethodReference calledMethod)
        {
            var calledMethodDefinition = calledMethod.Resolve();
            if (calledMethodDefinition != null && (force || GetInlineAttribute(calledMethodDefinition) != null))
            {
                ProcessMethod(calledMethodDefinition);
                var inlineMethodWeaver = new InlineMethodWeaver(this, instruction, method, calledMethodDefinition);
                inlineMethodWeaver.Process();
            }
        }
    }

    private void ProcessMethod(MethodDefinition method)
    {
        if (method.Body == null || !_visitedMethods.Add(method.FullName))
        {
            return;
        }

        var instruction = method.Body.Instructions.FirstOrDefault();
        while (instruction != null)
        {
            var nextInstruction = instruction.Next;
            if (instruction.OpCode.Code is Code.Call or Code.Callvirt)
            {
                ProcessCallInstruction(instruction, method);
            }

            instruction = nextInstruction;
        }
    }

    public override void Execute()
    {
        WriteMessage("InlineMethod execute", MessageImportance.High);

        var inlineMethods = new List<MethodDefinition>();
        // inline methods
        foreach (var type in ModuleDefinition.GetTypes())
        {
            foreach (var method in type.Methods)
            {
                if (GetInlineAttribute(method) != null)
                {
                    inlineMethods.Add(method);
                }

                ProcessMethod(method);
            }
        }

        // remove methods
        foreach (var method in inlineMethods)
        {
            var attr = GetInlineAttribute(method);
            if (attr != null)
            {
                var value = attr.ConstructorArguments.First().Value;
                if (value is bool boolValue)
                {
                    value = boolValue ? InlineBehavior.RemovePrivate : InlineBehavior.Keep;
                }

                var behavior = (InlineBehavior)value;
                if ((behavior == InlineBehavior.RemovePrivate && method.IsPrivate) ||
                    behavior == InlineBehavior.Remove)
                {
                    method.DeclaringType.Methods.Remove(method);
                }
                else
                {
                    var export = (bool)(attr.ConstructorArguments.Skip(1).FirstOrDefault().Value ?? false);
                    // consume attribute
                    if (!export)
                    {
                        method.CustomAttributes.Remove(attr);
                    }
                }
            }
        }

        var assemblyRef = ModuleDefinition.AssemblyReferences.FirstOrDefault(i => i.Name == "InlineMethod");
        if (assemblyRef != null)
        {
            ModuleDefinition.AssemblyReferences.Remove(assemblyRef);
        }
    }

    public override IEnumerable<string> GetAssembliesForScanning()
        => [];

    public override bool ShouldCleanReference => true;
}
