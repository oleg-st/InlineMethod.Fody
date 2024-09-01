using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using InlineMethod.Fody.Extensions;
using InlineMethod.Fody.Helper;
using InlineMethod.Fody.Helper.Cecil;
using InlineMethod.Fody.Helper.Eval;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;

namespace InlineMethod.Fody;

public class InlineMethodWeaver
{
    private enum ArgStrategy
    {
        None,
        KeepOnStack,
        Inline,
        Variable
    }

    private readonly ModuleWeaver _moduleWeaver;
    private readonly Instruction _callInstruction;
    private readonly MethodDefinition _parentMethod;
    private readonly MethodDefinition _method;
    private readonly ILProcessor _il;
    private readonly PushHelper[] _pushInstructions;
    private readonly Arg[] _args;
    private ArgStack? _argStack;
    private readonly MethodParameters _parameters;
    private int _firstInnerVariableIndex;
    private Instruction? _firstBodyInstruction;
    private Instruction? _beforeBodyInstruction;
    private readonly InstructionMapper _mapper = new();
    private readonly TypeResolver? _typeResolver;
    private readonly Context _parentContext;

    private static bool CanInlineInstruction(Instruction? instruction, int usages)
        => instruction != null &&
           (
               OpCodeHelper.IsLoadConst(instruction)
               || OpCodeHelper.IsLoadArg(instruction)
               || OpCodeHelper.IsLoadArgA(instruction)
               || OpCodeHelper.IsLoadLoc(instruction)
               || OpCodeHelper.IsLoadLocA(instruction)
               || OpCodeHelper.IsSizeOf(instruction)
               // conv + const/sizeof
               || (OpCodeHelper.IsConv(instruction) && (OpCodeHelper.IsLoadConst(instruction.Previous) ||
                                                        OpCodeHelper.IsSizeOf(instruction.Previous)))
               // load fld/flda + inline previous if one/no usage
               || (usages <= 1 && (OpCodeHelper.IsLoadFlda(instruction) || OpCodeHelper.IsLoadFld(instruction)) &&
                   CanInlineInstruction(instruction.Previous, usages))
               // convert + inline if one/no usage
               || (usages <= 1 && OpCodeHelper.IsConv(instruction) &&
                   CanInlineInstruction(instruction.Previous, usages))
           );

    public InlineMethodWeaver(ModuleWeaver moduleWeaver, Instruction callInstruction, MethodDefinition parentMethod,
        MethodDefinition method)
    {
        _moduleWeaver = moduleWeaver;
        _callInstruction = callInstruction;
        _parentMethod = parentMethod;
        _method = method;
        _il = _parentMethod.Body.GetILProcessor();
        // _moduleWeaver.WriteWarning($"Method {_method.Name} to {_parentMethod.Name}");
        _parentContext = new Context(_parentMethod, _mapper);
        _parentContext.ProcessTargets();
        var pushScanner = new PushScanner(_parentContext.Targets, _callInstruction);
        _pushInstructions = pushScanner.Scan();

        // parameters
        _parameters = new MethodParameters(_method);
        if (_parameters.Count != _pushInstructions.Length)
        {
            throw new NotSupportedException($"The number of parameters ({_parameters.Count}) is not equal to the number of push instructions ({_pushInstructions.Length})");
        }

        _args = new Arg[_pushInstructions.Length];

        _typeResolver = _callInstruction.Operand is GenericInstanceMethod genericInstanceMethod
            ? new TypeResolver(genericInstanceMethod)
            : null;
    }

    private class ArgStack(IEnumerable<Arg> args)
    {
        private readonly List<Arg> _argStack = [..args];

        public Arg? GetTop()
        {
            return _argStack.Count == 0 ? null : _argStack[_argStack.Count - 1];
        }

        private void ConsumeAt(int index)
        {
            if (index < 0)
            {
                return;
            }

            _argStack.RemoveAt(index);
        }

        public void Consume(Arg arg)
        {
            ConsumeAt(_argStack.IndexOf(arg));
        }

        public void Push(Arg arg)
        {
            _argStack.Add(arg);
        }
    }

    [MemberNotNull(nameof(_argStack))]
    private void CreateArgs()
    {
        for (var i = 0; i < _args.Length; i++)
        {
            _args[i] = new Arg(this, i, _pushInstructions[i].Instruction, _args.Length);
        }

        _argStack = new ArgStack(_args);
    }

