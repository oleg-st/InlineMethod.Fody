namespace InlineMethod.Fody.Helper.Eval;

public abstract class ValueNumber(EvalContext evalContext) : Value(evalContext)
{
    public override bool Removable => true;
    public abstract int I32Value { get; }
    public abstract uint U32Value { get; }
    public abstract long I64Value { get; }
    public abstract ulong U64Value { get; }
    public abstract float F32Value { get; }
    public abstract double F64Value { get; }
    public abstract bool IsNaN { get; }
    // convert to i8, extends to i32
    public override ValueI32 ConvI1() => new(EvalContext, (sbyte)I32Value);
    // convert to u8, extends to i32
    public override ValueI32 ConvU1() => new(EvalContext, (byte)U32Value);
    // convert to i16, extends to i32
    public override ValueI32 ConvI2() => new(EvalContext, (short)I32Value);
    // convert to u16, extends to i32
    public override ValueI32 ConvU2() => new(EvalContext, (ushort)U32Value);
    // convert to i32
    public override ValueI32 ConvI4() => new(EvalContext, I32Value);
    // convert to u32, extends to i32
    public override ValueI32 ConvU4() => new(EvalContext, (int)U32Value);
    // convert to i64
    public override ValueI64 ConvI8() => new(EvalContext, I64Value);
    // convert to u64, extends to i64
    public override ValueI64 ConvU8() => new(EvalContext, (long)U64Value);
    // safe if it fits in 32 bit
    public override ValueI32? ConvI() => I64Value is >= int.MinValue and <= int.MaxValue ? ConvI4() : null;
    // safe if it fits in 32 bit
    public override ValueI32? ConvU() => U64Value <= uint.MaxValue ? ConvU4() : null;
    public override ValueF32 ConvR4() => new(EvalContext, I64Value);
    public override ValueF64 ConvR8() => new(EvalContext, I64Value);
    public override ValueF32 ConvR_Un() => new(EvalContext, U64Value);
    public bool IsAnyUnordered(Value other) => IsNaN || other is ValueNumber {IsNaN: true};
    public abstract int CompareTo(Value other);
    public abstract int CompareToUn(Value other);
    public bool IsEq(Value other) => !IsAnyUnordered(other) && CompareTo(other) == 0;
    public bool IsLt(Value other) => !IsAnyUnordered(other) && CompareTo(other) < 0;
    public bool IsGt(Value other) => !IsAnyUnordered(other) && CompareTo(other) > 0;
    public bool IsLe(Value other) => !IsAnyUnordered(other) && CompareTo(other) <= 0;
    public bool IsGe(Value other) => !IsAnyUnordered(other) && CompareTo(other) >= 0;
    public bool IsNe(Value other) => !IsAnyUnordered(other) && CompareTo(other) != 0;
    public bool IsEqUn(Value other) => IsAnyUnordered(other) || CompareToUn(other) == 0;
    public bool IsLtUn(Value other) => IsAnyUnordered(other) || CompareToUn(other) < 0;
    public bool IsGtUn(Value other) => IsAnyUnordered(other) || CompareToUn(other) > 0;
    public bool IsLeUn(Value other) => IsAnyUnordered(other) || CompareToUn(other) <= 0;
    public bool IsGeUn(Value other) => IsAnyUnordered(other) || CompareToUn(other) >= 0;
    public bool IsNeUn(Value other) => IsAnyUnordered(other) || CompareToUn(other) != 0;
    public bool IsTrue => IsNe(EvalContext.FromI32(0));
    public bool IsFalse => IsEq(EvalContext.FromI32(0));

    public override Value? Add(Value? other)
    {
        if (other is not ValueNumber otherNumber)
            return null;

        if (this is ValueF64 || other is ValueF64)
            return EvalContext.FromF64(F64Value + otherNumber.F64Value);

        if (this is ValueF32 || other is ValueF32)
            return EvalContext.FromF32(F32Value + otherNumber.F32Value);

        if (this is ValueI64 || other is ValueI64)
            return EvalContext.FromI64(I64Value + otherNumber.I64Value);

        return EvalContext.FromI32(I32Value + otherNumber.I32Value);
    }

    public override Value? Sub(Value? other)
    {
        if (other is not ValueNumber otherNumber)
            return null;

        if (this is ValueF64 || other is ValueF64)
            return EvalContext.FromF64(F64Value - otherNumber.F64Value);

        if (this is ValueF32 || other is ValueF32)
            return EvalContext.FromF32(F32Value - otherNumber.F32Value);

        if (this is ValueI64 || other is ValueI64)
            return EvalContext.FromI64(I64Value - otherNumber.I64Value);

        return EvalContext.FromI32(I32Value - otherNumber.I32Value);
    }

    public override Value? Mul(Value? other)
    {
        if (other is not ValueNumber otherNumber)
            return null;

        if (this is ValueF64 || other is ValueF64)
            return EvalContext.FromF64(F64Value * otherNumber.F64Value);

        if (this is ValueF32 || other is ValueF32)
            return EvalContext.FromF32(F32Value * otherNumber.F32Value);

        if (this is ValueI64 || other is ValueI64)
            return EvalContext.FromI64(I64Value * otherNumber.I64Value);

        return EvalContext.FromI32(I32Value * otherNumber.I32Value);
    }

