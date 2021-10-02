using System.Collections.Generic;
using System.Linq;
using Fody;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace InlineMethod.Fody
{
    public class ModuleWeaver : BaseModuleWeaver
    {
        public CustomAttribute GetInlineAttribute(MethodDefinition method)
            => method.CustomAttributes.FirstOrDefault(i => i.AttributeType.FullName == "InlineMethod.InlineAttribute");

        private readonly HashSet<string> _visitedMethods = new HashSet<string>();

        private void ProcessCallInstruction(Instruction instruction, MethodDefinition method)
        {
            if (instruction.Operand is MethodReference calledMethod)
            {
                var calledMethodDefinition = calledMethod.Resolve();
                if (calledMethodDefinition != null && GetInlineAttribute(calledMethodDefinition) != null)
                {
                    ProcessMethod(calledMethodDefinition);
                    var inlineMethodWeaver = new InlineMethodWeaver(this, instruction, method, calledMethodDefinition);
                    inlineMethodWeaver.Process();
                }
            }
        }

        public void ProcessMethod(MethodDefinition method)
        {
            if (method.Body == null || _visitedMethods.Contains(method.FullName))
            {
                return;
            }

            _visitedMethods.Add(method.FullName);

            var instruction = method.Body.Instructions.FirstOrDefault();
            while (instruction != null)
            {
                var nextInstruction = instruction.Next;
                if (instruction.OpCode.Code == Code.Call)
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
                    var remove = (bool)attr.ConstructorArguments.Single().Value;
                    if (remove && method.IsPrivate)
                    {
                        method.DeclaringType.Methods.Remove(method);
                    }
                    else
                    {
                        // consume attribute
                        method.CustomAttributes.Remove(attr);
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
            => Enumerable.Empty<string>();

        public override bool ShouldCleanReference => true;
    }
}