    private void CreateVars()
    {
        var variables = _parentMethod.Body.Variables;
        _firstInnerVariableIndex = variables.Count;
        // add inner variables to parent
        foreach (var var in _method.Body.Variables)
        {
            variables.Add(new VariableDefinition(_typeResolver != null
                ? _typeResolver.ResolveVariableType(_method, var)
                : var.VariableType));
        }
    }

    private void Remove(Instruction instruction)
    {
        if (_firstBodyInstruction == instruction)
        {
            _firstBodyInstruction = instruction.Next;
        }

        _mapper.Map(instruction, instruction.Next);
        _il.Remove(instruction);
    }

    private void InsertAfter(Instruction target, Instruction instruction)
    {
        _il.InsertAfter(target, instruction);
    }

    private void AppendToBody(Instruction instruction)
    {
        _firstBodyInstruction ??= instruction;
        _il.InsertBefore(_callInstruction, instruction);
    }

    private void InsertBeforeBody(Instruction instruction)
    {
        var target = _firstBodyInstruction ?? _callInstruction;
        _mapper.Map(target, instruction);
        _il.InsertBefore(target, instruction);
        _beforeBodyInstruction ??= instruction;
    }

    private IEnumerable<Instruction> GetInstructionNext(Instruction instruction)
    {
        if (instruction.OpCode.Code == Code.Jmp)
        {
            return [];
        }

        return instruction.OpCode.FlowControl switch
        {
            FlowControl.Branch => _mapper.GetInstructionTargets(instruction),
            FlowControl.Cond_Branch => [.._mapper.GetInstructionTargets(instruction), instruction.Next],
            FlowControl.Next or FlowControl.Call or FlowControl.Meta or FlowControl.Break => [instruction.Next],
            // throw, return
            _ => []
        };
    }

    private void RemoveUnreachableInstructions()
    {
        var instruction = _parentMethod.Body.Instructions.FirstOrDefault();
        if (instruction == null)
        {
            return;
        }

        // find reachable
        var reachableSet = new HashSet<Instruction>();
        AddReachable(instruction);
        // remove unreachable
        while (instruction != null)
        {
            var nextInstruction = instruction.Next;
            if (!reachableSet.Contains(instruction))
            {
                Remove(instruction);
            }

            instruction = nextInstruction;
        }

        return;

        void AddReachable(Instruction i)
        {
            reachableSet.Add(i);
            foreach (var next in GetInstructionNext(i))
            {
                if (!reachableSet.Contains(next))
                {
                    AddReachable(next);
                }
            }
        }
    }

    private void RemoveNopBranchInstructions()
    {
        var instruction = _parentMethod.Body.Instructions.FirstOrDefault();
        while (instruction != null)
        {
            var nextInstruction = instruction.Next;
            if (instruction.OpCode.Code is Code.Br or Code.Br_S)
            {
                var target = _mapper.GetMappedInstruction((Instruction) instruction.Operand);
                if (target == nextInstruction)
                {
                    Remove(instruction);
                } else if (target.OpCode.Code == Code.Ret)
                {
                    // branch to ret -> ret
                    OpCodeHelper.ReplaceInstruction(instruction, Instruction.Create(OpCodes.Ret));
                }
            }

            instruction = nextInstruction;
        }
    }

    private void RemoveAll(IEnumerable<Instruction> instructions)
    {
        foreach (var i in instructions)
        {
            Remove(i);
        }
    }

