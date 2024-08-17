using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace InlineMethod.Fody.Helper;

public class Context(MethodDefinition method, InstructionMapper mapper)
{
    public Trackers Trackers { get; } = new();
    public Targets Targets { get; } = new(method.Body.Instructions);
    public MethodDefinition Method => method;

    public readonly MethodParameters Parameters = new(method);

    public Tracker? GetTracker(Instruction instruction) => Trackers.Get(this, instruction);

    public void Process(Instruction? outer = null)
    {
        ProcessTargets();
        ProcessTrackers(outer);
    }

    public void ProcessTrackers(Instruction? outer = null)
    {
        Trackers.Clear();
        foreach (var instruction in method.Body.Instructions.TakeWhile(instruction => instruction != outer))
        {
            Trackers.Track(this, instruction);
        }
    }

    public void ProcessTargets()
    {
        Targets.Clear();
        foreach (var instruction in Method.Body.Instructions)
        {
            foreach (var target in mapper.GetInstructionTargets(instruction))
            {
                Targets.Add(instruction, mapper.GetMappedInstruction(target));
            }
        }
    }
}
