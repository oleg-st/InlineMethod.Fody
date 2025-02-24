using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace InlineMethod.Fody.Helper.Eval;

internal static class EvalHelper
{
    public static Value? GetTrackerValue(Tracker tracker, EvalContext evalContext)
    {
        if (evalContext.GetTrackerValue(tracker, out var trackerValue))
        {
            return trackerValue;
        }

        evalContext.StartTracker(tracker);
        var instructionHelpers = tracker.GetInstructionHelpers(evalContext);
        if (instructionHelpers.Count == 0 ||
            !instructionHelpers.All(instructionHelper => instructionHelper.IsEvaluable()))
        {
            return null;
        }

        var firstValue = instructionHelpers.First().EvalFirst();
        if (firstValue == null || !instructionHelpers.Skip(1).All(instructionHelper =>
            {
                var value = instructionHelper.EvalFirst();
                return value != null && firstValue.Equals(value);
            }))
        {
            return null;
        }

        evalContext.FinishTracker(tracker, firstValue);
        return firstValue;
    }

    // Eval const expression
    public static Value? Eval(Context context, EvalContext evalContext, Instruction? instruction)
    {
        if (instruction == null)
        {
            return null;
        }

        var tracker = context.GetTracker(instruction);
        if (tracker != null && tracker.IsLoad(instruction))
        {
            return GetTrackerValue(tracker, evalContext);
        }

        var op = new InstructionHelper(context, evalContext, instruction);
        return instruction.OpCode.Code switch
        {
            // new
            Code.Newobj => instruction.Operand is MethodReference method
                ? new ValueNewObject(evalContext, method.DeclaringType, op.EvalAll().ToArray(), op)
                : null,
            // method
            Code.Ldftn => instruction.Operand is MethodReference method ? new ValueMethod(evalContext, method) : null,
            // arithmetic
            Code.Add => op.EvalFirst()?.Add(op.EvalSecond()),
            Code.Sub => op.EvalFirst()?.Sub(op.EvalSecond()),
            Code.Mul => op.EvalFirst()?.Mul(op.EvalSecond()),
            Code.Div => op.EvalFirst()?.Div(op.EvalSecond()),
            Code.Div_Un => op.EvalFirst()?.DivUn(op.EvalSecond()),
            Code.Neg => op.EvalFirst()?.Neg(),
            // bitwise
            Code.Not => op.EvalFirst()?.Not(),
            Code.Or => op.EvalFirst()?.Or(op.EvalSecond()),
            Code.And => op.EvalFirst()?.And(op.EvalSecond()),
            Code.Xor => op.EvalFirst()?.Xor(op.EvalSecond()),
            Code.Shl => op.EvalFirst()?.Shl(op.EvalSecond()),
            Code.Shr => op.EvalFirst()?.Shr(op.EvalSecond()),
            Code.Shr_Un => op.EvalFirst()?.ShrUn(op.EvalSecond()),
            // conditional
            Code.Ceq => op.EvalFirst()?.Ceq(op.EvalSecond()),
            Code.Clt => op.EvalFirst()?.Clt(op.EvalSecond()),
            Code.Clt_Un => op.EvalFirst()?.CltUn(op.EvalSecond()),
            Code.Cgt => op.EvalFirst()?.Cgt(op.EvalSecond()),
            Code.Cgt_Un => op.EvalFirst()?.CgtUn(op.EvalSecond()),
            // conv
            Code.Conv_I1 => op.EvalFirst()?.ConvI1(),
            Code.Conv_I2 => op.EvalFirst()?.ConvI2(),
            Code.Conv_I4 => op.EvalFirst()?.ConvI4(),
            Code.Conv_I8 => op.EvalFirst()?.ConvI8(),
            Code.Conv_U1 => op.EvalFirst()?.ConvU1(),
            Code.Conv_U2 => op.EvalFirst()?.ConvU2(),
            Code.Conv_U4 => op.EvalFirst()?.ConvU4(),
            Code.Conv_U8 => op.EvalFirst()?.ConvU8(),
            Code.Conv_I => op.EvalFirst()?.ConvI(),
            Code.Conv_U => op.EvalFirst()?.ConvU(),
            Code.Conv_R_Un => op.EvalFirst()?.ConvR_Un(),
            Code.Conv_R4 => op.EvalFirst()?.ConvR4(),
            Code.Conv_R8 => op.EvalFirst()?.ConvR8(),
            // dup
            Code.Dup => op.EvalFirst(),
            // const
            Code.Ldc_I4 or Code.Ldc_I4_S or Code.Ldc_I8 or Code.Ldc_R4 or Code.Ldc_R8 => instruction.Operand switch
            {
                // I4
                int v => evalContext.FromI32(v),
                // I4_S
                sbyte v => evalContext.FromI32(v),
                // I8
                long v => evalContext.FromI64(v),
                // R4
                float v => evalContext.FromF32(v),
                // R8
                double v => evalContext.FromF64(v),
                _ => throw new ArgumentException($"Unknown const {instruction.Operand.GetType()}")
            },
            Code.Ldc_I4_0 => evalContext.FromI32(0),
            Code.Ldc_I4_1 => evalContext.FromI32(1),
            Code.Ldc_I4_2 => evalContext.FromI32(2),
            Code.Ldc_I4_3 => evalContext.FromI32(3),
            Code.Ldc_I4_4 => evalContext.FromI32(4),
            Code.Ldc_I4_5 => evalContext.FromI32(5),
            Code.Ldc_I4_6 => evalContext.FromI32(6),
            Code.Ldc_I4_7 => evalContext.FromI32(7),
            Code.Ldc_I4_8 => evalContext.FromI32(8),
            Code.Ldc_I4_M1 => evalContext.FromI32(-1),
            Code.Ldnull => evalContext.FromNull(),
            _ => null
        };
    }

    public static bool IsBinaryCondition(Instruction instruction, ValueNumber op1, ValueNumber op2)
    {
        return instruction.OpCode.Code switch
        {
            Code.Beq or Code.Beq_S => op1.IsEq(op2),
            Code.Bne_Un or Code.Bne_Un_S => op1.IsNeUn(op2),
            Code.Bge or Code.Bge_S => op1.IsGe(op2),
            Code.Bge_Un or Code.Bge_Un_S => op1.IsGeUn(op2),
            Code.Ble or Code.Ble_S => op1.IsLe(op2),
            Code.Ble_Un or Code.Ble_Un_S => op1.IsLeUn(op2),
            Code.Bgt or Code.Bgt_S => op1.IsGt(op2),
            Code.Bgt_Un or Code.Bgt_Un_S => op1.IsGtUn(op2),
            Code.Blt or Code.Blt_S => op1.IsLt(op2),
            Code.Blt_Un or Code.Blt_Un_S => op1.IsLtUn(op2),
            _ => throw new NotImplementedException()
        };
    }

    public static bool IsUnaryCondition(Instruction instruction, ValueNumber op)
    {
        return instruction.OpCode.Code switch
        {
            Code.Brfalse or Code.Brfalse_S => op.IsFalse,
            Code.Brtrue or Code.Brtrue_S => op.IsTrue,
            _ => throw new NotImplementedException()
        };
    }
}