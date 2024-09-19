using Mono.Cecil.Cil;
using System.Collections.Generic;
using Mono.Collections.Generic;

namespace InlineMethod.Fody.Helper;

public class Targets(Collection<Instruction> instructions)
{
    public Collection<Instruction> Instructions => instructions;

    private readonly Dictionary<Instruction, List<Instruction>> _targets = [];

    public void Add(Instruction instruction, Instruction target)
    {
        if (!_targets.TryGetValue(target, out var sources))
        {
            sources = [];
            _targets.Add(target, sources);
        }

        sources.Add(instruction);
    }

    public bool Contains(Instruction source) => _targets.ContainsKey(source);

    public bool Remove(Instruction instruction, Instruction target)
    {
        if (!_targets.TryGetValue(target, out var sources))
        {
            return false;
        }

        if (!sources.Remove(instruction))
        {
            return false;
        }

        if (sources.Count == 0)
        {
            _targets.Remove(target);
        }

        return true;
    }

    public bool TryGetSources(Instruction target, out List<Instruction> sources)
        => _targets.TryGetValue(target, out sources);

    private static bool HasNext(Instruction? instruction)
        => instruction != null &&
           instruction.OpCode.Code != Code.Jmp &&
           instruction.OpCode.FlowControl is FlowControl.Cond_Branch or FlowControl.Next or FlowControl.Call
               or FlowControl.Meta or FlowControl.Break;

    public IEnumerable<(Instruction, bool)> GetPrevious(Instruction instruction)
    {
        var previous = instruction.Previous;

        if (HasNext(previous))
        {
            yield return (previous, true);
        }

        if (TryGetSources(instruction, out var sources))
        {
            var index = Instructions.IndexOf(instruction);
            foreach (var source in sources)
            {
                yield return (source, Instructions.IndexOf(source) < index);
            }
        }
    }

    public void Clear() => _targets.Clear();
}
