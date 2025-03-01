using System;
using System.Collections.Generic;
using InlineMethod.Fody.Extensions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;

namespace InlineMethod.Fody.Helper;

internal static class OpCodeHelper
{
    public static ParameterDefinition? GetArgParameterDefinition(Instruction instruction, MethodParameters parameters)
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

        if (instruction.OpCode.OperandType is OperandType.InlineArg or OperandType.ShortInlineArg)
        {
            return (ParameterDefinition) instruction.Operand;
        }

        return null;
    }

    public static VariableDefinition? GetLocVariableDefinition(Instruction instruction,
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

        if (instruction.OpCode.OperandType is OperandType.InlineVar or OperandType.ShortInlineVar)
        {
            return (VariableDefinition) instruction.Operand;
        }

        return null;
    }

    private static bool IsShortIndex(int index) => index <= short.MaxValue;

    public static Instruction CreateLoadLoc(VariableDefinition variableDefinition)
    {
        return variableDefinition.Index switch
        {
            0 => Instruction.Create(OpCodes.Ldloc_0),
            1 => Instruction.Create(OpCodes.Ldloc_1),
            2 => Instruction.Create(OpCodes.Ldloc_2),
            3 => Instruction.Create(OpCodes.Ldloc_3),
            _ => Instruction.Create(IsShortIndex(variableDefinition.Index) ? OpCodes.Ldloc_S : OpCodes.Ldloc,
                variableDefinition)
        };
    }

    public static Instruction CreateLoadLocA(VariableDefinition variableDefinition)
    {
        return Instruction.Create(IsShortIndex(variableDefinition.Index) ? OpCodes.Ldloca_S : OpCodes.Ldloca,
            variableDefinition);
    }

    public static Instruction CreateStoreLoc(VariableDefinition variableDefinition)
    {
        return variableDefinition.Index switch
        {
            0 => Instruction.Create(OpCodes.Stloc_0),
            1 => Instruction.Create(OpCodes.Stloc_1),
            2 => Instruction.Create(OpCodes.Stloc_2),
            3 => Instruction.Create(OpCodes.Stloc_3),
            _ => Instruction.Create(IsShortIndex(variableDefinition.Index) ? OpCodes.Stloc_S : OpCodes.Stloc,
                variableDefinition)
        };
    }

    public static bool IsLoadConst(Instruction instruction)
    {
        var code = instruction.OpCode.Code;
        return code is Code.Ldc_I4 or Code.Ldc_I4_0 or Code.Ldc_I4_1 or Code.Ldc_I4_2 or Code.Ldc_I4_3 or Code.Ldc_I4_4
            or Code.Ldc_I4_5 or Code.Ldc_I4_6 or Code.Ldc_I4_7 or Code.Ldc_I4_8 or Code.Ldc_I4_M1 or Code.Ldc_I4_S
            or Code.Ldc_I8 or Code.Ldc_R4 or Code.Ldc_R8 or Code.Ldnull;
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

    public static void ExtendVariableOpCode(Instruction instruction)
    {
        if (instruction.OpCode.OperandType == OperandType.ShortInlineVar &&
            instruction.Operand is VariableDefinition {Index: > byte.MaxValue})
        {
            instruction.OpCode = instruction.OpCode.Code switch
            {
                Code.Ldloc_S => OpCodes.Ldloc,
                Code.Ldloca_S => OpCodes.Ldloca,
                Code.Stloc_S => OpCodes.Stloc,
                _ => instruction.OpCode
            };
        }
    }

    public static void ExtendBranchOpCode(Instruction instruction)
    {
        instruction.OpCode = instruction.OpCode.Code switch
        {
            Code.Br_S => OpCodes.Br,
            Code.Brfalse_S => OpCodes.Brfalse,
            Code.Brtrue_S => OpCodes.Brtrue,
            Code.Beq_S => OpCodes.Beq,
            Code.Bge_S => OpCodes.Bge,
            Code.Bgt_S => OpCodes.Bgt,
            Code.Ble_S => OpCodes.Ble,
            Code.Blt_S => OpCodes.Blt,
            Code.Bne_Un_S => OpCodes.Bne_Un,
            Code.Bge_Un_S => OpCodes.Bge_Un,
            Code.Bgt_Un_S => OpCodes.Bgt_Un,
            Code.Ble_Un_S => OpCodes.Ble_Un,
            Code.Blt_Un_S => OpCodes.Blt_Un,
            Code.Leave_S => OpCodes.Leave,
            _ => instruction.OpCode
        };
    }

    public static bool IsLoadLoc(Instruction instruction)
    {
        var code = instruction.OpCode.Code;
        return code is Code.Ldloc or Code.Ldloc_0 or Code.Ldloc_1 or Code.Ldloc_2 or Code.Ldloc_3 or Code.Ldloc_S;
    }

    public static bool IsLoadLocA(Instruction instruction)
    {
        var code = instruction.OpCode.Code;
        return code is Code.Ldloca or Code.Ldloca_S;
    }

    public static bool IsLoadFlda(Instruction instruction) 
        => instruction.OpCode.Code == Code.Ldflda;

    public static bool IsLoadSFld(Instruction instruction)
        => instruction.OpCode.Code == Code.Ldsfld;

    public static bool IsStoreSFld(Instruction instruction)
        => instruction.OpCode.Code == Code.Stsfld;

    public static bool IsStoreLoc(Instruction instruction)
    {
        var code = instruction.OpCode.Code;
        return code is Code.Stloc or Code.Stloc_0 or Code.Stloc_1 or Code.Stloc_2 or Code.Stloc_3 or Code.Stloc_S;
    }

    public static bool IsLoadArg(Instruction instruction)
    {
        var code = instruction.OpCode.Code;
        return code is Code.Ldarg or Code.Ldarg_0 or Code.Ldarg_1 or Code.Ldarg_2 or Code.Ldarg_3 or Code.Ldarg_S;
    }

    public static bool IsConv(Instruction instruction)
    {
        var code = instruction.OpCode.Code;
        return code is Code.Conv_I or Code.Conv_I1 or Code.Conv_I2 or Code.Conv_I4 or Code.Conv_I8 or Code.Conv_U
            or Code.Conv_U1 or Code.Conv_U2 or Code.Conv_U4 or Code.Conv_U8 or Code.Conv_R4 or Code.Conv_R8
            or Code.Conv_R_Un or Code.Conv_Ovf_I1_Un or Code.Conv_Ovf_I2_Un or Code.Conv_Ovf_I4_Un
            or Code.Conv_Ovf_I8_Un or Code.Conv_Ovf_U1_Un or Code.Conv_Ovf_U2_Un or Code.Conv_Ovf_U4_Un
            or Code.Conv_Ovf_U8_Un or Code.Conv_Ovf_I_Un or Code.Conv_Ovf_U_Un or Code.Conv_Ovf_I1 or Code.Conv_Ovf_I2
            or Code.Conv_Ovf_I4 or Code.Conv_Ovf_I8 or Code.Conv_Ovf_U1 or Code.Conv_Ovf_U2 or Code.Conv_Ovf_U4
            or Code.Conv_Ovf_U8 or Code.Conv_Ovf_I or Code.Conv_Ovf_U;
    }

    public static bool IsLoadArgA(Instruction instruction) =>
        instruction.OpCode.Code is Code.Ldarga or Code.Ldarga_S;
        
    public static bool IsStoreArg(Instruction instruction) =>
        instruction.OpCode.Code is Code.Starg or Code.Starg_S;

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
        switch (instruction.Operand)
        {
            case Instruction opInstruction:
                yield return opInstruction;
                break;
            case Instruction[] opInstructions:
            {
                foreach (var opInnerInstruction in opInstructions)
                {
                    yield return opInnerInstruction;
                }

                break;
            }
        }
    }

    public static bool HasTargets(Instruction instruction) => instruction.Operand is Instruction or Instruction[];

    public static void ReplaceInstruction(Instruction target, Instruction source)
    {
        target.OpCode = source.OpCode;
        target.Operand = source.Operand;
    }

    public static bool IsSizeOf(Instruction instruction)
        => instruction.OpCode.Code == Code.Sizeof;

    public static bool IsLoadFld(Instruction instruction)
        => instruction.OpCode.Code == Code.Ldfld;

    public static bool IsConditionalBranch(Instruction instruction)
        => instruction.OpCode.FlowControl == FlowControl.Cond_Branch;

    public static OpCode ConvertUnaryConditional(Instruction instruction, Instruction op) =>
        instruction.OpCode.Code switch
        {
            Code.Brtrue => op.OpCode.Code switch
            {
                Code.Ceq => OpCodes.Beq,
                Code.Clt => OpCodes.Blt,
                Code.Clt_Un => OpCodes.Blt_Un,
                Code.Cgt => OpCodes.Bgt,
                Code.Cgt_Un => OpCodes.Bgt_Un,
                _ => throw new NotSupportedException(),
            },
            Code.Brtrue_S => op.OpCode.Code switch
            {
                Code.Ceq => OpCodes.Beq_S,
                Code.Clt => OpCodes.Blt_S,
                Code.Clt_Un => OpCodes.Blt_Un_S,
                Code.Cgt => OpCodes.Bgt_S,
                Code.Cgt_Un => OpCodes.Bgt_Un_S,
                _ => throw new NotSupportedException(),
            },
            Code.Brfalse => op.OpCode.Code switch
            {
                Code.Ceq => OpCodes.Bne_Un,
                Code.Clt => OpCodes.Bge,
                Code.Clt_Un => OpCodes.Bge_Un,
                Code.Cgt => OpCodes.Ble,
                Code.Cgt_Un => OpCodes.Ble_Un,
                _ => throw new NotSupportedException(),
            },
            Code.Brfalse_S => op.OpCode.Code switch
            {
                Code.Ceq => OpCodes.Bne_Un_S,
                Code.Clt => OpCodes.Bge_S,
                Code.Clt_Un => OpCodes.Bge_Un_S,
                Code.Cgt => OpCodes.Ble_S,
                Code.Cgt_Un => OpCodes.Ble_Un_S,
                _ => throw new NotSupportedException(),
            },
            _ => throw new NotSupportedException(),
        };

    public static bool HasSideEffects(Instruction instruction)
    {
        if (
            // const
            OpCodeHelper.IsLoadConst(instruction) ||
            // conv
            OpCodeHelper.IsConv(instruction) ||
            // load arg/var
            OpCodeHelper.IsLoadArg(instruction) || OpCodeHelper.IsLoadLoc(instruction) ||
            // load addr of arg/var
            OpCodeHelper.IsLoadArgA(instruction) || OpCodeHelper.IsLoadLocA(instruction) ||
            // branches
            OpCodeHelper.IsConditionalBranch(instruction) || instruction.OpCode.FlowControl == FlowControl.Branch ||
            // eval
            instruction.OpCode.Code is Code.Dup or Code.Pop or Code.Ldftn or Code.Add or Code.Sub or Code.Mul
                or Code.Div
                or Code.Div_Un or Code.Neg or Code.Not or Code.Or or Code.And or Code.Xor or Code.Shl or Code.Shr
                or Code.Shr_Un or Code.Ceq or Code.Clt or Code.Clt_Un or Code.Cgt or Code.Cgt_Un
           )
        {
            return false;
        }

        // load/store static fields (CompilerGenerated + Delegate)
        if (
            (OpCodeHelper.IsLoadSFld(instruction) || OpCodeHelper.IsStoreSFld(instruction)) &&
            instruction.Operand is FieldReference fieldReference)
        {
            var declaringType = fieldReference.DeclaringType.Resolve();
            if (declaringType.IsSealed && TypeHelper.IsCompilerGenerated(declaringType))
            {
                var fieldType = fieldReference.FieldType.Resolve();
                if (TypeHelper.IsDelegateType(fieldType) || TypeHelper.IsCompilerGenerated(fieldType))
                {
                    return false;
                }
            }
        }

        // new Delegate()
        if (instruction.OpCode.Code == Code.Newobj && instruction.Operand is MethodReference method &&
            TypeHelper.IsDelegateType(method.DeclaringType))
        {
            return false;
        }

        return true;
    }
}