    private bool ConvertConstantConditionalBranches()
    {
        var converted = false;
        var instruction = _parentMethod.Body.Instructions.FirstOrDefault();
        while (instruction != null)
        {
            var nextInstruction = instruction.Next;
            switch (instruction.OpCode.Code)
            {
                // unary conditional branch
                case Code.Brtrue or Code.Brtrue_S or Code.Brfalse or Code.Brfalse_S:
                {
                    var instructionHelper = new InstructionHelper(_parentContext, instruction);
                    if (instructionHelper.IsEvaluable())
                    {
                        var value = instructionHelper.EvalFirst();
                        if (value is ValueNumber valueNumber && instructionHelper.IsRemovable)
                        {
                            RemoveAll(instructionHelper.AllPush());
                            if (EvalHelper.IsUnaryCondition(instruction, valueNumber))
                            {
                                instruction.OpCode = OpCodes.Br;
                            }
                            else
                            {
                                Remove(instruction);
                            }

                            converted = true;
                        }
                        else
                        {
                            if (instructionHelper.First is
                                {OpCode.Code: Code.Ceq or Code.Clt or Code.Clt_Un or Code.Cgt or Code.Cgt_Un})
                            {
                                Remove(instructionHelper.First);
                                instruction.OpCode =
                                    OpCodeHelper.ConvertUnaryConditional(instruction, instructionHelper.First);
                                converted = true;
                            }
                        }
                    }

                    break;
                }
                case Code.Switch:
                {
                    var instructionHelper = new InstructionHelper(_parentContext, instruction);
                    if (instructionHelper.IsEvaluable())
                    {
                        var value = instructionHelper.EvalFirst();
                        if (value is ValueNumber valueNumber && instructionHelper.IsRemovable)
                        {
                            var switchTargets = (Instruction[])instruction.Operand;
                            var i = valueNumber.I64Value;
                            RemoveAll(instructionHelper.AllPush());
                            if (i >= 0 && i < switchTargets.Length)
                            {
                                instruction.OpCode = OpCodes.Br;
                                instruction.Operand = switchTargets[i];
                            }
                            else
                            {
                                Remove(instruction);
                            }

                            converted = true;
                        }
                    }

                    break;
                }
                default:
                {
                    if (OpCodeHelper.IsConditionalBranch(instruction)) // binary conditional branch
                    {
                        var instructionHelper = new InstructionHelper(_parentContext, instruction);

                        if (instructionHelper.IsEvaluable())
                        {
                            var firstValue = instructionHelper.EvalFirst();
                            var secondValue = instructionHelper.EvalSecond();
                            if (firstValue is ValueNumber firstValueNumber && secondValue is ValueNumber secondValueNumber && instructionHelper.IsRemovable)
                            {
                                RemoveAll(instructionHelper.AllPush());
                                if (EvalHelper.IsBinaryCondition(instruction, firstValueNumber, secondValueNumber))
                                {
                                    instruction.OpCode = OpCodes.Br;
                                }
                                else
                                {
                                    Remove(instruction);
                                }

                                converted = true;
                            }
                        }
                    }

                    break;
                }
            }

            instruction = nextInstruction;
        }

        return converted;
    }

    private void RemoveVariables(HashSet<VariableDefinition> variablesToRemove)
    {
        // copy old variables
        var oldVariables = new Collection<VariableDefinition>(_parentMethod.Body.Variables);
        // remove variables
        foreach (var variableDefinition in variablesToRemove)
        {
            _parentMethod.Body.Variables.Remove(variableDefinition);
        }

        // adjust variable instructions
        foreach (var instruction in _parentMethod.Body.Instructions)
        {
            var variableDefinition = OpCodeHelper.GetLocVariableDefinition(instruction, oldVariables);
            if (variableDefinition != null)
            {
                if (OpCodeHelper.IsLoadLoc(instruction))
                {
                    OpCodeHelper.ReplaceInstruction(instruction, OpCodeHelper.CreateLoadLoc(variableDefinition));
                }
                else if (OpCodeHelper.IsStoreLoc(instruction))
                {
                    OpCodeHelper.ReplaceInstruction(instruction, OpCodeHelper.CreateStoreLoc(variableDefinition));
                }
                else if (OpCodeHelper.IsLoadLocA(instruction))
                {
                    OpCodeHelper.ReplaceInstruction(instruction, OpCodeHelper.CreateLoadLocA(variableDefinition));
                }
            }
        }
    }

    private void RemoveConstantVarStores()
    {
        var variablesToRemove = new HashSet<VariableDefinition>();
        _parentContext.ProcessTargets();
        bool converted;
        do
        {
            converted = false;
            _parentContext.ProcessTrackers();
            foreach (var varTracker in _parentContext.Trackers.VarTrackers)
            {
                // only one store
                if (varTracker is {LoadAddresses: 0, Loads: 0, Stores: 1})
                {
                    var instructionHelper = varTracker.GetInstructionHelper(new EvalContext());
                    if (instructionHelper is {Instruction: not null})
                    {
                        // constant value
                        if (instructionHelper.IsEvaluable())
                        {
                            var value = instructionHelper.EvalFirst();
                            if (value?.Removable == true && instructionHelper.IsRemovable)
                            {
                                RemoveAll([..instructionHelper.AllPush(), instructionHelper.Instruction]);
                                variablesToRemove.Add(varTracker.VariableDefinition);
                                converted = true;
                            }
                        }
                    }
                }
            }
        } while (converted);

        if (variablesToRemove.Any())
        {
            RemoveVariables(variablesToRemove);
        }
    }

