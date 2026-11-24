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

    // Removing only a dup preserves the original evaluation, including its side effects.
    public bool IsRemovable => EscapedDup != null || (!hasSideEffects && AllForRemove.Any());
    private Instruction? EscapedDup =>
        Sequences is { Items.Count: 1 } && Sequences.Items[0].PushEscapedInstructions is { Count: 1 } escaped &&
        escaped[0].OpCode.Code == Code.Dup ? escaped[0] : null;

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
                if (EscapedDup is { } escapedInstruction)
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
