using Mono.Cecil.Cil;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
namespace InlineMethod.Fody.Helper;

public class InstructionMapper
{
    private readonly Dictionary<Instruction, Instruction> _instructionMap = [];

    public bool GetInstructionFromMap(Instruction instruction, [MaybeNullWhen(false)] out Instruction outInstruction)
    {
        if (!_instructionMap.TryGetValue(instruction, out var newInstruction))
        {
            outInstruction = null;
            return false;
        }

        while (_instructionMap.TryGetValue(newInstruction, out var newInstruction2))
        {
            newInstruction = newInstruction2;
        }

        outInstruction = newInstruction;
        return true;
    }

    public void Map(Instruction source, Instruction target)
    {
        _instructionMap[source] = target;
    }

    public Instruction GetMappedInstruction(Instruction instruction) =>
        GetInstructionFromMap(instruction, out var newInstruction) ? newInstruction : instruction;

    public IEnumerable<Instruction> GetInstructionTargets(Instruction instruction)
    {
        return instruction.Operand switch
        {
            Instruction targetInstruction => [GetMappedInstruction(targetInstruction)],
            Instruction[] targetInstructions => targetInstructions.Select(GetMappedInstruction),
            _ => []
        };
    }
}