    private void FoldBranches()
    {
        _parentContext.ProcessTrackers();
        while (true)
        {
            _parentContext.ProcessTargets();
            // convert conditional branches to unconditional
            if (!ConvertConstantConditionalBranches())
            {
                break;
            }

            RemoveUnreachableInstructions();
            RemoveNopBranchInstructions();
        }

        RemoveConstantVarStores();
    }

    private void FixInstructions()
    {
        // TODO: optimize (do once per parent method)
        // fix targets, extend instructions
        var instruction = _parentMethod.Body.Instructions.FirstOrDefault();
        var offset = 0;
        var shortBranchInstructions = new List<Instruction>();
        while (instruction != null)
        {
            var nextInstruction = instruction.Next;
            switch (instruction.Operand)
            {
                case Instruction opInstruction:
                {
                    if (_mapper.GetInstructionFromMap(opInstruction, out var newInstruction))
                    {
                        instruction.Operand = newInstruction;
                    }

                    break;
                }
                case Instruction[] opInstructions:
                {
                    for (var index = 0; index < opInstructions.Length; index++)
                    {
                        if (_mapper.GetInstructionFromMap(opInstructions[index], out var newInstruction))
                        {
                            opInstructions[index] = newInstruction;
                        }
                    }

                    break;
                }
            }

            if (instruction.OpCode.OperandType == OperandType.ShortInlineBrTarget)
            {
                shortBranchInstructions.Add(instruction);
            }

            // extend short variable instructions to long if needed
            OpCodeHelper.ExtendVariableOpCode(instruction);

            instruction.Offset = offset;
            offset += instruction.GetSize();

            instruction = nextInstruction;
        }

        // extend short branch instructions if needed
        bool wasExtended;
        do
        {
            wasExtended = false;
            for (var i = shortBranchInstructions.Count - 1; i >= 0; i--)
            {
                var shortBranchInstruction = shortBranchInstructions[i];
                var target = (Instruction) shortBranchInstruction.Operand;
                var diff = target.Offset - shortBranchInstruction.Offset - shortBranchInstruction.GetSize();
                if (diff is < sbyte.MinValue or > sbyte.MaxValue)
                {
                    OpCodeHelper.ExtendBranchOpCode(shortBranchInstruction);
                    shortBranchInstructions.RemoveAt(i);
                    wasExtended = true;
                }
            }

            if (wasExtended && shortBranchInstructions.Count > 0)
            {
                CalcParentMethodOffsets();
            }
        } while (wasExtended);
    }

    private void CalcParentMethodOffsets()
    {
        var instruction = _parentMethod.Body.Instructions.FirstOrDefault();
        var offset = 0;
        while (instruction != null)
        {
            var nextInstruction = instruction.Next;
            instruction.Offset = offset;
            offset += instruction.GetSize();
            instruction = nextInstruction;
        }
    }

    private static IEnumerable<Instruction> GetReferencedInstructions(Instruction? instruction)
    {
        while (instruction != null)
        {
            var nextInstruction = instruction.Next;

            switch (instruction.Operand)
            {
                case Instruction opInstruction:
                    yield return opInstruction;
                    break;
                case Instruction[] opInstructions:
                {
                    foreach (var innerOpInstruction in opInstructions)
                    {
                        yield return innerOpInstruction;
                    }

                    break;
                }
            }

            instruction = nextInstruction;
        }
    }

    private class Arg(InlineMethodWeaver inlineMethodWeaver, int paramIndex, Instruction? pushInstruction, int count)
    {
        public Instruction? PushInstruction { get; private set; } = pushInstruction;
        private bool _onlyLoad = true;
        public int Usages { get; private set; }

        public ArgStrategy Strategy { get; private set; }
        private VariableDefinition? _variableDefinition;
        private List<Instruction>? _allPushInstructions;

        public void TrackInstruction(Instruction instruction)
        {
            Usages++;
            if (OpCodeHelper.IsLoadArgA(instruction) || OpCodeHelper.IsStoreArg(instruction))
            {
                _onlyLoad = false;
            }
        }

