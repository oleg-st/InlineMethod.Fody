using InlineMethod.Fody.Helper;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace InlineMethod.Fody.Extensions
{
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

                stackToConsume -= pushCount;

                if (stackToConsume == 0 && result == null)
                    result = currentInstruction;

                if (stackToConsume < 0)
                    throw new InstructionWeavingException(startInstruction, $"Could not locate call argument due to {currentInstruction} which pops an unexpected number of items from the stack");

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

            if (instruction.OpCode.Code == Code.Dup)
                return 0;

            switch (instruction.OpCode.StackBehaviourPop)
            {
                case StackBehaviour.Pop0:
                    return 0;

                case StackBehaviour.Popi:
                case StackBehaviour.Popref:
                case StackBehaviour.Pop1:
                    return 1;

                case StackBehaviour.Pop1_pop1:
                case StackBehaviour.Popi_pop1:
                case StackBehaviour.Popi_popi:
                case StackBehaviour.Popi_popi8:
                case StackBehaviour.Popi_popr4:
                case StackBehaviour.Popi_popr8:
                case StackBehaviour.Popref_pop1:
                case StackBehaviour.Popref_popi:
                    return 2;

                case StackBehaviour.Popi_popi_popi:
                case StackBehaviour.Popref_popi_popi:
                case StackBehaviour.Popref_popi_popi8:
                case StackBehaviour.Popref_popi_popr4:
                case StackBehaviour.Popref_popi_popr8:
                case StackBehaviour.Popref_popi_popref:
                    return 3;

                case StackBehaviour.PopAll:
                    throw new InstructionWeavingException(instruction, "Unexpected stack-clearing instruction encountered");

                default:
                    throw new InstructionWeavingException(instruction, "Could not locate method argument value");
            }
        }

        public static int GetPushCount(this Instruction instruction)
        {
            if (instruction.OpCode.FlowControl == FlowControl.Call)
            {
                var method = (IMethodSignature)instruction.Operand;
                return method.ReturnType.MetadataType != MetadataType.Void || instruction.OpCode.Code == Code.Newobj ? 1 : 0;
            }

            if (instruction.OpCode.Code == Code.Dup)
                return 1;

            switch (instruction.OpCode.StackBehaviourPush)
            {
                case StackBehaviour.Push0:
                    return 0;

                case StackBehaviour.Push1:
                case StackBehaviour.Pushi:
                case StackBehaviour.Pushi8:
                case StackBehaviour.Pushr4:
                case StackBehaviour.Pushr8:
                case StackBehaviour.Pushref:
                    return 1;

                case StackBehaviour.Push1_push1:
                    return 2;

                default:
                    throw new InstructionWeavingException(instruction, "Could not locate method argument value");
            }
        }
    }
}
