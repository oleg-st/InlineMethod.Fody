using System;
using System.Collections.Generic;
using System.Linq;
using InlineMethod.Fody.Extensions;
using Mono.Cecil.Cil;

namespace InlineMethod.Fody.Helper.Eval;

public class PushInstruction(Instruction? instruction)
{
    public Instruction? Instruction => instruction;
    public readonly Lazy<List<Instruction>> All = new(() => OpCodeHelper.GetAllPushInstructions(instruction));
    public bool IsKnown => instruction != null;
}

public class InstructionHelper
{
    private readonly VarTrackers _varTrackers;
    private readonly HashSet<Instruction> _targets;
    private readonly bool _isPushKnown;
    public Instruction Instruction { get; }
    public PushInstruction[] PushInstructions { get; }

    public Instruction? First => PushInstructions[0].Instruction;
    public Instruction? Second => PushInstructions[1].Instruction;
    
    public Value? EvalFirst() => EvalHelper.Eval(First, _varTrackers, _targets);
    public Value? EvalSecond() => EvalHelper.Eval(Second, _varTrackers, _targets);

    public InstructionHelper(Instruction instruction, VarTrackers varTrackers, HashSet<Instruction> targets)
    {
        _varTrackers = varTrackers;
        _targets = targets;
        Instruction = instruction;
        PushInstructions = instruction.GetPushInstructions(Instruction.GetPopCount())
            .Select(i => new PushInstruction(i)).ToArray();
        _isPushKnown = PushInstructions.All(p => p.IsKnown);
    }

    // all push instructions are known and no targets to any instruction except first
    public bool IsEvaluable() => _isPushKnown && !All().Skip(1).Any(i => i == null || _targets.Contains(i));

    public IEnumerable<Instruction> AllPush() =>
        PushInstructions
            .Select(i => i.All.Value)
            .SelectMany(i => i);

    public IEnumerable<Instruction> All() =>
    [
        ..AllPush(),
        Instruction
    ];
}
