using System;
using System.Collections.Generic;
using System.Linq;
using InlineMethod.Fody.Extensions;
using InlineMethod.Fody.Helper;
using InlineMethod.Fody.Helper.Cecil;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace InlineMethod.Fody
{
    public class InlineMethodWeaver
    {
        private enum ArgStrategy
        {
            None,
            KeepOnStack,
            Inline,
            Variable,
        }

        private readonly ModuleWeaver _moduleWeaver;
        private readonly Instruction _callInstruction;
        private readonly MethodDefinition _parentMethod;
        private readonly MethodDefinition _method;
        private readonly ILProcessor _il;
        private readonly Instruction[] _pushInstructions;
        private readonly Arg[] _args;
        private ArgStack _argStack;
        private readonly MethodParameters _parameters;
        private int _firstInnerVariableIndex;
        private Instruction _firstBodyInstruction;
        private Instruction _beforeBodyInstruction;

        private readonly Dictionary<Instruction, Instruction> _instructionMap =
            new Dictionary<Instruction, Instruction>();

        private readonly TypeResolver _typeResolver;

        private static bool CanInlineInstruction(Instruction instruction, int usages)
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
            //_moduleWeaver.WriteMessage($"Method {_method.Name} to {_parentMethod.Name}", MessageImportance.Normal);
            _pushInstructions = _callInstruction.GetArgumentPushInstructions();

            // parameters
            _parameters = new MethodParameters(_method);
            if (_parameters.Count != _pushInstructions.Length)
            {
                throw new NotSupportedException($"The number of parameters ({_parameters.Count}) is not equal to the number of push instructions ({_pushInstructions.Length})");
            }

            _args = new Arg[_pushInstructions.Length];

            _typeResolver = _callInstruction.Operand is MethodReference methodCallReference &&
                           methodCallReference is GenericInstanceMethod genericInstanceMethod
                ? new TypeResolver(genericInstanceMethod)
                : null;
        }

        public class MethodParameters
        {
            private readonly MethodDefinition _methodDefinition;
            private readonly bool _hasImplicitThis;
            public int Count { get; }

            public MethodParameters(MethodDefinition methodDefinition)
            {
                _methodDefinition = methodDefinition;
                _hasImplicitThis = _methodDefinition.HasThis && !_methodDefinition.ExplicitThis;
                Count = _methodDefinition.Parameters.Count + (_hasImplicitThis ? 1 : 0);
            }

            public ParameterDefinition this[int index]
            {
                get
                {
                    if (_hasImplicitThis)
                    {
                        if (index == 0)
                        {
                            return _methodDefinition.Body.ThisParameter;
                        }
                        index--;
                    }

                    return _methodDefinition.Parameters[index];
                }
            }
        }

        private class ArgStack
        {
            private readonly List<Arg> _argStack;

            public ArgStack(IEnumerable<Arg> args)
            {
                _argStack = new List<Arg>(args);
            }

            public Arg GetTop()
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

        private void CreateArgs()
        {
            FindSkipArgsReferences();

            for (var i = 0; i < _args.Length; i++)
            {
                _args[i] = new Arg(this, i, _pushInstructions[i], _args.Length);
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
            _instructionMap[instruction] = instruction.Next;
            _il.Remove(instruction);
        }

        private void InsertAfter(Instruction target, Instruction instruction)
        {
            _il.InsertAfter(target, instruction);
        }

        private void AppendToBody(Instruction instruction)
        {
            if (_firstBodyInstruction == null)
            {
                _firstBodyInstruction = instruction;
            }

            _il.InsertBefore(_callInstruction, instruction);
        }

        private void InsertBeforeBody(Instruction instruction)
        {
            var target = _firstBodyInstruction ?? _callInstruction;
            _instructionMap[target] = instruction;
            _il.InsertBefore(target, instruction);
            if (_beforeBodyInstruction == null)
            {
                _beforeBodyInstruction = instruction;
            }
        }

        private bool GetInstructionFromMap(Instruction instruction, out Instruction outInstruction)
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
                if (instruction.Operand is FieldReference opFieldReference)
                {
                    instruction.Operand = _moduleWeaver.ModuleDefinition.ImportReference(opFieldReference);
                }

                if (instruction.Operand is MethodReference opMethodReference)
                {
                    instruction.Operand = _moduleWeaver.ModuleDefinition.ImportReference(opMethodReference);
                }

                if (instruction.Operand is TypeReference opTypeReference)
                {
                    instruction.Operand = _moduleWeaver.ModuleDefinition.ImportReference(opTypeReference);
                }

                if (instruction.Operand is Instruction opInstruction)
                {
                    if (GetInstructionFromMap(opInstruction, out var newInstruction))
                    {
                        instruction.Operand = newInstruction;
                    }
                }
                else if (instruction.Operand is Instruction[] opInstructions)
                {
                    for (var index = 0; index < opInstructions.Length; index++)
                    {
                        if (GetInstructionFromMap(opInstructions[index], out var newInstruction))
                        {
                            opInstructions[index] = newInstruction;
                        }
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
                    if (diff < sbyte.MinValue || diff > sbyte.MaxValue)
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

        private IEnumerable<Instruction> GetReferencedInstructions(Instruction instruction)
        {
            while (instruction != null)
            {
                var nextInstruction = instruction.Next;

                if (instruction.Operand is Instruction opInstruction)
                {
                    yield return opInstruction;
                }
                else if (instruction.Operand is Instruction[] opInstructions)
                {
                    foreach (var innerOpInstruction in opInstructions)
                    {
                        yield return innerOpInstruction;
                    }
                }

                instruction = nextInstruction;
            }
        }

        private void FindSkipArgsReferences()
        {
            if (_pushInstructions.Length == 0)
            {
                return;
            }

            int closestOffsetBeforeCall = -1;
            foreach (var opInstruction in GetReferencedInstructions(_parentMethod.Body.Instructions.FirstOrDefault()))
            {
                if (opInstruction == _callInstruction)
                {
                    closestOffsetBeforeCall = opInstruction.Offset;
                    break;
                }

                if (opInstruction.Offset < _callInstruction.Offset && opInstruction.Offset > closestOffsetBeforeCall)
                {
                    closestOffsetBeforeCall = opInstruction.Offset;
                }
            }

            if (closestOffsetBeforeCall >= 0)
            {
                for (var i = 0; i < _pushInstructions.Length; i++)
                {
                    if (_pushInstructions[i] != null && closestOffsetBeforeCall > _pushInstructions[i].Offset)
                    {
                        _pushInstructions[i] = null;
                    }
                }
            }
        }

        private class Arg
        {
            private readonly InlineMethodWeaver _inlineMethodWeaver;
            private readonly int _paramIndex;
            private readonly Instruction _pushInstruction;
            private readonly int _count;
            private int _usages;
            private bool _onlyLoad = true;
            public int Usages => _usages;
            private ArgStrategy _strategy;
            public ArgStrategy Strategy => _strategy;
            private VariableDefinition _variableDefinition;
            public Instruction PushInstruction => _pushInstruction;
            private List<Instruction> _allPushInstructions;

            public Arg(InlineMethodWeaver inlineMethodWeaver, int paramIndex, Instruction pushInstruction, int count)
            {
                _inlineMethodWeaver = inlineMethodWeaver;
                _paramIndex = paramIndex;
                _pushInstruction = pushInstruction;
                _count = count;
            }

            public void TrackInstruction(Instruction instruction)
            {
                _usages++;
                if (OpCodeHelper.IsLoadArgA(instruction) || OpCodeHelper.IsStoreArg(instruction))
                {
                    _onlyLoad = false;
                }
            }

            private void CreateVariableDefinition()
            {
                _variableDefinition = new VariableDefinition(_inlineMethodWeaver._parameters[_paramIndex].ParameterType);
                _inlineMethodWeaver._parentMethod.Body.Variables.Add(_variableDefinition);
            }

            public bool CanKeepOnStack => _onlyLoad && (_usages == 1 || (_usages > 1 && CanDup));

            private bool IsLast => _count == _paramIndex + 1;

            private bool CanDup => (HasPush && _pushInstruction.GetPushCount() == 1) || IsLast;

            private void InsertConsumeTopArg(Instruction instruction)
            {
                var topArg = _inlineMethodWeaver._argStack.GetTop();
                if (topArg != this)
                {
                    throw new Exception($"Failed to reach argument from stack {_inlineMethodWeaver._method.Name} to {_inlineMethodWeaver._parentMethod.Name}");
                }

                _inlineMethodWeaver.InsertBeforeBody(instruction);
            }

            public void Finish()
            {
                // keep/dup arg push
                if (KeepOnStack)
                {
                    _strategy = ArgStrategy.KeepOnStack;
                    if (_usages > 1)
                    {
                        if (HasPush && _pushInstruction.GetPushCount() == 1)
                        {
                            _inlineMethodWeaver.InsertAfter(_pushInstruction, Instruction.Create(OpCodes.Dup));
                        }
                        else
                        {
                            InsertConsumeTopArg(Instruction.Create(OpCodes.Dup));
                            _inlineMethodWeaver._argStack.Push(this);
                        }
                    }
                    return;
                }

                // neutralize arg push
                if (CanInline || _usages == 0)
                {
                    _strategy = _usages == 0 ? ArgStrategy.None : ArgStrategy.Inline;
                    _allPushInstructions = GetAllPushInstructions();
                    if (CanRemovePush)
                    {
                        foreach (var instruction in _allPushInstructions)
                        {
                            _inlineMethodWeaver.Remove(instruction);
                        }
                    }
                    else
                    {
                        // neutralize push instruction
                        if (_pushInstruction == null)
                        {
                            InsertConsumeTopArg(Instruction.Create(OpCodes.Pop));
                        }
                        else
                        {
                            for (var i = 0; i < _pushInstruction.GetPushCount(); i++)
                            {
                                _inlineMethodWeaver.InsertAfter(_pushInstruction, Instruction.Create(OpCodes.Pop));
                            }
                        }
                    }

                    _inlineMethodWeaver._argStack.Consume(this);
                    return;
                }

                _strategy = ArgStrategy.Variable;
                CreateVariableDefinition();

                // place store loc
                var storeLoc = OpCodeHelper.CreateStoreLoc(_variableDefinition);
                if (_pushInstruction == null)
                {
                    InsertConsumeTopArg(storeLoc);
                }
                else
                {
                    _inlineMethodWeaver.InsertAfter(_pushInstruction, storeLoc);
                }

                _inlineMethodWeaver._argStack.Consume(this);
            }

            private List<Instruction> GetAllPushInstructions()
            {
                if (_pushInstruction == null)
                {
                    return new List<Instruction>();
                }

                var instructions = new List<Instruction>();
                var depth = 0;
                var instruction = _pushInstruction;
                do
                {
                    instructions.Insert(0, instruction);

                    depth += instruction.GetPushCount();
                    depth -= instruction.GetPopCount();

                    instruction = instruction.Previous;
                } while (depth != 1);

                return instructions;
            }

            private IEnumerable<Instruction> GetLdInstructions()
            {
                switch (_strategy)
                {
                    case ArgStrategy.Variable:
                        yield return OpCodeHelper.CreateLoadLoc(_variableDefinition);
                        break;
                    case ArgStrategy.Inline:
                        foreach (var instruction in _allPushInstructions)
                        {
                            yield return OpCodeHelper.Clone(instruction);
                        }
                        break;
                    case ArgStrategy.KeepOnStack:
                        break;
                }
            }

            private bool CanRemovePush => _pushInstruction != null && (_pushInstruction.OpCode.StackBehaviourPop == StackBehaviour.Pop0 || CanInline);

            private bool CanInline => _onlyLoad && CanInlineInstruction(_pushInstruction, _usages);

            public bool HasPush => _pushInstruction != null;
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
                    yield return OpCodeHelper.CreateLoadLocA(_variableDefinition);
                } else if (OpCodeHelper.IsStoreArg(instruction))
                {
                    yield return OpCodeHelper.CreateStoreLoc(_variableDefinition);
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
                                if (last != null && last.Popped > 0)
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
        }

        private void AnalyzeKeepOnStack(List<LoadArgInfo> firstLoadArgs)
        {
            int currentIndex = 0;
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
            var callSequencePoint = _parentMethod.DebugInformation.HasSequencePoints ? _parentMethod.DebugInformation.GetSequencePoint(_callInstruction) : null;
            CreateVars();
            CreateArgs();
            AnalyzeArgs();

            var innerVariables = _method.Body.Variables;
            var parentVariables = _parentMethod.Body.Variables;

            // inline body
            var instructions = _method.Body.Instructions;
            foreach (var instruction in instructions)
            {
                var nextInstruction = instruction.Next;
                Instruction newInstruction = null;

                // arg
                var parameterDefinition = OpCodeHelper.GetArgParameterDefinition(instruction, _parameters);
                if (parameterDefinition != null)
                {
                    var arg = _args[parameterDefinition.Sequence];
                    bool isFirst = true;
                    foreach (var argInstruction in arg.GetInstructions(instruction))
                    {
                        if (isFirst)
                        {
                            _instructionMap[instruction] = argInstruction;
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
                if (instruction.OpCode.OperandType == OperandType.InlineBrTarget ||
                    instruction.OpCode.OperandType == OperandType.ShortInlineBrTarget)
                {
                    var target = (Instruction) instruction.Operand;
                    // fix target ret
                    if (target?.OpCode.Code == Code.Ret)
                    {
                        newInstruction = Instruction.Create(instruction.OpCode, _callInstruction.Next);
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

                    newInstruction = Instruction.Create(OpCodes.Br, _callInstruction.Next);
                }

                // resolve generic calls
                if (_typeResolver != null && instruction.Operand is MethodReference calledMethod && calledMethod.ContainsGenericParameter)
                {
                    newInstruction = Instruction.Create(instruction.OpCode, _typeResolver.Resolve(calledMethod));
                }

                // resolve generic types
                if (_typeResolver != null && instruction.Operand is TypeReference typeReference)
                {
                    newInstruction = Instruction.Create(instruction.OpCode, _typeResolver.Resolve(typeReference));
                }

                if (newInstruction == null)
                {
                    newInstruction = OpCodeHelper.Clone(instruction);
                }

                _instructionMap[instruction] = newInstruction;
                AppendToBody(newInstruction);
            }

            Remove(_callInstruction);

            // replace call target
            if (_firstBodyInstruction != null || _beforeBodyInstruction != null)
            {
                _instructionMap[_callInstruction] = _beforeBodyInstruction ?? _firstBodyInstruction;
            }

            FixInstructions();

            if (_parentMethod.DebugInformation.HasSequencePoints && _firstBodyInstruction != null && callSequencePoint != null)
            {
                var sequencePoints = _parentMethod.DebugInformation.SequencePoints;
                int indexOf = sequencePoints.Count;
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
        }
    }
}
