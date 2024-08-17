using InlineMethod.Fody.Helper;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace InlineMethod.Fody.Extensions;

internal static class CecilExtensions
{
    private static int GetArgCount(OpCode opCode, IMethodSignature method)
    {
        var argCount = method.Parameters.Count;

        if (method is {HasThis: true, ExplicitThis: false} && opCode.Code != Code.Newobj)
            ++argCount;

        if (opCode.Code == Code.Calli)
            ++argCount;

        return argCount;
    }

    public static int GetPopCount(this Instruction instruction)
    {
        if (instruction.OpCode.FlowControl == FlowControl.Call)
            return GetArgCount(instruction.OpCode, (IMethodSignature)instruction.Operand);

        return instruction.OpCode.StackBehaviourPop switch
        {
            StackBehaviour.Pop0 => 0,
            StackBehaviour.Popi or StackBehaviour.Popref or StackBehaviour.Pop1 => 1,
            StackBehaviour.Pop1_pop1 or StackBehaviour.Popi_pop1 or StackBehaviour.Popi_popi
                or StackBehaviour.Popi_popi8 or StackBehaviour.Popi_popr4 or StackBehaviour.Popi_popr8
                or StackBehaviour.Popref_pop1 or StackBehaviour.Popref_popi => 2,
            StackBehaviour.Popi_popi_popi or StackBehaviour.Popref_popi_popi or StackBehaviour.Popref_popi_popi8
                or StackBehaviour.Popref_popi_popr4 or StackBehaviour.Popref_popi_popr8
                or StackBehaviour.Popref_popi_popref => 3,
            StackBehaviour.PopAll => throw new InstructionWeavingException(instruction,
                "Unexpected stack-clearing instruction encountered"),
            _ => throw new InstructionWeavingException(instruction, "Could not locate method argument value")
        };
    }

    public static int GetPushCount(this Instruction instruction)
    {
        if (instruction.OpCode.FlowControl == FlowControl.Call)
        {
            var method = (IMethodSignature)instruction.Operand;
            return method.ReturnType.MetadataType != MetadataType.Void || instruction.OpCode.Code == Code.Newobj ? 1 : 0;
        }

        return instruction.OpCode.StackBehaviourPush switch
        {
            StackBehaviour.Push0 => 0,
            StackBehaviour.Push1 or StackBehaviour.Pushi or StackBehaviour.Pushi8 or StackBehaviour.Pushr4
                or StackBehaviour.Pushr8 or StackBehaviour.Pushref => 1,
            StackBehaviour.Push1_push1 => 2,
            _ => throw new InstructionWeavingException(instruction, "Could not locate method argument value")
        };
    }
}