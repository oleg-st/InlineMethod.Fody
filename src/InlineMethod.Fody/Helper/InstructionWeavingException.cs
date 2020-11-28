using Fody;
using Mono.Cecil.Cil;

namespace InlineMethod.Fody.Helper
{
    internal class InstructionWeavingException : WeavingException
    {
        public Instruction Instruction { get; }

        public InstructionWeavingException(Instruction instruction, string message)
            : base(message)
        {
            Instruction = instruction;
        }
    }
}
