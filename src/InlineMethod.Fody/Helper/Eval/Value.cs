using System;

namespace InlineMethod.Fody.Helper.Eval;

public abstract class Value(EvalContext evalContext) : IEquatable<Value>
{
    public EvalContext EvalContext => evalContext;
    // can be safely removed
    public abstract bool Removable { get; }
    // convert to i8, extends to i32
    public abstract ValueI32? ConvI1();
    // convert to u8, extends to i32
    public abstract ValueI32? ConvU1();
    // convert to i16, extends to i32
    public abstract ValueI32? ConvI2();
    // convert to u16, extends to i32
    public abstract ValueI32? ConvU2();
    // convert to i32
    public abstract ValueI32? ConvI4();
    // convert to u32, extends to i32
    public abstract ValueI32? ConvU4();
    // convert to i64
    public abstract ValueI64? ConvI8();
    // convert to u64, extends to i64
    public abstract ValueI64? ConvU8();
    // safe if it fits in 32 bit
    public abstract ValueI32? ConvI();
    // safe if it fits in 32 bit
    public abstract ValueI32? ConvU();
    public abstract ValueF32? ConvR4();
    public abstract ValueF64? ConvR8();
    public abstract ValueF32? ConvR_Un();
    public abstract bool Equals(Value other);
    public abstract Value? Add(Value? other);
    public abstract Value? Sub(Value? other);
    public abstract Value? Mul(Value? other);
    public abstract Value? Div(Value? other);
    public abstract Value? DivUn(Value? other);
    public abstract Value? Not();
    public abstract Value? Neg();
    public abstract Value? Or(Value? other);
    public abstract Value? Xor(Value? other);
    public abstract Value? And(Value? other);
    public abstract Value? Shl(Value? other);
    public abstract Value? Shr(Value? other);
    public abstract Value? ShrUn(Value? other);
    public abstract Value? Ceq(Value? other);
    public abstract Value? Clt(Value? other);
    public abstract Value? CltUn(Value? other);
    public abstract Value? Cgt(Value? other);
    public abstract Value? CgtUn(Value? other);
}