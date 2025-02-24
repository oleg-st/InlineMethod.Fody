using System.Collections.Generic;
using System.Linq;
using InlineMethod.Fody.Helper.Eval;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace InlineMethod.Fody.Helper;

public abstract class Tracker(Context context)
{
    public Context Context { get; } = context;

    public int Stores { get; protected set; }

    public abstract void TrackInstruction(Instruction instruction);

    public abstract bool IsLoad(Instruction instruction);

    public abstract ICollection<InstructionHelper> GetInstructionHelpers(EvalContext evalContext);
}

public sealed class ArgTracker : Tracker
{
    private readonly PushHelper _pushHelper;
    public ParameterDefinition ParameterDefinition { get; }
    public int Loads { get; private set; }
    public int LoadAddresses { get; private set; }

    public override void TrackInstruction(Instruction instruction)
    {
        if (OpCodeHelper.IsStoreArg(instruction))
        {
            Stores++;
        } else if (OpCodeHelper.IsLoadArg(instruction))
        {
            Loads++;
        } else if (OpCodeHelper.IsLoadArgA(instruction))
        {
            LoadAddresses++;
        }
    }

    public override bool IsLoad(Instruction instruction) =>
        OpCodeHelper.IsLoadArg(instruction);

    public ArgTracker(Context context, ParameterDefinition parameterDefinition, PushHelper pushHelper) : base(context)
    {
        _pushHelper = pushHelper;
        ParameterDefinition = parameterDefinition;
        Stores++;
    }

    public override ICollection<InstructionHelper> GetInstructionHelpers(EvalContext evalContext) =>
        Stores == 1 && LoadAddresses == 0 ? [new InstructionHelper(Context, evalContext, [_pushHelper])] : [];
}

public sealed class VarTracker(Context context, VariableDefinition variableDefinition) : Tracker(context)
{
    private readonly List<Instruction> _storeInstructions = [];
    public int Loads { get; private set; }
    public int LoadAddresses { get; private set; }
    public VariableDefinition VariableDefinition => variableDefinition;

    public override void TrackInstruction(Instruction instruction)
    {
        if (OpCodeHelper.IsStoreLoc(instruction))
        {
            _storeInstructions.Add(instruction);
            Stores++;
        } else if (OpCodeHelper.IsLoadLoc(instruction))
        {
            Loads++;
        } else if (OpCodeHelper.IsLoadLocA(instruction))
        {
            LoadAddresses++;
        }
    }

    public override bool IsLoad(Instruction instruction) => 
        OpCodeHelper.IsLoadLoc(instruction);

    public override ICollection<InstructionHelper> GetInstructionHelpers(EvalContext evalContext)
        => Stores > 0 && LoadAddresses == 0
            ? _storeInstructions
                .Select(storeInstruction => new InstructionHelper(Context, evalContext, storeInstruction)).ToList()
            : [];
}

public sealed class StaticFieldTracker : Tracker
{
    public FieldDefinition FieldDefinition { get; }

    public StaticFieldTracker(Context context, FieldDefinition fieldDefinition) : base(context)
    {
        FieldDefinition = fieldDefinition;
        // field is static -> scan static constructor
        if (FieldDefinition is {IsStatic: true})
        {
            var staticConstructor = FieldDefinition.DeclaringType.Methods.FirstOrDefault(m => m.IsStatic && m.IsConstructor);
            if (staticConstructor is {HasBody: true})
            {
                foreach (var instruction in staticConstructor.Body.Instructions)
                {
                    if (instruction.Operand is FieldReference opFieldReference && opFieldReference.Resolve() == fieldDefinition)
                    {
                        TrackInstruction(instruction);
                    }
                }
            }
        }
    }

    private readonly List<Instruction> _storeInstructions = [];

    public override void TrackInstruction(Instruction instruction)
    {
        if (OpCodeHelper.IsStoreSFld(instruction))
        {
            _storeInstructions.Add(instruction);
            Stores++;
        }
    }

    public override bool IsLoad(Instruction instruction) => 
        OpCodeHelper.IsLoadSFld(instruction);

    public override ICollection<InstructionHelper> GetInstructionHelpers(EvalContext evalContext)
        => Stores > 0
            ? _storeInstructions
                .Select(storeInstruction => new InstructionHelper(Context, evalContext, storeInstruction)).ToList()
            : [];
}

public class Trackers
{
    private readonly Dictionary<VariableDefinition, VarTracker> _varTrackers = [];
    private readonly Dictionary<FieldDefinition, StaticFieldTracker> _staticFieldTrackers = [];
    private readonly Dictionary<ParameterDefinition, ArgTracker> _argTrackers = [];

    public Tracker? Get(Context context, Instruction instruction)
    {
        var variableDefinition = OpCodeHelper.GetLocVariableDefinition(instruction, context.Method.Body.Variables);
        if (variableDefinition != null)
        {
            if (!_varTrackers.TryGetValue(variableDefinition, out var varTracker))
            {
                varTracker = new VarTracker(context, variableDefinition);
                _varTrackers.Add(variableDefinition, varTracker);
            }

            return varTracker;
        }

        if (instruction.Operand is FieldReference fieldReference)
        {
            var fieldDefinition = fieldReference.Resolve();
            if (!_staticFieldTrackers.TryGetValue(fieldDefinition, out var fieldTracker))
            {
                fieldTracker = new StaticFieldTracker(context, fieldDefinition);
                _staticFieldTrackers.Add(fieldDefinition, fieldTracker);
            }

            return fieldTracker;
        }

        var parameterDefinition = OpCodeHelper.GetArgParameterDefinition(instruction, context.Parameters);
        if (parameterDefinition != null)
        {
            if (_argTrackers.TryGetValue(parameterDefinition, out var argTracker))
            {
                return argTracker;
            }
        }

        return null;
    }

    public void Add(ArgTracker argTracker)
    {
        _argTrackers[argTracker.ParameterDefinition] = argTracker;
    }

    public void Track(Context context, Instruction instruction)
    {
        Get(context, instruction)?.TrackInstruction(instruction);
    }

    public IEnumerable<VarTracker> VarTrackers => _varTrackers.Values;
    public IEnumerable<ArgTracker> ArgTrackers => _argTrackers.Values;
    public IEnumerable<StaticFieldTracker> StaticFieldTrackers => _staticFieldTrackers.Values;

    public void Clear()
    {
        _argTrackers.Clear();
        _staticFieldTrackers.Clear();
        _varTrackers.Clear();
    }
}