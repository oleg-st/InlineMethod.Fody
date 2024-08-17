using System.Collections.Generic;
using System.Linq;
using Mono.Cecil.Cil;

namespace InlineMethod.Fody.Helper.Eval;

public class InstructionHelper
{
    private readonly Context _context;
    private readonly EvalContext _evalContext;

    public Instruction? Instruction { get; }
    public PushHelper[] PushInstructions { get; }

    public PushHelper FirstPush => PushInstructions[0];
    public Instruction? First => FirstPush.Instruction;
    public bool IsRemovable => PushInstructions.All(p => p.IsRemovable);

    private Value? Eval(PushHelper pushHelper)
    {
        // all values is equals
        if (pushHelper.Sequences != null)
        {
            var value = EvalHelper.Eval(_context, _evalContext, pushHelper.Sequences.Items[0].PushInstruction);
            if (value == null)
            {
                return null;
            }

            for (var i = 1; i < pushHelper.Sequences.Items.Count; i++)
            {
                var current = EvalHelper.Eval(_context, _evalContext, pushHelper.Sequences.Items[i].PushInstruction);
                if (current == null || !current.Equals(value))
                {
                    return null;
                }
            }

            return value;
        }

        return null;
    }

    public IEnumerable<Value?> EvalAll() => PushInstructions.Select(Eval);

    public Value? EvalFirst() => Eval(PushInstructions[0]);

    public Value? EvalSecond() => Eval(PushInstructions[1]);

    public InstructionHelper(Context context, Instruction instruction) : this(context, new EvalContext(), instruction)
    {
    }

    public InstructionHelper(Context context, EvalContext evalContext, Instruction instruction)
    {
        _context = context;
        _evalContext = evalContext;
        Instruction = instruction;
        var pushScanner = new PushScanner(_context.Targets, Instruction);
        PushInstructions = pushScanner.Scan();
    }

    public InstructionHelper(Context context, EvalContext evalContext, PushHelper[] pushHelpers)
    {
        _context = context;
        _evalContext = evalContext;
        Instruction = null;
        PushInstructions = pushHelpers;
    }


    // all push instructions are known and no targets to any instruction except first
    public bool IsEvaluable() => PushInstructions.All(p => p.IsEvaluable);

    public IEnumerable<Instruction> AllPush() =>
        PushInstructions
            .Select(i => i.All)
            .SelectMany(i => i);
}
