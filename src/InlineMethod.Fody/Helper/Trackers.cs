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

    // the stores we can see give the value of every load of the field
    private readonly bool _isValueKnown;

    public StaticFieldTracker(Context context, FieldDefinition fieldDefinition) : base(context)
    {
        FieldDefinition = fieldDefinition;
        var staticConstructor = fieldDefinition is {IsStatic: true}
            ? fieldDefinition.DeclaringType.Methods.FirstOrDefault(m => m.IsStatic && m.IsConstructor)
            : null;

        // a cache field holds one value written by one store, so it does not matter where the
        // store sits; any other field is only pinned down by its static constructor's stores once
        // nothing can read it ahead of them
        _isValueKnown = TypeHelper.IsSingleAssignmentCacheField(fieldDefinition) ||
                        (IsConstantField(fieldDefinition) &&
                         IsSettledInStaticConstructor(staticConstructor, fieldDefinition));

        // the stores that give the value are the ones in the static constructor
        if (!_isValueKnown || staticConstructor is not {HasBody: true})
        {
            return;
        }

        foreach (var instruction in staticConstructor.Body.Instructions)
        {
            if (instruction.Operand is FieldReference opFieldReference && opFieldReference.Resolve() == fieldDefinition)
            {
                TrackInstruction(instruction);
            }
        }
    }

    // a static readonly field can only be stored by the static constructor of its declaring type,
    // so no other method body can add to the stores we find there
    private static bool IsConstantField(FieldDefinition? fieldDefinition)
        // a mutable field can be assigned at any time from anywhere, so a store in the static
        // constructor says nothing about the value a load will see
        => fieldDefinition is {IsStatic: true, IsInitOnly: true} &&
           // a type that is not beforefieldinit runs its static constructor on the first access to
           // one of its static fields, so folding away the only access in a method would skip it
           fieldDefinition.DeclaringType.IsBeforeFieldInit;

    // Whether the static constructor's stores of the field give the value a load outside it sees.
    //
    // The stores are collected as a set, with no order among them, which is only the field's
    // settled value once nothing can read the field before the last of them. While the static
    // constructor is still running the field still holds its default, so a load reached from
    // there would be handed a value that has not been assigned yet.
    private static bool IsSettledInStaticConstructor(MethodDefinition? staticConstructor,
        FieldDefinition fieldDefinition)
    {
        if (staticConstructor is not {HasBody: true})
        {
            return true;
        }

        var instructions = staticConstructor.Body.Instructions;
        var lastStore = -1;
        for (var i = 0; i < instructions.Count; i++)
        {
            if (OpCodeHelper.IsStoreSFld(instructions[i]) &&
                (instructions[i].Operand as FieldReference)?.Resolve() == fieldDefinition)
            {
                lastStore = i;
            }
        }

        for (var i = 0; i < lastStore; i++)
        {
            var instruction = instructions[i];

            // control leaves the static constructor and can read the field on its way
            if (instruction.OpCode.Code is Code.Call or Code.Callvirt or Code.Calli or Code.Newobj)
            {
                return false;
            }

            // the static constructor reading its own type's state before this field has settled:
            // the value being stored may itself come from a field that is only assigned later
            if (instruction.OpCode.Code is Code.Ldsfld or Code.Ldsflda &&
                instruction.Operand is FieldReference operandField &&
                operandField.DeclaringType.Resolve() == fieldDefinition.DeclaringType)
            {
                return false;
            }
        }

        return true;
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
        => _isValueKnown && Stores > 0
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