using System;

namespace InlineMethod.Fody.Helper.Eval;

public class ValueI32(int value) : Value
{
    public override int I32Value => Value;
    public override uint U32Value => (uint)Value;
    public override long I64Value => Value;
    public override ulong U64Value => U32Value;
    public override float F32Value => Value;
    public override double F64Value => Value;
    public override bool IsNaN => false;
    public int Value => value;
    public override int CompareTo(Value other) =>
        other switch
        {
            ValueI32 valueI32 => I32Value.CompareTo(valueI32.I32Value),
            ValueI64 valueI64 => I64Value.CompareTo(valueI64.I64Value),
            ValueF32 valueF32 => F32Value.CompareTo(valueF32.F32Value),
            ValueF64 valueF64 => F64Value.CompareTo(valueF64.F64Value),
            _ => throw new ArgumentException("Unknown value")
        };

    public override int CompareToUn(Value other) =>
        other switch
        {
            ValueI32 valueI32 => U32Value.CompareTo(valueI32.U32Value),
            ValueI64 valueI64 => U64Value.CompareTo(valueI64.U64Value),
            ValueF32 valueF32 => F32Value.CompareTo(valueF32.F32Value),
            ValueF64 valueF64 => F64Value.CompareTo(valueF64.F64Value),
            _ => throw new ArgumentException("Unknown value")
        };
}