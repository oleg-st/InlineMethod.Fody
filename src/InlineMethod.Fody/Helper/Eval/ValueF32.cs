using System;

namespace InlineMethod.Fody.Helper.Eval;

public class ValueF32(EvalContext evalContext, float value) : ValueNumber(evalContext)
{
    public override int I32Value => (int)Value;
    public override uint U32Value => (uint)Value;
    public override long I64Value => (long)Value;
    public override ulong U64Value => (ulong)Value;
    public override float F32Value => Value;
    public override double F64Value => Value;
    public override bool IsNaN => float.IsNaN(Value);
    public float Value => value;
    public override int CompareTo(Value other) =>
        other switch
        {
            ValueI32 value32 => F32Value.CompareTo(value32.F32Value),
            ValueI64 value64 => F32Value.CompareTo(value64.F32Value),
            ValueF32 valueF32 => F32Value.CompareTo(valueF32.F32Value),
            ValueF64 valueF64 => F64Value.CompareTo(valueF64.F64Value),
            _ => throw new ArgumentException("Unknown value")
        };

    public override int CompareToUn(Value other) => CompareTo(other);
    public override bool Equals(Value other) => other is ValueF32 v && Value.Equals(v.Value);
}