using System;

namespace InlineMethod.Fody.Helper.Eval;

public class ValueF64(EvalContext evalContext, double value) : ValueNumber(evalContext)
{
    public override int I32Value => (int)Value;
    public override uint U32Value => (uint)Value;
    public override long I64Value => (long)Value;
    public override ulong U64Value => (ulong)Value;
    public override float F32Value => (float)Value;
    public override double F64Value => Value;
    public override bool IsNaN => double.IsNaN(Value);
    public double Value => value;
    public override int CompareTo(Value other) =>
        other switch
        {
            ValueI32 value32 => F64Value.CompareTo(value32.F64Value),
            ValueI64 value64 => F64Value.CompareTo(value64.F64Value),
            ValueF32 valueF32 => F64Value.CompareTo(valueF32.F64Value),
            ValueF64 valueF64 => F64Value.CompareTo(valueF64.F64Value),
            _ => throw new ArgumentException("Unknown value")
        };

    public override int CompareToUn(Value other) => CompareTo(other);

    public override bool Equals(Value other) => other is ValueF64 v && Value.Equals(v.Value);
}