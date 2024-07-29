namespace InlineMethod.Fody.Helper.Eval;

public abstract class Value
{
    public abstract int I32Value { get; }
    public abstract uint U32Value { get; }
    public abstract long I64Value { get; }
    public abstract ulong U64Value { get; }
    public abstract float F32Value { get; }
    public abstract double F64Value { get; }
    public abstract bool IsNaN { get; }
    // convert to i8, extends to i32
    public ValueI32 ConvI1() => new((sbyte)I32Value);
    // convert to u8, extends to i32
    public ValueI32 ConvU1() => new((byte)U32Value);
    // convert to i16, extends to i32
    public ValueI32 ConvI2() => new((short)I32Value);
    // convert to u16, extends to i32
    public ValueI32 ConvU2() => new((ushort)U32Value);
    // convert to i32
    public ValueI32 ConvI4() => new(I32Value);
    // convert to u32, extends to i32
    public ValueI32 ConvU4() => new((int)U32Value);
    // convert to i64
    public ValueI64 ConvI8() => new(I64Value);
    // convert to u64, extends to i64
    public ValueI64 ConvU8() => new((long)U64Value);
    // safe if it fits in 32 bit
    public ValueI32? ConvI() => I64Value is >= int.MinValue and <= int.MaxValue ? ConvI4() : null;
    // safe if it fits in 32 bit
    public ValueI32? ConvU() => U64Value <= uint.MaxValue ? ConvU4() : null;
    public ValueF32 ConvR4() => new(I64Value);
    public ValueF64 ConvR8() => new(I64Value);
    public ValueF32 ConvR_Un() => new(U64Value);
    public abstract int CompareTo(Value other);
    public abstract int CompareToUn(Value other);
    public static Value FromNull() => FromI32(0);
    public static ValueI32 FromI32(int value) => new(value);
    public static ValueI32 FromU32(uint value) => new((int)value);
    public static ValueI64 FromI64(long value) => new(value);
    public static ValueI64 FromU64(ulong value) => new((long)value);
    public static ValueF32 FromF32(float value) => new(value);
    public static ValueF64 FromF64(double value) => new(value);
    public bool IsAnyUnordered(Value other) => IsNaN || other.IsNaN;
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
    public bool IsTrue => IsNe(FromI32(0));
    public bool IsFalse => IsEq(FromI32(0));

    public Value? Add(Value? other)
    {
        if (other == null)
            return null;

        if (this is ValueF64 || other is ValueF64)
            return FromF64(F64Value + other.F64Value);

        if (this is ValueF32 || other is ValueF32)
            return FromF32(F32Value + other.F32Value);

        if (this is ValueI64 || other is ValueI64) 
            return FromI64(I64Value + other.I64Value);

        return FromI32(I32Value + other.I32Value);
    }

    public Value? Sub(Value? other)
    {
        if (other == null)
            return null;

        if (this is ValueF64 || other is ValueF64)
            return FromF64(F64Value - other.F64Value);

        if (this is ValueF32 || other is ValueF32)
            return FromF32(F32Value - other.F32Value);

        if (this is ValueI64 || other is ValueI64)
            return FromI64(I64Value - other.I64Value);

        return FromI32(I32Value - other.I32Value);
    }

    public Value? Mul(Value? other)
    {
        if (other == null)
            return null;

        if (this is ValueF64 || other is ValueF64)
            return FromF64(F64Value * other.F64Value);

        if (this is ValueF32 || other is ValueF32)
            return FromF32(F32Value * other.F32Value);

        if (this is ValueI64 || other is ValueI64)
            return FromI64(I64Value * other.I64Value);

        return FromI32(I32Value * other.I32Value);
    }

    public Value? Div(Value? other)
    {
        if (other == null)
            return null;

        if (this is ValueF64 || other is ValueF64)
            return other.F64Value != 0 ? FromF64(F64Value / other.F64Value) : null;

        if (this is ValueF32 || other is ValueF32)
            return other.F32Value != 0 ? FromF32(F32Value / other.F32Value) : null;

        if (this is ValueI64 || other is ValueI64)
            return other.I64Value != 0 ? FromI64(I64Value / other.I64Value) : null;

        return other.I32Value != 0 ? FromI32(I32Value / other.I32Value) : null;
    }

    public Value? DivUn(Value? other)
    {
        if (other == null || this is ValueF64 || other is ValueF64 || this is ValueF32 || other is ValueF32)
            return null;

        if (this is ValueI64 || other is ValueI64)
            return other.U64Value != 0 ? FromU64(U64Value / other.U64Value) : null;

        return other.U32Value != 0 ? FromU32(U32Value / other.U32Value) : null;

    }

    public Value? Not()
    {
        if (this is ValueF64 || this is ValueF32)
            return null;

        return this is ValueI32 ? FromI32(~I32Value) : FromI64(~I64Value);
    }

    public Value Neg()
    {
        return this switch
        {
            ValueF64 => FromF64(-F64Value),
            ValueF32 => FromF32(-F32Value),
            ValueI64 => FromI64(-I64Value),
            _ => FromI32(-I32Value)
        };
    }

    public Value? Or(Value? other)
    {
        if (other == null || this is ValueF64 || other is ValueF64 || this is ValueF32 || other is ValueF32)
            return null;

        if (this is ValueI64 || other is ValueI64)
            return FromU64(U64Value | other.U64Value);

        return FromU32(U32Value | other.U32Value);
    }

    public Value? Xor(Value? other)
    {
        if (other == null || this is ValueF64 || other is ValueF64 || this is ValueF32 || other is ValueF32)
            return null;

        if (this is ValueI64 || other is ValueI64)
            return FromU64(U64Value ^ other.U64Value);

        return FromU32(U32Value ^ other.U32Value);
    }

    public Value? And(Value? other)
    {
        if (other == null || this is ValueF64 || other is ValueF64 || this is ValueF32 || other is ValueF32)
            return null;

        if (this is ValueI64 || other is ValueI64)
            return FromU64(U64Value & other.U64Value);

        return FromU32(U32Value & other.U32Value);
    }

    public Value? Shl(Value? other)
    {
        if (other == null || this is ValueF64 || other is ValueF64 || this is ValueF32 || other is ValueF32)
            return null;

        if (this is ValueI64)
            return FromU64(U64Value << other.I32Value);

        return FromU32(U32Value << other.I32Value);
    }

    public Value? Shr(Value? other)
    {
        if (other == null || this is ValueF64 || other is ValueF64 || this is ValueF32 || other is ValueF32)
            return null;

        if (this is ValueI64)
            return FromI64(I64Value >> other.I32Value);

        return FromI32(I32Value >> other.I32Value);
    }

    public Value? ShrUn(Value? other)
    {
        if (other == null || this is ValueF64 || other is ValueF64 || this is ValueF32 || other is ValueF32)
            return null;

        if (this is ValueI64)
            return FromU64(U64Value >> other.I32Value);

        return FromU32(U32Value >> other.I32Value);
    }

    public Value? Ceq(Value? other) => other != null ? FromI32(IsEq(other) ? 1 : 0) : null;
    public Value? Clt(Value? other) => other != null ? FromI32(IsLt(other) ? 1 : 0) : null;
    public Value? CltUn(Value? other) => other != null ? FromI32(IsLtUn(other) ? 1 : 0) : null;
    public Value? Cgt(Value? other) => other != null ? FromI32(IsGt(other) ? 1 : 0) : null;
    public Value? CgtUn(Value? other) => other != null ? FromI32(IsGtUn(other) ? 1 : 0) : null;
}