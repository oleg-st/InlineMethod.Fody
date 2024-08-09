using System.Collections.Generic;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;

namespace InlineMethod.Fody.Helper;

public class VarTracker(VariableDefinition variableDefinition)
{
    private Instruction? _storeInstruction;
    public int Stores { get; private set; }
    public int Loads { get; private set; }
    public int LoadAddresses { get; private set; }
    public VariableDefinition VariableDefinition => variableDefinition;

    public Instruction? StoreInstruction => Stores == 1 ? _storeInstruction : null;

    public void TrackInstruction(Instruction instruction)
    {
        if (OpCodeHelper.IsStoreLoc(instruction))
        {
            _storeInstruction ??= instruction;
            Stores++;
        } else if (OpCodeHelper.IsLoadLoc(instruction))
        {
            Loads++;
        } else if (!OpCodeHelper.IsLoadLocA(instruction))
        {
            LoadAddresses++;
        }
    }

    public bool IsLoad(Instruction instruction) => 
        OpCodeHelper.IsLoadLoc(instruction);
}

public class VarTrackers(Collection<VariableDefinition> variables)
{
    private readonly Dictionary<int, VarTracker> _varTrackers = new();

    public VarTracker? Get(Instruction instruction)
    {
        var variableDefinition = OpCodeHelper.GetLocVariableDefinition(instruction, variables);
        if (variableDefinition != null)
        {
            if (!_varTrackers.TryGetValue(variableDefinition.Index, out var varTracker))
            {
                varTracker = new VarTracker(variableDefinition);
                _varTrackers.Add(variableDefinition.Index, varTracker);
            }

            return varTracker;
        }

        return null;
    }

    public void Track(Instruction instruction)
    {
        Get(instruction)?.TrackInstruction(instruction);
    }

    public IEnumerable<VarTracker> All => _varTrackers.Values;
}