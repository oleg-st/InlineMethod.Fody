using System;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;

namespace InlineMethod.Fody.Helper
{
    internal static class OpCodeHelper
    {
        public static ParameterDefinition GetArgParameterDefinition(Instruction instruction,
            InlineMethodWeaver.MethodParameters parameters)
        {
            switch (instruction.OpCode.Code)
            {
                case Code.Ldarg_0:
                    return parameters[0];
                case Code.Ldarg_1:
                    return parameters[1];
                case Code.Ldarg_2:
                    return parameters[2];
                case Code.Ldarg_3:
                    return parameters[3];
            }

            if (instruction.OpCode.OperandType == OperandType.InlineArg ||
                instruction.OpCode.OperandType == OperandType.ShortInlineArg)
            {
                return (ParameterDefinition) instruction.Operand;
            }

            return null;
        }

        public static VariableDefinition GetLocVariableDefinition(Instruction instruction,
            Collection<VariableDefinition> variables)
        {
            switch (instruction.OpCode.Code)
            {
                case Code.Ldloc_0:
                case Code.Stloc_0:
                    return variables[0];
                case Code.Ldloc_1:
                case Code.Stloc_1:
                    return variables[1];
                case Code.Ldloc_2:
                case Code.Stloc_2:
                    return variables[2];
                case Code.Ldloc_3:
                case Code.Stloc_3:
                    return variables[3];
            }

            if (instruction.OpCode.OperandType == OperandType.InlineVar ||
                instruction.OpCode.OperandType == OperandType.ShortInlineVar)
            {
                return (VariableDefinition) instruction.Operand;
            }

            return null;
        }

        private static bool IsShortIndex(int index) => index <= short.MaxValue;

        public static Instruction CreateLoadLoc(VariableDefinition variableDefinition)
        {
            switch (variableDefinition.Index)
            {
                case 0:
                    return Instruction.Create(OpCodes.Ldloc_0);
                case 1:
                    return Instruction.Create(OpCodes.Ldloc_1);
                case 2:
                    return Instruction.Create(OpCodes.Ldloc_2);
                case 3:
                    return Instruction.Create(OpCodes.Ldloc_3);
                default:
                    return Instruction.Create(IsShortIndex(variableDefinition.Index) ? OpCodes.Ldloc_S : OpCodes.Ldloc, variableDefinition);
            }
        }

        public static Instruction CreateLoadLocA(VariableDefinition variableDefinition)
        {
            return Instruction.Create(IsShortIndex(variableDefinition.Index) ? OpCodes.Ldloca_S : OpCodes.Ldloca,
                variableDefinition);
        }

        public static Instruction CreateStoreLoc(VariableDefinition variableDefinition)
        {
            switch (variableDefinition.Index)
            {
                case 0:
                    return Instruction.Create(OpCodes.Stloc_0);
                case 1:
                    return Instruction.Create(OpCodes.Stloc_1);
                case 2:
                    return Instruction.Create(OpCodes.Stloc_2);
                case 3:
                    return Instruction.Create(OpCodes.Stloc_3);
                default:
                    return Instruction.Create(IsShortIndex(variableDefinition.Index) ? OpCodes.Stloc_S : OpCodes.Stloc,
                        variableDefinition);
            }
        }

        public static bool IsLoadConst(Instruction instruction)
        {
            var code = instruction.OpCode.Code;
            return code == Code.Ldc_I4 ||
                   code == Code.Ldc_I4_0 ||
                   code == Code.Ldc_I4_1 ||
                   code == Code.Ldc_I4_2 ||
                   code == Code.Ldc_I4_3 ||
                   code == Code.Ldc_I4_4 ||
                   code == Code.Ldc_I4_5 ||
                   code == Code.Ldc_I4_6 ||
                   code == Code.Ldc_I4_7 ||
                   code == Code.Ldc_I4_8 ||
                   code == Code.Ldc_I4_M1 ||
                   code == Code.Ldc_I4_S ||
                   code == Code.Ldc_I8 ||
                   code == Code.Ldc_R4 ||
                   code == Code.Ldc_R8 ||
                   code == Code.Ldnull;
        }

        public static Instruction Clone(Instruction instruction)
        {
            switch (instruction.Operand)
            {
                case ParameterDefinition parameterDefinition:
                    return Instruction.Create(instruction.OpCode, parameterDefinition);
                case VariableDefinition variableDefinition:
                    return Instruction.Create(instruction.OpCode, variableDefinition);
                case Instruction[] instructions:
                    var array = new Instruction[instructions.Length];
                    instructions.CopyTo(array, 0);
                    return Instruction.Create(instruction.OpCode, array);
                case Instruction i:
                    return Instruction.Create(instruction.OpCode, i);
                case string str:
                    return Instruction.Create(instruction.OpCode, str);
                case FieldReference fieldReference:
                    return Instruction.Create(instruction.OpCode, fieldReference);
                case MethodReference methodReference:
                    return Instruction.Create(instruction.OpCode, methodReference);
                case CallSite callSite:
                    return Instruction.Create(instruction.OpCode, callSite);
                case TypeReference typeReference:
                    return Instruction.Create(instruction.OpCode, typeReference);
                case int i:
                    return Instruction.Create(instruction.OpCode, i);
                case sbyte sb:
                    return Instruction.Create(instruction.OpCode, sb);
                case byte b:
                    return Instruction.Create(instruction.OpCode, b);
                case long l:
                    return Instruction.Create(instruction.OpCode, l);
                case float f:
                    return Instruction.Create(instruction.OpCode, f);
                case double d:
                    return Instruction.Create(instruction.OpCode, d);
                default:
                    return Instruction.Create(instruction.OpCode);
            }
        }

