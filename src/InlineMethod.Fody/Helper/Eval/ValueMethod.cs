using Mono.Cecil;

namespace InlineMethod.Fody.Helper.Eval;

public class ValueMethod(EvalContext evalContext, MethodReference method) : ValueOther(evalContext)
{
    public override bool Removable => true;
    public MethodReference Method => method;
    public override bool Equals(Value other) => other is ValueMethod v && Method.Equals(v.Method);
}
