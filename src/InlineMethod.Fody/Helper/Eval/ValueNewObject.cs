using Mono.Cecil;
using System.Linq;

namespace InlineMethod.Fody.Helper.Eval;

public class ValueNewObject(EvalContext evalContext, TypeReference typeReference, Value?[] arguments, InstructionHelper instructionHelper) : ValueOther(evalContext)
{
    // if new Delegate(..., ...)
    public override bool Removable => TypeHelper.IsDelegateType(Type);
    public TypeReference Type => typeReference;
    public Value?[] Arguments => arguments;
    public InstructionHelper InstructionHelper => instructionHelper;
    public override bool Equals(Value other) => other is ValueNewObject v && Type.Equals(v.Type) && Arguments.All(a => a != null) && Arguments.SequenceEqual(v.Arguments);
}