        [MemberNotNull(nameof(_variableDefinition))]
        private void CreateVariableDefinition()
        {
            _variableDefinition = new VariableDefinition(inlineMethodWeaver._parameters[paramIndex].ParameterType);
            inlineMethodWeaver._parentMethod.Body.Variables.Add(_variableDefinition);
        }

        public bool CanKeepOnStack => _onlyLoad && (Usages == 1 || (Usages > 1 && CanDup));

        private bool IsLast => count == paramIndex + 1;

        private bool CanDup => (HasPush && PushInstruction.GetPushCount() == 1) || IsLast;

        private void InsertConsumeTopArg(Instruction instruction)
        {
            var topArg = inlineMethodWeaver._argStack!.GetTop();
            if (topArg != this)
            {
                throw new Exception($"Failed to reach argument from stack {inlineMethodWeaver._method.Name} to {inlineMethodWeaver._parentMethod.Name}");
            }

            inlineMethodWeaver.InsertBeforeBody(instruction);
        }

        public void Finish()
        {
            // keep/dup arg push
            var argStack = inlineMethodWeaver._argStack!;
            if (KeepOnStack)
            {
                Strategy = ArgStrategy.KeepOnStack;
                if (Usages > 1)
                {
                    if (HasPush && PushInstruction.GetPushCount() == 1)
                    {
                        inlineMethodWeaver.InsertAfter(PushInstruction!, Instruction.Create(OpCodes.Dup));
                    }
                    else
                    {
                        InsertConsumeTopArg(Instruction.Create(OpCodes.Dup));
                        argStack.Push(this);
                    }
                }
                return;
            }

            // neutralize arg push
            if (CanInline || Usages == 0)
            {
                Strategy = Usages == 0 ? ArgStrategy.None : ArgStrategy.Inline;
                _allPushInstructions = OpCodeHelper.GetAllPushInstructions(PushInstruction);
                if (CanRemovePush)
                {
                    foreach (var instruction in _allPushInstructions)
                    {
                        inlineMethodWeaver.Remove(instruction);
                    }
                }
                else
                {
                    // neutralize push instruction
                    if (PushInstruction == null)
                    {
                        InsertConsumeTopArg(Instruction.Create(OpCodes.Pop));
                    }
                    else
                    {
                        for (var i = 0; i < PushInstruction.GetPushCount(); i++)
                        {
                            inlineMethodWeaver.InsertAfter(PushInstruction, Instruction.Create(OpCodes.Pop));
                        }
                    }
                }

                argStack.Consume(this);
                return;
            }

            Strategy = ArgStrategy.Variable;
            CreateVariableDefinition();

            // place store loc
            var storeLoc = OpCodeHelper.CreateStoreLoc(_variableDefinition);
            if (PushInstruction == null)
            {
                InsertConsumeTopArg(storeLoc);
            }
            else
            {
                inlineMethodWeaver.InsertAfter(PushInstruction, storeLoc);
            }

            argStack.Consume(this);
        }

        private IEnumerable<Instruction> GetLdInstructions()
        {
            switch (Strategy)
            {
                case ArgStrategy.Variable:
                    yield return OpCodeHelper.CreateLoadLoc(_variableDefinition!);
                    break;
                case ArgStrategy.Inline:
                    foreach (var instruction in _allPushInstructions!)
                    {
                        yield return OpCodeHelper.Clone(instruction);
                    }
                    break;
                case ArgStrategy.KeepOnStack:
                    break;
            }
        }

        private bool CanRemovePush => PushInstruction != null && (PushInstruction.OpCode.StackBehaviourPop == StackBehaviour.Pop0 || CanInline);

        private bool CanInline => _onlyLoad && CanInlineInstruction(PushInstruction, Usages);

        [MemberNotNullWhen(true, nameof(PushInstruction))]
        public bool HasPush => PushInstruction != null;
        public bool KeepOnStack { get; set; }

        public IEnumerable<Instruction> GetInstructions(Instruction instruction)
        {
            if (OpCodeHelper.IsLoadArg(instruction))
            {
                foreach (var ldInstruction in GetLdInstructions())
                {
                    yield return ldInstruction;
                }
            } else if (OpCodeHelper.IsLoadArgA(instruction))
            {
                yield return OpCodeHelper.CreateLoadLocA(_variableDefinition!);
            } else if (OpCodeHelper.IsStoreArg(instruction))
            {
                yield return OpCodeHelper.CreateStoreLoc(_variableDefinition!);
            }
            else
            {
                throw new NotSupportedException($"Unknown arg instruction {instruction.OpCode}");
            }
        }
    }

