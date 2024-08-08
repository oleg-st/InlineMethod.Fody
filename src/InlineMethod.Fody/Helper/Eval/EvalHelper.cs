using System;
using System.Collections.Generic;
using InlineMethod.Fody.Extensions;
using Mono.Cecil.Cil;

namespace InlineMethod.Fody.Helper.Eval;

internal static class EvalHelper
{
    private class OpHelper(Instruction instruction, Trackers trackers, HashSet<Instruction> targets)
    {
        // unary
        private readonly Lazy<Instruction?> _opSingle = new(instruction.GetSinglePushInstruction);
        public Value? Single() => Eval(_opSingle.Value, trackers, targets);

        // binary
        private readonly Lazy<(Instruction?, Instruction?)> _opTwo = new(instruction.GetTwoPushInstructions);
        public Value? First() => Eval(_opTwo.Value.Item1, trackers, targets);
        public Value? Second() => Eval(_opTwo.Value.Item2, trackers, targets);
    }

    // Eval const expression
    public static Value? Eval(Instruction? instruction, Trackers trackers, HashSet<Instruction> targets)
    {
        if (instruction == null)
        {
            return null;
        }

        var op = new OpHelper(instruction, trackers, targets);
        var tracker = trackers.GetTracker(instruction);
        if (tracker != null && tracker.IsLoad(instruction) && tracker.StoreInstruction != null)
        {
            if (!OpCodeHelper.IsSingleFlow(tracker.StoreInstruction, targets))
            {
                return null;
            }

            return new OpHelper(tracker.StoreInstruction, trackers, targets).Single();
        }

        return instruction.OpCode.Code switch
        {
            // arithmetic
            Code.Add => op.First()?.Add(op.Second()),
            Code.Sub => op.First()?.Sub(op.Second()),
            Code.Mul => op.First()?.Mul(op.Second()),
            Code.Div => op.First()?.Div(op.Second()),
            Code.Div_Un => op.First()?.DivUn(op.Second()),
            Code.Neg => op.Single()?.Neg(),
            // bitwise
            Code.Not => op.Single()?.Not(),
            Code.Or => op.First()?.Or(op.Second()),
            Code.And => op.First()?.And(op.Second()),
            Code.Xor => op.First()?.Xor(op.Second()),
            Code.Shl => op.First()?.Shl(op.Second()),
            Code.Shr => op.First()?.Shr(op.Second()),
            Code.Shr_Un => op.First()?.ShrUn(op.Second()),
            // conditional
            Code.Ceq => op.First()?.Ceq(op.Second()),
            Code.Clt => op.First()?.Clt(op.Second()),
            Code.Clt_Un => op.First()?.CltUn(op.Second()),
            Code.Cgt => op.First()?.Cgt(op.Second()),
            Code.Cgt_Un => op.First()?.CgtUn(op.Second()),
            // conv
            Code.Conv_I1 => op.Single()?.ConvI1(),
            Code.Conv_I2 => op.Single()?.ConvI2(),
            Code.Conv_I4 => op.Single()?.ConvI4(),
            Code.Conv_I8 => op.Single()?.ConvI8(),
            Code.Conv_U1 => op.Single()?.ConvU1(),
            Code.Conv_U2 => op.Single()?.ConvU2(),
            Code.Conv_U4 => op.Single()?.ConvU4(),
            Code.Conv_U8 => op.Single()?.ConvU8(),
            Code.Conv_I => op.Single()?.ConvI(),
            Code.Conv_U => op.Single()?.ConvU(),
            Code.Conv_R_Un => op.Single()?.ConvR_Un(),
            Code.Conv_R4 => op.Single()?.ConvR4(),
            Code.Conv_R8 => op.Single()?.ConvR8(),
            // dup
            Code.Dup => op.Single(),
            // const
            Code.Ldc_I4 or Code.Ldc_I4_S or Code.Ldc_I8 or Code.Ldc_R4 or Code.Ldc_R8 => instruction.Operand switch
            {
                // I4
                int v => Value.FromI32(v),
                // I4_S
                sbyte v => Value.FromI32(v),
                // I8
                long v => Value.FromI64(v),
                // R4
                float v => Value.FromF32(v),
                // R8
                double v => Value.FromF64(v),
                _ => throw new ArgumentException($"Unknown const {instruction.Operand.GetType()}")
            },
            Code.Ldc_I4_0 => Value.FromI32(0),
            Code.Ldc_I4_1 => Value.FromI32(1),
            Code.Ldc_I4_2 => Value.FromI32(2),
            Code.Ldc_I4_3 => Value.FromI32(3),
            Code.Ldc_I4_4 => Value.FromI32(4),
            Code.Ldc_I4_5 => Value.FromI32(5),
            Code.Ldc_I4_6 => Value.FromI32(6),
            Code.Ldc_I4_7 => Value.FromI32(7),
            Code.Ldc_I4_8 => Value.FromI32(8),
            Code.Ldc_I4_M1 => Value.FromI32(-1),
            Code.Ldnull => Value.FromNull(),
            _ => null
        };
    }

    public static bool IsBinaryCondition(Instruction instruction, Value op1, Value op2)
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

    public static bool IsUnaryCondition(Instruction instruction, Value op)
    {
        return instruction.OpCode.Code switch
        {
            Code.Brfalse or Code.Brfalse_S => op.IsFalse,
            Code.Brtrue or Code.Brtrue_S => op.IsTrue,
            _ => throw new NotImplementedException()
        };
    }
}