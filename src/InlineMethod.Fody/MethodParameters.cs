using Mono.Cecil;

namespace InlineMethod.Fody;

public class MethodParameters
{
    private readonly MethodDefinition _methodDefinition;
    private readonly bool _hasImplicitThis;
    public int Count { get; }

    public MethodParameters(MethodDefinition methodDefinition)
    {
        _methodDefinition = methodDefinition;
        _hasImplicitThis = _methodDefinition is {HasThis: true, ExplicitThis: false};
        Count = _methodDefinition.Parameters.Count + (_hasImplicitThis ? 1 : 0);
    }

    public ParameterDefinition this[int index]
    {
        get
        {
            if (_hasImplicitThis)
            {
                if (index == 0)
                {
                    return _methodDefinition.Body.ThisParameter;
                }

                index--;
            }

            return _methodDefinition.Parameters[index];
        }
    }
}