    private class LoadArgInfo
    {
        public int Index { get; }
        public int StackDepth { get; }
        public int Count { get; private set; }
        public int Popped { get; set; }

        public int CurrentDepth => StackDepth + Count - Popped;

        public LoadArgInfo(int index, int stackDepth)
        {
            Index = index;
            StackDepth = stackDepth;
            Count++;
        }

        public void Increment()
        {
            Count++;
        }
    }

    private void AnalyzeArgs()
    {
        var firstLoadArgs = new List<LoadArgInfo>();
        var references = new HashSet<int>(
            GetReferencedInstructions(_method.Body.Instructions.FirstOrDefault())
                .Select(i => i.Offset)
        );

        var isInLoadArgs = true;
        var instructions = _method.Body.Instructions;

        var stackDepth = 0;
        foreach (var instruction in instructions)
        {
            // targets / references
            if (isInLoadArgs && (OpCodeHelper.HasTargets(instruction) || references.Contains(instruction.Offset)))
            {
                isInLoadArgs = false;
            }

            var parameterDefinition = OpCodeHelper.GetArgParameterDefinition(instruction, _parameters);
            if (parameterDefinition != null)
            {
                var index = parameterDefinition.Sequence;
                var arg = _args[index];
                arg.TrackInstruction(instruction);

                if (isInLoadArgs)
                {
                    var last = firstLoadArgs.LastOrDefault();

                    if (OpCodeHelper.IsLoadArg(instruction) &&
                        (last?.CurrentDepth ?? 0) == stackDepth &&
                        (last == null || index >= last.Index))
                    {
                        // same index
                        if (last != null && index == last.Index)
                        {
                            last.Increment();
                        }
                        else
                        {
                            // cannot use another index after pop
                            if (last is {Popped: > 0})
                            {
                                isInLoadArgs = false;
                            }
                            else
                            {
                                firstLoadArgs.Add(new LoadArgInfo(index, stackDepth));
                            }
                        }
                    }
                    else
                    {
                        isInLoadArgs = false;
                    }
                }
            }

            if (isInLoadArgs && instruction.OpCode.Code != Code.Ret)
            {
                var popCount = instruction.GetPopCount();
                stackDepth -= popCount;
                if (popCount > 0)
                {
                    var last = firstLoadArgs.LastOrDefault();
                    if (last != null)
                    {
                        // popped beyond last
                        if (stackDepth < last.StackDepth)
                        {
                            isInLoadArgs = false;
                        } else
                        {
                            // popped some more
                            var newPopped = last.StackDepth + last.Count - stackDepth;
                            if (stackDepth < last.StackDepth + last.Count && newPopped > last.Popped)
                            {
                                last.Popped = newPopped;
                            }
                        }
                    }
                }

                stackDepth += instruction.GetPushCount();
            }
        }

        // check keep on stack
        AnalyzeKeepOnStack(firstLoadArgs);

        // finish
        for (var index = _args.Length - 1; index >= 0; index--)
        {
            var arg = _args[index];
            arg.Finish();
        }

        //_moduleWeaver.WriteWarning($"{_method.Name} to {_parentMethod.Name} {string.Join(", ", _args.Select(arg => $"{arg.Strategy}"))}");
    }

    private void AnalyzeKeepOnStack(List<LoadArgInfo> firstLoadArgs)
    {
        var currentIndex = 0;
        foreach (var loadArg in firstLoadArgs)
        {
            var arg = _args[loadArg.Index];
            if (loadArg.Count != arg.Usages || !arg.CanKeepOnStack)
            {
                // cannot keep -> stop
                return;
            }

            for (var i = currentIndex; i < loadArg.Index; i++)
            {
                if (!_args[i].HasPush)
                {
                    // cannot neutralize push on args before -> stop
                    return;
                }
            }

            arg.KeepOnStack = true;
            currentIndex = loadArg.Index;
        }
    }