        public static void ExtendBranchOpCode(Instruction instruction)
        {
            switch (instruction.OpCode.Code)
            {
                case Code.Br_S:
                    instruction.OpCode = OpCodes.Br;
                    break;
                case Code.Brfalse_S:
                    instruction.OpCode = OpCodes.Brfalse;
                    break;
                case Code.Brtrue_S:
                    instruction.OpCode = OpCodes.Brtrue;
                    break;
                case Code.Beq_S:
                    instruction.OpCode = OpCodes.Beq;
                    break;
                case Code.Bge_S:
                    instruction.OpCode = OpCodes.Bge;
                    break;
                case Code.Bgt_S:
                    instruction.OpCode = OpCodes.Bgt;
                    break;
                case Code.Ble_S:
                    instruction.OpCode = OpCodes.Ble;
                    break;
                case Code.Blt_S:
                    instruction.OpCode = OpCodes.Blt;
                    break;
                case Code.Bne_Un_S:
                    instruction.OpCode = OpCodes.Bne_Un;
                    break;
                case Code.Bge_Un_S:
                    instruction.OpCode = OpCodes.Bge_Un;
                    break;
                case Code.Bgt_Un_S:
                    instruction.OpCode = OpCodes.Bgt_Un;
                    break;
                case Code.Ble_Un_S:
                    instruction.OpCode = OpCodes.Ble_Un;
                    break;
                case Code.Blt_Un_S:
                    instruction.OpCode = OpCodes.Blt_Un;
                    break;
                case Code.Leave_S:
                    instruction.OpCode = OpCodes.Leave;
                    break;
            }
        }

        public static bool IsLoadLoc(Instruction instruction)
        {
            var code = instruction.OpCode.Code;
            return code == Code.Ldloc ||
                   code == Code.Ldloc_0 ||
                   code == Code.Ldloc_1 ||
                   code == Code.Ldloc_2 ||
                   code == Code.Ldloc_3 ||
                   code == Code.Ldloc_S;
        }

        public static bool IsLoadLocA(Instruction instruction)
        {
            var code = instruction.OpCode.Code;
            return code == Code.Ldloca ||
                   code == Code.Ldloca_S;
        }

        public static bool IsStoreLoc(Instruction instruction)
        {
            var code = instruction.OpCode.Code;
            return code == Code.Stloc ||
                   code == Code.Stloc_0 ||
                   code == Code.Stloc_1 ||
                   code == Code.Stloc_2 ||
                   code == Code.Stloc_3 ||
                   code == Code.Stloc_S;
        }

        public static bool IsLoadArg(Instruction instruction)
        {
            var code = instruction.OpCode.Code;
            return code == Code.Ldarg ||
                   code == Code.Ldarg_0 ||
                   code == Code.Ldarg_1 ||
                   code == Code.Ldarg_2 ||
                   code == Code.Ldarg_3 ||
                   code == Code.Ldarg_S;
        }

        public static bool IsLoadArgA(Instruction instruction) =>
            instruction.OpCode.Code == Code.Ldarga || instruction.OpCode.Code == Code.Ldarga_S;
        
        public static bool IsStoreArg(Instruction instruction) =>
            instruction.OpCode.Code == Code.Starg || instruction.OpCode.Code == Code.Starg_S;

        public static Instruction CreateVarInstruction(Instruction instruction, VariableDefinition variableDefinition)
        {
            if (IsLoadLoc(instruction))
            {
                return CreateLoadLoc(variableDefinition);
            }

            if (IsStoreLoc(instruction))
            {
                return CreateStoreLoc(variableDefinition);
            }

            if (IsLoadLocA(instruction))
            {
                return CreateLoadLocA(variableDefinition);
            }

            throw new NotSupportedException($"Unknown var instruction {instruction.OpCode}");
        }

        public static IEnumerable<Instruction> GetTargets(Instruction instruction)
        {
            if (instruction.Operand is Instruction opInstruction)
            {
                yield return opInstruction;
            } else if (instruction.Operand is Instruction[] opInstructions)
            {
                foreach (var opInnerInstruction in opInstructions)
                {
                    yield return opInnerInstruction;
                }
            }
        }
    }
}
