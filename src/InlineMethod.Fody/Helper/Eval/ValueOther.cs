namespace InlineMethod.Fody.Helper.Eval;

public abstract class ValueOther(EvalContext evalContext) : Value(evalContext)
{
    public override Value? Add(Value? other) => null;
    public override Value? Sub(Value? other) => null;
    public override Value? Mul(Value? other) => null;
    public override Value? Div(Value? other) => null;
    public override Value? DivUn(Value? other) => null;
    public override Value? Not() => null;
    public override Value? Neg() => null;
    public override Value? Or(Value? other) => null;
    public override Value? Xor(Value? other) => null;
    public override Value? And(Value? other) => null;
    public override Value? Shl(Value? other) => null;
    public override Value? Shr(Value? other) => null;
    public override Value? ShrUn(Value? other) => null;
    public override Value? Ceq(Value? other) => null;
    public override Value? Clt(Value? other) => null;
    public override Value? CltUn(Value? other) => null;
    public override Value? Cgt(Value? other) => null;
    public override Value? CgtUn(Value? other) => null;
    public override ValueI32? ConvI1() => null;
    public override ValueI32? ConvU1() => null;
    public override ValueI32? ConvI2() => null;
    public override ValueI32? ConvU2() => null;
    public override ValueI32? ConvI4() => null;
    public override ValueI32? ConvU4() => null;
    public override ValueI64? ConvI8() => null;
    public override ValueI64? ConvU8() => null;
    public override ValueI32? ConvI() => null;
    public override ValueI32? ConvU() => null;
    public override ValueF32? ConvR4() => null;
    public override ValueF64? ConvR8() => null;
    public override ValueF32? ConvR_Un() => null;
}