    public void Process()
    {
        AnalyzeResolveDelegates();

        var callSequencePoint = _parentMethod.DebugInformation.HasSequencePoints ? _parentMethod.DebugInformation.GetSequencePoint(_callInstruction) : null;
        CreateVars();
        CreateArgs();
        AnalyzeArgs();

        var callInstructionNext = _callInstruction.Next;
        var innerVariables = _method.Body.Variables;
        var parentVariables = _parentMethod.Body.Variables;

        // inline body
        var instructions = _method.Body.Instructions;
        foreach (var instruction in instructions)
        {
            var nextInstruction = instruction.Next;
            Instruction? newInstruction = null;

            // arg
            var parameterDefinition = OpCodeHelper.GetArgParameterDefinition(instruction, _parameters);
            if (parameterDefinition != null)
            {
                var arg = _args[parameterDefinition.Sequence];
                var isFirst = true;
                foreach (var argInstruction in arg.GetInstructions(instruction))
                {
                    if (isFirst)
                    {
                        _mapper.Map(instruction, argInstruction);
                        isFirst = false;
                    }
                    AppendToBody(argInstruction);
                }
                continue;
            }

            // loc
            var innerVariableDefinition = OpCodeHelper.GetLocVariableDefinition(instruction, innerVariables);
            if (innerVariableDefinition != null)
            {
                newInstruction = OpCodeHelper.CreateVarInstruction(instruction,
                    parentVariables[innerVariableDefinition.Index + _firstInnerVariableIndex]);
            }

            // branch
            if (instruction.OpCode.OperandType is OperandType.InlineBrTarget or OperandType.ShortInlineBrTarget)
            {
                var target = (Instruction) instruction.Operand;
                // fix target ret
                if (target?.OpCode.Code == Code.Ret)
                {
                    newInstruction = Instruction.Create(instruction.OpCode, callInstructionNext);
                }
            }

            // ret
            if (instruction.OpCode.Code == Code.Ret)
            {
                // skip last return
                if (nextInstruction == null)
                {
                    break;
                }

                newInstruction = Instruction.Create(OpCodes.Br, callInstructionNext);
            }

            // resolve delegate
            if (_resolveDelegates.TryGetValue(instruction, out var resolveDelegate))
            {
                var instanceAllInstructions = resolveDelegate.DelegateInstanceAllInstructions.Select(i => _mapper.GetMappedInstruction(i)).ToList();
                // delegate.Invoke(...) -> instance.Method(...)
                // push delegate, push args, call Delegate::Invoke -> push methodInstance, push args, call Method
                InsertAfter(instanceAllInstructions.Last(), OpCodeHelper.Clone(resolveDelegate.MethodInstanceInstruction));
                RemoveAll(instanceAllInstructions);
                newInstruction = Instruction.Create(OpCodes.Callvirt, resolveDelegate.Method);
            }

            newInstruction ??= OpCodeHelper.Clone(instruction);

            // import references / resolve generics
            if (newInstruction.Operand is FieldReference opFieldReference)
            {
                if (_typeResolver != null)
                {
                    opFieldReference = _typeResolver.Resolve(opFieldReference);
                }

                newInstruction.Operand = _moduleWeaver.ModuleDefinition.ImportReference(opFieldReference, _parentMethod);
            }

            if (newInstruction.Operand is MethodReference opMethodReference)
            {
                if (_typeResolver != null && opMethodReference.ContainsGenericParameter)
                {
                    opMethodReference = _typeResolver.Resolve(opMethodReference);
                }

                newInstruction.Operand = _moduleWeaver.ModuleDefinition.ImportReference(opMethodReference, _parentMethod);
            }

            if (newInstruction.Operand is TypeReference opTypeReference)
            {
                if (_typeResolver != null)
                {
                    opTypeReference = _typeResolver.Resolve(opTypeReference);
                }

                newInstruction.Operand = _moduleWeaver.ModuleDefinition.ImportReference(opTypeReference, _parentMethod);
            }

            _mapper.Map(instruction, newInstruction);
            AppendToBody(newInstruction);
        }

        Remove(_callInstruction);

        // replace call target
        var callInstructionTarget = _beforeBodyInstruction ?? _firstBodyInstruction;
        if (callInstructionTarget != null)
        {
            _mapper.Map(_callInstruction, callInstructionTarget);
        }

        FoldBranches();
        FixInstructions();

        if (_parentMethod.DebugInformation.HasSequencePoints && _firstBodyInstruction != null && callSequencePoint != null)
        {
            var sequencePoints = _parentMethod.DebugInformation.SequencePoints;
            var indexOf = sequencePoints.Count;
            for (var i = 0; i < sequencePoints.Count; i++)
            {
                if (sequencePoints[i].Offset >= _firstBodyInstruction.Offset)
                {
                    indexOf = i;
                    break;
                }
            }

            sequencePoints.Insert(indexOf,
                new SequencePoint(_firstBodyInstruction, callSequencePoint.Document)
                {
                    StartLine = callSequencePoint.StartLine,
                    StartColumn = callSequencePoint.StartColumn,
                    EndLine = callSequencePoint.EndLine,
                    EndColumn = callSequencePoint.EndColumn
                });
        }

        InlineDelegates();
    }