    public override Value? Div(Value? other)
    {
        if (other is not ValueNumber otherNumber)
            return null;

        if (this is ValueF64 || other is ValueF64)
            return otherNumber.F64Value != 0 ? EvalContext.FromF64(F64Value / otherNumber.F64Value) : null;

        if (this is ValueF32 || other is ValueF32)
            return otherNumber.F32Value != 0 ? EvalContext.FromF32(F32Value / otherNumber.F32Value) : null;

        if (this is ValueI64 || other is ValueI64)
            return otherNumber.I64Value != 0 ? EvalContext.FromI64(I64Value / otherNumber.I64Value) : null;

        return otherNumber.I32Value != 0 ? EvalContext.FromI32(I32Value / otherNumber.I32Value) : null;
    }

    public override Value? DivUn(Value? other)
    {
        if (other is not ValueNumber otherNumber || this is ValueF64 || other is ValueF64 || this is ValueF32 || other is ValueF32)
            return null;

        if (this is ValueI64 || other is ValueI64)
            return otherNumber.U64Value != 0 ? EvalContext.FromU64(U64Value / otherNumber.U64Value) : null;

        return otherNumber.U32Value != 0 ? EvalContext.FromU32(U32Value / otherNumber.U32Value) : null;
    }

    public override Value? Not()
    {
        if (this is ValueF64 || this is ValueF32)
            return null;

        return this is ValueI32 ? EvalContext.FromI32(~I32Value) : EvalContext.FromI64(~I64Value);
    }

    public override Value Neg() =>
        this switch
        {
            ValueF64 => EvalContext.FromF64(-F64Value),
            ValueF32 => EvalContext.FromF32(-F32Value),
            ValueI64 => EvalContext.FromI64(-I64Value),
            _ => EvalContext.FromI32(-I32Value)
        };

    public override Value? Or(Value? other)
    {
        if (other is not ValueNumber otherNumber || this is ValueF64 || other is ValueF64 || this is ValueF32 || other is ValueF32)
            return null;

        if (this is ValueI64 || other is ValueI64)
            return EvalContext.FromU64(U64Value | otherNumber.U64Value);

        return EvalContext.FromU32(U32Value | otherNumber.U32Value);
    }

    public override Value? Xor(Value? other)
    {
        if (other is not ValueNumber otherNumber || this is ValueF64 || other is ValueF64 || this is ValueF32 || other is ValueF32)
            return null;

        if (this is ValueI64 || other is ValueI64)
            return EvalContext.FromU64(U64Value ^ otherNumber.U64Value);

        return EvalContext.FromU32(U32Value ^ otherNumber.U32Value);
    }

    public override Value? And(Value? other)
    {
        if (other is not ValueNumber otherNumber || this is ValueF64 || other is ValueF64 || this is ValueF32 || other is ValueF32)
            return null;

        if (this is ValueI64 || other is ValueI64)
            return EvalContext.FromU64(U64Value & otherNumber.U64Value);

        return EvalContext.FromU32(U32Value & otherNumber.U32Value);
    }

    public override Value? Shl(Value? other)
    {
        if (other is not ValueNumber otherNumber || this is ValueF64 || other is ValueF64 || this is ValueF32 || other is ValueF32)
            return null;

        if (this is ValueI64)
            return EvalContext.FromU64(U64Value << otherNumber.I32Value);

        return EvalContext.FromU32(U32Value << otherNumber.I32Value);
    }

    public override Value? Shr(Value? other)
    {
        if (other is not ValueNumber otherNumber || this is ValueF64 || other is ValueF64 || this is ValueF32 || other is ValueF32)
            return null;

        if (this is ValueI64)
            return EvalContext.FromI64(I64Value >> otherNumber.I32Value);

        return EvalContext.FromI32(I32Value >> otherNumber.I32Value);
    }

    public override Value? ShrUn(Value? other)
    {
        if (other is not ValueNumber otherNumber || this is ValueF64 || other is ValueF64 || this is ValueF32 || other is ValueF32)
            return null;

        if (this is ValueI64)
            return EvalContext.FromU64(U64Value >> otherNumber.I32Value);

        return EvalContext.FromU32(U32Value >> otherNumber.I32Value);
    }

    public override Value? Ceq(Value? other) => other != null ? EvalContext.FromI32(IsEq(other) ? 1 : 0) : null;
    public override Value? Clt(Value? other) => other != null ? EvalContext.FromI32(IsLt(other) ? 1 : 0) : null;
    public override Value? CltUn(Value? other) => other != null ? EvalContext.FromI32(IsLtUn(other) ? 1 : 0) : null;
    public override Value? Cgt(Value? other) => other != null ? EvalContext.FromI32(IsGt(other) ? 1 : 0) : null;
    public override Value? CgtUn(Value? other) => other != null ? EvalContext.FromI32(IsGtUn(other) ? 1 : 0) : null;
}