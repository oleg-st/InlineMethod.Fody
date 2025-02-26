using Mono.Cecil.Cil;
using System.Collections.Generic;
using System.Linq;
using InlineMethod.Fody.Extensions;
using Mono.Cecil;

namespace InlineMethod.Fody.Helper;

public class PushScanner(Targets targets, Instruction tail)
{
    public class Sequence
    {
        public List<Instruction> Nodes { get; }
        public bool IsFinished { get; private set; }
        public Instruction First => Nodes.First();
        public Instruction Last => Nodes.Last();
        public Instruction? PushInstruction { get; private set; }
        private int _stackToConsume;
        public int Depth { get; private set; }
        public bool IsValidDepth => Depth == 1;
        // push value escaped
        public bool PushEscaped => PushEscapedInstructions.Count > 0;
        public List<Instruction> PushEscapedInstructions { get; private set; }
        public Instruction? PrevBelowZero { get; private set; }

        private void Process()
        {
            if (IsFinished)
            {
                return;
            }

            // skip first
            if (Nodes.Count < 2)
            {
                return;
            }

            var currentInstruction = Last;
            switch (currentInstruction.OpCode.FlowControl)
            {
                case FlowControl.Return:
                case FlowControl.Throw:
                    Fail();
                    return;

                case FlowControl.Call:
                    if (currentInstruction.OpCode.Code == Code.Jmp)
                    {
                        Fail();
                        return;
                    }

                    break;
            }

            if (currentInstruction.OpCode.StackBehaviourPop == StackBehaviour.PopAll)
            {
                Fail();
                return;
            }

            var popCount = currentInstruction.GetPopCount();
            var pushCount = currentInstruction.GetPushCount();

            if (pushCount > 0)
            {
                switch (_stackToConsume)
                {
                    case 1 when PushInstruction == null:
                        PushInstruction = currentInstruction;
                        if (PrevBelowZero != null)
                        {
                            PushEscapedInstructions.Add(PrevBelowZero);
                        }
                        break;
                    case < 1:
                        throw new InstructionWeavingException(First,
                            $"Could not locate call argument due to {currentInstruction} which pops an unexpected number of items from the stack");
                }

                _stackToConsume -= pushCount;
            }

            _stackToConsume += popCount;

            var beforeDepth = Depth;
            Depth -= popCount;
            Depth += pushCount;
            if (beforeDepth < 0 && popCount > 0)
            {
                PrevBelowZero = currentInstruction;
            }
            else if (Depth >= 0)
            {
                PrevBelowZero = null;
            }

            if (_stackToConsume <= 0)
            {
                if (PushInstruction != null)
                {
                    IsFinished = true;
                    return;
                }

                throw new InstructionWeavingException(First, "Could not locate call argument, reached beginning of method");
            }
        }

        public void Fail()
        {
            PushInstruction = null;
            IsFinished = true;
        }

        public Sequence(Instruction instruction) : this(instruction, [], 1, null, 0, [], null)
        {
        }

        private Sequence(Instruction instruction, List<Instruction> nodes, int stackToConsume, Instruction? result, int depth, List<Instruction> pushEscapedInstructions, Instruction? prevBelowZero)
        {
            Nodes = [.. nodes, instruction];
            _stackToConsume = stackToConsume;
            PushInstruction = result;
            Depth = depth;
            PushEscapedInstructions = [.. pushEscapedInstructions];
            PrevBelowZero = prevBelowZero;
            Process();
        }

        public Sequence WithPrev(Instruction instruction)
             => new(instruction, Nodes, _stackToConsume, PushInstruction, Depth, PushEscapedInstructions, PrevBelowZero);
    }

    public class Sequences(Targets targets)
    {
        public List<Sequence> Items { get; } = [];
        private readonly Queue<Sequence> _notFinished = [];

        public void Add(Sequence sequence)
        {
            Items.Add(sequence);
            if (!sequence.IsFinished)
            {
                _notFinished.Enqueue(sequence);
            }
        }

        public void Process()
        {
            while (_notFinished.Count > 0)
            {
                var sequence = _notFinished.Dequeue();
                var instruction = sequence.Last;
                var prevInstructions = new List<(Instruction, bool)>(targets.GetPrevious(instruction));
                if (prevInstructions.Count == 0)
                {
                    // no previous
                    sequence.Fail();
                }
                else if (prevInstructions.Any(i => !i.Item2))
                {
                    // prev is not before instruction -> fail
                    sequence.Fail();
                }
                else
                {
                    // replace with instruction + prev
                    Items.Remove(sequence);
                    foreach (var (prev, _) in prevInstructions)
                    {
                        Add(sequence.WithPrev(prev));
                    }
                }
            }
        }

