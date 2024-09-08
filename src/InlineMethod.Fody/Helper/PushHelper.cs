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
    public bool IsRemovable => All.Any();

    // all instructions for removing
    public IEnumerable<Instruction> All
    {
        get
        {
            if (Sequences == null || Sequences.Items.Count == 0)
            {
                return [];
            }

            if (Sequences.Items.Count == 1)
            {
                var sequence = Sequences.Items[0];
                return sequence.PushInstruction != null && !sequence.PushEscaped
                    ? sequence.Nodes.SkipWhile(p => p != sequence.PushInstruction).Reverse()
                    : [];
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
