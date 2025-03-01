using System.Collections.Generic;
using System.Linq;
using Mono.Cecil.Cil;

namespace InlineMethod.Fody.Helper;

public class PushHelper(Instruction? instruction, PushScanner.Sequences? sequences, bool hasSideEffects = false)
{
    // single push instruction if exists
    public Instruction? Instruction { get; } = instruction;

    // instruction sequences to evaluate
    public PushScanner.Sequences? Sequences { get; } = sequences;
    public bool IsEvaluable => Sequences != null;
    public bool IsRemovable => AllForRemove.Any();
    public bool NoPushOrEscaped => Sequences == null || Sequences.Items.Count == 0 || Sequences.Items.All(sequence => sequence.PushEscaped);

    // all push instructions
    public IEnumerable<Instruction> AllPush =>
        Sequences is {Items.Count: 1} && Sequences.Items[0] is
            {PushInstruction: not null} sequence
            ? sequence.Nodes.SkipWhile(p => p != sequence.PushInstruction).Reverse()
            : [];

    // all instructions for removing
    public IEnumerable<Instruction> AllForRemove
    {
        get
        {
            if (Sequences == null || Sequences.Items.Count == 0)
            {
                return [];
            }

            if (Sequences.Items.Count == 1)
            {
                // push + dup -> remove dup
                var sequence = Sequences.Items[0];
                if (sequence is { PushEscapedInstructions.Count: 1 } && sequence.PushEscapedInstructions[0] is { OpCode.Code: Code.Dup } escapedInstruction)
                {
                    return [escapedInstruction];
                }

                if (sequence.PushEscaped)
                {
                    return [];
                }

                return AllPush;
            }

            if (hasSideEffects)
            {
                return [];
            }

            // we don't have side effects, so remove all
            return Sequences.Items.Select(s => s.Nodes.Skip(1).Reverse()).SelectMany(s => s).Distinct().OrderBy(s => s.Offset);
        }
    }
}
