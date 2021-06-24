using System;
using System.Collections.Generic;
using System.Linq;
using InlineMethod.Fody.Extensions;
using InlineMethod.Fody.Helper;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace InlineMethod.Fody
{
    public class InlineMethodWeaver
    {
        private readonly ModuleWeaver _moduleWeaver;
        private readonly Instruction _callInstruction;
        private readonly MethodDefinition _parentMethod;
        private readonly MethodDefinition _method;
        private readonly ILProcessor _il;
        private readonly Instruction[] _pushInstructions;
        private readonly Arg[] _args;
        private readonly MethodParameters _parameters;
        private int _firstInnerVariableIndex;
        private ArgStack _argStack;
        private Instruction _firstBodyInstruction;
        private Instruction _beforeBodyInstruction;
        private readonly List<LoadArgInfo> _firstLoadArgs;

        private class LoadArgInfo
        {
            public int Index { get; }
            public int InstructionIndex { get; }

            public LoadArgInfo(int index, int instructionIndex)
            {
                Index = index;
                InstructionIndex = instructionIndex;
            }
        }

        private readonly Dictionary<Instruction, Instruction> _instructionMap =
            new Dictionary<Instruction, Instruction>();

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
            _firstLoadArgs = new List<LoadArgInfo>(_parameters.Count);
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
        }

        private void CreateArgs()
        {
            FindSkipArgsReferences();

            for (var i = 0; i < _args.Length; i++)
            {
                _args[i] = new Arg(this, i, _pushInstructions[i]);
            }

            _argStack = new ArgStack(_args);
        }

        private void FinishArgs()
        {
            // try to keep some args on stack
            // todo: check targets and skip
            var prevIndex = -1;
            foreach (var loadArg in _firstLoadArgs)
            {
                var loadArgIndex = loadArg.Index;
                var arg = _args[loadArgIndex];
                if (!arg.IsDeferred || prevIndex >= loadArgIndex)
                {
                    break;
                }

                arg.KeepOnStack = true;
                prevIndex = loadArgIndex;
            }

            if (!CheckKeepStack())
            {
                foreach (var arg in _args)
                {
                    arg.KeepOnStack = false;
                }
            }

            for (var index = _args.Length - 1; index >= 0; index--)
            {
                var arg = _args[index];
                arg.Finish();
            }
        }

        private bool CheckKeepStack()
        {
            var argStack = new ArgStack(_args);

            for (var index = _args.Length - 1; index >= 0; index--)
            {
                var arg = _args[index];

                if (arg.KeepOnStack)
                {
                    continue;
                }

                if (!arg.HasPush)
                {
                    var topArg = argStack.GetTop();
                    if (topArg != arg)
                    {
                        return false;
                    }
                }

                argStack.Consume(arg);
            }

            return true;
        }

        private class Arg
        {
            private readonly InlineMethodWeaver _inlineMethodWeaver;
            private readonly int _paramIndex;
            private VariableDefinition _variableDefinition;
            private readonly Instruction _pushInstruction;
            private int _usages;
            private Instruction _deferredInstruction;
            public bool KeepOnStack { get; set; }

            public bool HasPush => _pushInstruction != null;

            public bool IsDeferred => _deferredInstruction != null;

            public Arg(InlineMethodWeaver inlineMethodWeaver, int paramIndex, Instruction pushInstruction)
            {
                _inlineMethodWeaver = inlineMethodWeaver;
                _paramIndex = paramIndex;
                _pushInstruction = pushInstruction;
            }

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
                if (_deferredInstruction != null)
                {
                    // keep on stack
                    if (KeepOnStack)
                    {
                        _inlineMethodWeaver.Remove(_deferredInstruction);
                        _deferredInstruction = null;
                        return;
                    }
                }

                ProcessDeferred();
                if (_variableDefinition == null)
                {
                    if (CanRemovePush)
                    {
                        _inlineMethodWeaver.Remove(_pushInstruction);
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
                }
                else
                {
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
                }

                _inlineMethodWeaver._argStack.Consume(this);
            }

            // no pops, can be removed safely
            private bool CanRemovePush => _pushInstruction != null && _pushInstruction.OpCode.StackBehaviourPop == StackBehaviour.Pop0;

            // inline push
            private bool CanInline => _pushInstruction != null && _variableDefinition == null && 
                                      (OpCodeHelper.IsLoadConst(_pushInstruction) || OpCodeHelper.IsLoadArg(_pushInstruction) || OpCodeHelper.IsLoadLoc(_pushInstruction));

            private readonly List<Instruction> _inlinedInstructions = new List<Instruction>();

            private VariableDefinition GetVariableDefinition()
            {
                if (_variableDefinition == null)
                {
                    _variableDefinition = new VariableDefinition(_inlineMethodWeaver._parameters[_paramIndex].ParameterType);
                    _inlineMethodWeaver._parentMethod.Body.Variables.Add(_variableDefinition);

                    // revert inline
                    if (_inlinedInstructions.Count > 0)
                    {
                        foreach (var inlinedInstruction in _inlinedInstructions)
                        {
                            OpCodeHelper.ReplaceInstruction(inlinedInstruction, OpCodeHelper.CreateLoadLoc(_variableDefinition));
                        }
                        _inlinedInstructions.Clear();
                    }
                }

                return _variableDefinition;
            }

            private void ProcessDeferred()
            {
                if (_deferredInstruction != null)
                {
                    OpCodeHelper.ReplaceInstruction(_deferredInstruction, GetLdArgInstruction());
                    _deferredInstruction = null;
                }
            }

            private Instruction Defer()
            {
                _deferredInstruction = Instruction.Create(OpCodes.Nop);
                return _deferredInstruction;
            }

            private Instruction GetLdArgInstruction()
            {
                if (CanInline)
                {
                    var ldArgInstruction = OpCodeHelper.Clone(_pushInstruction);
                    _inlinedInstructions.Add(ldArgInstruction);
                    return ldArgInstruction;
                }
                return OpCodeHelper.CreateLoadLoc(GetVariableDefinition());
            }

            public Instruction GetInstruction(Instruction instruction)
            {
                _usages++;
                if (OpCodeHelper.IsLoadArg(instruction))
                {
                    if (_usages == 1)
                    {
                        return Defer();
                    }

                    ProcessDeferred();
                    return GetLdArgInstruction();
                }

                ProcessDeferred();
                if (OpCodeHelper.IsLoadArgA(instruction))
                {
                    return OpCodeHelper.CreateLoadLocA(GetVariableDefinition());
                }

                if (OpCodeHelper.IsStoreArg(instruction))
                {
                    return OpCodeHelper.CreateStoreLoc(GetVariableDefinition());
                }

                throw new NotSupportedException($"Unknown arg instruction {instruction.OpCode}");
            }
        }

        private void CreateVars()
        {
            var variables = _parentMethod.Body.Variables;
            _firstInnerVariableIndex = variables.Count;
            // add inner variables to parent
            foreach (var var in _method.Body.Variables)
            {
                variables.Add(new VariableDefinition(var.VariableType));
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
            _il.InsertBefore( _firstBodyInstruction ?? _callInstruction, instruction);
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
            while (instruction != null)
            {
                var nextInstruction = instruction.Next;

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

                // extend short branch to long to avoid short operand overflow, TODO: choose short / long instruction
                OpCodeHelper.ExtendBranchOpCode(instruction);
                // extend short variable instructions to long if needed
                OpCodeHelper.ExtendVariableOpCode(instruction);

                instruction = nextInstruction;
            }
        }

        private IEnumerable<Instruction> GetReferencedInstructions()
        {
            var instruction = _parentMethod.Body.Instructions.FirstOrDefault();
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
            foreach (var opInstruction in GetReferencedInstructions())
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

        public void Process()
        {
            CreateVars();
            CreateArgs();

            var innerVariables = _method.Body.Variables;
            var parentVariables = _parentMethod.Body.Variables;

            var isLoadArgs = true;

            // inline body
            var instructions = _method.Body.Instructions;
            for (int instructionIndex = 0; instructionIndex < instructions.Count; instructionIndex++)
            {
                var instruction = instructions[instructionIndex];
                var nextInstruction = instruction.Next;
                Instruction newInstruction = null;

                // arg
                var parameterDefinition = OpCodeHelper.GetArgParameterDefinition(instruction, _parameters);
                if (parameterDefinition != null)
                {
                    var arg = _args[parameterDefinition.Sequence];
                    newInstruction = arg.GetInstruction(instruction);
                    if (isLoadArgs)
                    {
                        if (OpCodeHelper.IsLoadArg(instruction) && arg.IsDeferred)
                        {
                            _firstLoadArgs.Add(new LoadArgInfo(parameterDefinition.Sequence, instructionIndex));
                        }
                        else
                        {
                            isLoadArgs = false;
                        }
                    }
                }
                else
                {
                    if (isLoadArgs && instruction.OpCode != OpCodes.Nop)
                    {
                        isLoadArgs = false;
                    }
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

                if (newInstruction == null)
                {
                    newInstruction = OpCodeHelper.Clone(instruction);
                }

                _instructionMap[instruction] = newInstruction;
                AppendToBody(newInstruction);
            }

            FinishArgs();
            Remove(_callInstruction);

            // replace call target
            if (_firstBodyInstruction != null || _beforeBodyInstruction != null)
            {
                _instructionMap[_callInstruction] = _beforeBodyInstruction ?? _firstBodyInstruction;
            }

            FixInstructions();
        }
    }
}