    private CustomAttribute? GetResolveDelegate(ParameterDefinition parameter)
        => parameter.CustomAttributes.FirstOrDefault(attr =>
            attr.AttributeType.FullName == "InlineMethod.ResolveDelegateAttribute");

    private bool IsResolveDelegateInline(CustomAttribute attr)
        => attr.ConstructorArguments.First().Value is true;

    private class ResolveDelegateInfo(
        Instruction callInstruction,
        MethodReference method,
        Instruction methodInstanceInstruction,
        Instruction[] delegateInstanceAllInstructions,
        bool inline)
    {
        public Instruction CallInstruction => callInstruction;
        public MethodReference Method => method;
        public Instruction MethodInstanceInstruction => methodInstanceInstruction;
        public Instruction[] DelegateInstanceAllInstructions => delegateInstanceAllInstructions;
        public bool Inline => inline;
    }

    private readonly Dictionary<Instruction, ResolveDelegateInfo> _resolveDelegates = [];

    private void AnalyzeResolveDelegates()
    {
        if (_method.Parameters.All(p => GetResolveDelegate(p) == null))
        {
            return;
        }

        var parentContext = new Context(_parentMethod, new InstructionMapper());
        var context = new Context(_method, new InstructionMapper());
        parentContext.Process(_callInstruction);
        context.Process();

        for (var i = 0; i < _parameters.Count; i++)
        {
            var parameter = _parameters[i];
            if (GetResolveDelegate(parameter) != null)
            {
                if (!TypeHelper.IsDelegateType(parameter.ParameterType))
                {
                    _moduleWeaver.WriteWarning($"Cannot resolve delegate for `{parameter}` of `{_method.FullName}`");
                    continue;
                }

                context.Trackers.Add(new ArgTracker(parentContext, parameter, _pushInstructions[i]));
            }
        }

        if (!context.Trackers.ArgTrackers.Any())
        {
            return;
        }

        foreach (var instruction in _method.Body.Instructions)
        {
            // Delegate.Invoke
            if (instruction.OpCode.Code == Code.Callvirt && instruction.Operand is MethodReference { HasThis: true, Name: "Invoke"} calledMethod)
            {
                var calledMethodDefinition = calledMethod.Resolve();
                if (calledMethodDefinition != null && TypeHelper.IsDelegateType(calledMethodDefinition.DeclaringType))
                {
                    var instructionHelper = new InstructionHelper(context, instruction);
                    if (instructionHelper.IsEvaluable())
                    {
                        var callee = instructionHelper.EvalFirst();
                        // value -> new (<>c.instance, Method)
                        if (callee is ValueNewObject {Arguments.Length: 2} delegateObject &&
                            delegateObject.Arguments[0] is ValueNewObject {Arguments.Length: 0} &&
                            delegateObject.Arguments[1] is ValueMethod method && delegateObject.InstructionHelper.First != null &&
                            OpCodeHelper.IsLoadSFld(delegateObject.InstructionHelper.First))
                        {
                            // inline?
                            var inline = callee.EvalContext.Trackers
                                .OfType<ArgTracker>()
                                .Select(argTracker => GetResolveDelegate(argTracker.ParameterDefinition))
                                .Any(attr => attr != null && IsResolveDelegateInline(attr));

                            // (new (<>c.instance, Method)).Invoke(...) -> <>c.instance.Method(...)
                            _resolveDelegates.Add(instruction,
                                new ResolveDelegateInfo(instruction, method.Method,
                                    delegateObject.InstructionHelper.First,
                                    [.. instructionHelper.FirstPush.All], inline));
                        }
                    }
                }
            }
        }
    }

    private void InlineDelegates()
    {
        foreach (var resolveDelegate in _resolveDelegates.Values.Where(resolveDelegate => resolveDelegate.Inline))
        {
            var callInstruction = _mapper.GetMappedInstruction(resolveDelegate.CallInstruction);
            _moduleWeaver.ProcessCallInstruction(callInstruction, _parentMethod, true);
        }
    }
}