        public bool Extend()
        {
            if (Items.Any(s => s.PushInstruction == null))
            {
                return false;
            }
            // find common instruction with valid depth, extend each sequence
            while (true)
            {
                var minIndex = Items.Min(s => targets.Instructions.IndexOf(s.Last));
                var candidate = Items.FirstOrDefault(s => targets.Instructions.IndexOf(s.Last) > minIndex || !s.IsValidDepth);
                if (candidate == null)
                {
                    return true;
                }

                // extend
                var sequence = candidate;
                while (targets.Instructions.IndexOf(sequence.Last)> minIndex|| !sequence.IsValidDepth)
                {
                    var prevInstructions = new List<(Instruction, bool)>(targets.GetPrevious(sequence.Last));
                    if (prevInstructions.Count != 1 || !prevInstructions.Single().Item2)
                    {
                        return false;
                    }

                    var prevInstruction = prevInstructions.Single().Item1;
                    sequence = sequence.WithPrev(prevInstruction);
                }

                Items.Remove(candidate);
                Add(sequence);
            }
        }
    }

    private bool HasSideEffects(Instruction instruction)
    {
        if (
            // const
            OpCodeHelper.IsLoadConst(instruction) || 
            // conv
            OpCodeHelper.IsConv(instruction) ||
            // load arg/var
            OpCodeHelper.IsLoadArg(instruction) || OpCodeHelper.IsLoadLoc(instruction) ||
            // load addr of arg/var
            OpCodeHelper.IsLoadArgA(instruction) || OpCodeHelper.IsLoadLocA(instruction) ||
            // branches
            OpCodeHelper.IsConditionalBranch(instruction) || instruction.OpCode.FlowControl == FlowControl.Branch ||
            // eval
            instruction.OpCode.Code is Code.Dup or Code.Pop or Code.Ldftn or Code.Add or Code.Sub or Code.Mul
                or Code.Div
                or Code.Div_Un or Code.Neg or Code.Not or Code.Or or Code.And or Code.Xor or Code.Shl or Code.Shr
                or Code.Shr_Un or Code.Ceq or Code.Clt or Code.Clt_Un or Code.Cgt or Code.Cgt_Un
           )
        {
            return false;
        }

        // load/store static fields (CompilerGenerated + Delegate)
        if (
            (OpCodeHelper.IsLoadSFld(instruction) || OpCodeHelper.IsStoreSFld(instruction)) &&
            instruction.Operand is FieldReference fieldReference)
        {
            var declaringType = fieldReference.DeclaringType.Resolve();
            if (declaringType.IsSealed && TypeHelper.IsCompilerGenerated(declaringType))
            {
                var fieldType = fieldReference.FieldType.Resolve();
                if (TypeHelper.IsDelegateType(fieldType) || TypeHelper.IsCompilerGenerated(fieldType))
                {
                    return false;
                }
            }
        }

        // new Delegate()
        if (instruction.OpCode.Code == Code.Newobj && instruction.Operand is MethodReference method &&
            TypeHelper.IsDelegateType(method.DeclaringType))
        {
            return false;
        }

        return true;
    }

    private PushHelper Get(ref Instruction? currentInstruction)
    {
        if (currentInstruction == null)
        {
            return new PushHelper(null, null);
        }

        var sequences = new Sequences(targets);
        sequences.Add(new Sequence(currentInstruction));
        sequences.Process();

        // start of block
        Instruction? startInstruction = null;
        // single push instruction
        Instruction? pushInstruction = null;
        Sequences? pushSequences = null;
        var hasSideEffects = false;
        switch (sequences.Items.Count)
        {
            // single sequence
            case 1:
            {
                var sequence = sequences.Items[0];
                if (sequence.PushInstruction != null)
                {
                    startInstruction = sequence.Last;
                    pushInstruction = sequence.PushInstruction;
                    pushSequences = sequences;
                    hasSideEffects = sequence.Nodes.Skip(1).Any(HasSideEffects);
                }

                break;
            }
            // special case: 2 sequences
            case 2:
            {
                // try to extend to common instruction, check some side effects
                if (sequences.Extend())
                {
                    hasSideEffects = sequences.Items.SelectMany(s => s.Nodes.Skip(1)).Any(HasSideEffects);
                    startInstruction = sequences.Items[0].Last;
                    pushSequences = sequences;
                }

                break;
            }
            // todo more sequences is not supported now
        }

        currentInstruction = startInstruction;
        return new PushHelper(pushInstruction, pushSequences, hasSideEffects);
    }

    public PushHelper[] Scan()
    {
        var instruction = tail;
        var count = instruction.GetPopCount();
        var result = new PushHelper[count];
        for (var i = count - 1; i >= 0; i--)
        {
            result[i] = Get(ref instruction);
        }

        return result;
    }
}