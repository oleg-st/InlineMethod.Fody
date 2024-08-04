using System.Collections.Generic;
using InlineMethod.Fody.Extensions;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;

namespace InlineMethod.Fody.Helper;

public abstract class Tracker
{
    public int Stores { get; protected set; }

    public abstract Instruction? StoreInstruction { get; }

    public abstract void TrackInstruction(Instruction instruction);
}

public class VarTracker : Tracker
{
    private Instruction? _storeInstruction;

    public override Instruction? StoreInstruction => Stores == 1 ? _storeInstruction : null;

    public override void TrackInstruction(Instruction instruction)
    {
        if (OpCodeHelper.IsStoreLoc(instruction))
        {
            _storeInstruction ??= instruction;
            Stores++;
        }
    }
}

public class Trackers(Collection<VariableDefinition> variables)
{
    private readonly Dictionary<int, VarTracker> _varTrackers = new();

    public Tracker? GetTracker(Instruction instruction)
    {
        var variableDefinition = OpCodeHelper.GetLocVariableDefinition(instruction, variables);
        if (variableDefinition != null)
        {
            if (!_varTrackers.TryGetValue(variableDefinition.Index, out var varTracker))
            {
                varTracker = new VarTracker();
                _varTrackers.Add(variableDefinition.Index, varTracker);
            }

            return varTracker;
        }

        return null;
    }

    public void TrackInstruction(Instruction instruction)
    {
        GetTracker(instruction)?.TrackInstruction(instruction);
    }
}