using InlineMethod.Fody.Helper;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace InlineMethod.Fody.Extensions;

internal static class CecilExtensions
{
    public static Instruction?[] GetArgumentPushInstructions(this Instruction instruction)
    {
        if (instruction.OpCode.FlowControl != FlowControl.Call)
            throw new InstructionWeavingException(instruction, "Expected a call instruction");

        var method = (IMethodSignature)instruction.Operand;
        var argCount = GetArgCount(instruction.OpCode, method);

        if (argCount == 0)
            return [];

        var result = new Instruction?[argCount];
        var currentInstruction = instruction.Previous;

        for (var paramIndex = result.Length - 1; paramIndex >= 0; --paramIndex)
            result[paramIndex] = BackwardScanPush(ref currentInstruction);

        return result;
    }

    public static Instruction? GetSinglePushInstruction(this Instruction instruction)
    {
        var currentInstruction = instruction.Previous;
        return BackwardScanPush(ref currentInstruction);
    }

    public static (Instruction?, Instruction?) GetTwoPushInstructions(this Instruction instruction)
    {
        var currentInstruction = instruction.Previous;
        var second = BackwardScanPush(ref currentInstruction);
        if (second == null)
        {
            return (null, null);
        }

        var first = BackwardScanPush(ref currentInstruction);
        return (first, second);
    }

    public static Instruction?[] GetPushInstructions(this Instruction instruction, int count)
    {
        if (count == 0)
            return [];

        var result = new Instruction?[count];
        var currentInstruction = instruction.Previous;

        for (var paramIndex = result.Length - 1; paramIndex >= 0; --paramIndex)
            result[paramIndex] = BackwardScanPush(ref currentInstruction);

        return result;
    }

    private static Instruction? BackwardScanPush(ref Instruction? currentInstruction)
    {
        if (currentInstruction == null)
        {
            return null;
        }

        var startInstruction = currentInstruction;
        Instruction? result = null;
        var stackToConsume = 1;

        while (stackToConsume > 0)
        {
            switch (currentInstruction.OpCode.FlowControl)
            {
                case FlowControl.Branch:
                case FlowControl.Cond_Branch:
                case FlowControl.Return:
                case FlowControl.Throw:
                    currentInstruction = null;
                    return null;

                case FlowControl.Call:
                    if (currentInstruction.OpCode.Code == Code.Jmp)
                    {
                        currentInstruction = null;
                        return null;
                    }

                    break;
            }

            var popCount = GetPopCount(currentInstruction);
            var pushCount = GetPushCount(currentInstruction);

            if (pushCount > 0)
            {
                result = stackToConsume switch
                {
                    1 when result == null => currentInstruction,
                    < 1 => throw new InstructionWeavingException(startInstruction,
                        $"Could not locate call argument due to {currentInstruction} which pops an unexpected number of items from the stack"),
                    _ => result
                };
                stackToConsume -= pushCount;
            }

            stackToConsume += popCount;
            currentInstruction = currentInstruction.Previous;
        }

        return result ?? throw new InstructionWeavingException(startInstruction, "Could not locate call argument, reached beginning of method");
    }

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