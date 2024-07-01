using Fody;
using Mono.Cecil.Cil;

namespace InlineMethod.Fody.Helper;

internal class InstructionWeavingException(Instruction instruction, string message) : WeavingException(message)
{
    public Instruction Instruction { get; } = instruction;
}