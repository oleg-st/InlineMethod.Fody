using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace InlineMethod.Fody.Helper;

// Analyze before weaving changes the cache expressions. Names and attributes alone
// cannot distinguish a lambda cache from an ordinary mutable field.
internal sealed class DelegateCacheFields
{
    public static DelegateCacheFields Empty { get; } = new(null, []);

    private readonly ModuleDefinition? _module;
    private readonly Dictionary<(string Owner, string Name), FieldDefinition> _fields;

    private DelegateCacheFields(ModuleDefinition? module, Dictionary<(string Owner, string Name), FieldDefinition> fields)
    {
        _module = module;
        _fields = fields;
    }

    public bool Contains(FieldReference field)
        => _module != null && FindCandidate(field, _module, _fields) != null;

    public static DelegateCacheFields Analyze(ModuleDefinition module)
    {
        var holders = module.GetTypes().Select(TryCreateHolder).OfType<Holder>().ToArray();
        var candidates = holders.SelectMany(holder => holder.Uses.Keys)
            .ToDictionary(field => (field.DeclaringType.FullName, field.Name));
        if (candidates.Count == 0)
        {
            return Empty;
        }

        var owners = holders.ToDictionary(holder => holder.Type);
        var targets = new Dictionary<MethodDefinition, HashSet<Instruction>>();
        foreach (var method in module.GetTypes().SelectMany(type => type.Methods).Where(method => method.HasBody))
        {
            foreach (var instruction in method.Body.Instructions)
            {
                if (instruction.Operand is FieldReference reference &&
                    FindCandidate(reference, module, candidates) is { } field)
                {
                    owners[field.DeclaringType].Uses[field].Add((method, instruction));
                }
            }
        }

        var approved = new Dictionary<(string Owner, string Name), FieldDefinition>();
        foreach (var holder in holders)
        {
            if (IsValid(holder, targets))
            {
                foreach (var field in holder.Uses.Keys)
                    approved.Add((holder.Type.FullName, field.Name), field);
            }
        }

        return new DelegateCacheFields(module, approved);
    }

    private sealed class Holder(TypeDefinition type, FieldDefinition singleton, Instruction singletonStore)
    {
        public TypeDefinition Type { get; } = type;
        public FieldDefinition Singleton { get; } = singleton;
        public Instruction SingletonStore { get; } = singletonStore;
        public Dictionary<FieldDefinition, List<(MethodDefinition Method, Instruction Instruction)>> Uses { get; }
            = type.Fields.ToDictionary(field => field, _ => new List<(MethodDefinition, Instruction)>());
    }

    private static Holder? TryCreateHolder(TypeDefinition type)
    {
        if (!type.IsNestedPrivate || !type.IsSealed || !type.IsBeforeFieldInit ||
            type.BaseType?.MetadataType != MetadataType.Object || !TypeHelper.IsCompilerGenerated(type) ||
            type.Methods.Any(method => method.Name == "Finalize" || method.Overrides.Any(overridden => overridden.Name == "Finalize")) ||
            type.Fields.Any(field => field.CustomAttributes.Any(attribute =>
                attribute.AttributeType.FullName is "System.ThreadStaticAttribute" or "System.ContextStaticAttribute")) ||
            type.Fields.Select(field => field.Name).Distinct().Count() != type.Fields.Count)
        {
            return null;
        }

        var initializers = type.Methods.Where(method => method.IsConstructor && method.IsStatic).ToArray();
        if (initializers.Length != 1 || !initializers[0].HasBody || initializers[0].Body.HasExceptionHandlers)
        {
            return null;
        }

        var body = initializers[0].Body.Instructions.Where(instruction => instruction.OpCode.Code != Code.Nop).ToArray();
        if (body.Length != 3 || body[0].OpCode.Code != Code.Newobj || body[1].OpCode.Code != Code.Stsfld ||
            body[2].OpCode.Code != Code.Ret || body[0].Operand is not MethodReference constructor ||
            body[1].Operand is not FieldReference singletonReference ||
            singletonReference.DeclaringType.GetElementType().Scope != type.Module ||
            singletonReference.Resolve() is not {IsStatic: true, IsInitOnly: true} singleton ||
            singleton.DeclaringType != type || !SameType(constructor.DeclaringType, singletonReference.DeclaringType) ||
            !SameFieldType(constructor.DeclaringType, singletonReference) || constructor.Parameters.Count != 0)
        {
            return null;
        }

        var instanceConstructor = constructor.Resolve();
        if (instanceConstructor is not {IsConstructor: true, IsStatic: false, HasBody: true} || instanceConstructor.DeclaringType != type ||
            instanceConstructor.Body.HasExceptionHandlers)
        {
            return null;
        }

        var instanceBody = instanceConstructor.Body.Instructions.Where(instruction => instruction.OpCode.Code != Code.Nop).ToArray();
        if (instanceBody.Length != 3 || instanceBody[0].OpCode.Code != Code.Ldarg_0 ||
            instanceBody[1].OpCode.Code != Code.Call || instanceBody[2].OpCode.Code != Code.Ret ||
            instanceBody[1].Operand is not MethodReference {Name: ".ctor", HasThis: true, Parameters.Count: 0} baseConstructor ||
            baseConstructor.DeclaringType.MetadataType != MetadataType.Object)
        {
            return null;
        }

        // The initializer must have no user state or other initialization work.
        if (type.Fields.Any(field => field != singleton &&
                (!field.IsStatic || field.IsInitOnly || !IsDelegate(field.FieldType))))
        {
            return null;
        }

        return new Holder(type, singleton, body[1]);
    }

    private static bool IsValid(Holder holder, Dictionary<MethodDefinition, HashSet<Instruction>> targets)
    {
        var singletonUses = new HashSet<Instruction> {holder.SingletonStore};
        foreach (var pair in holder.Uses.Where(pair => pair.Key != holder.Singleton))
        {
            // Roslyn emits one guarded load and one store per lambda cache. Any
            // further load, assignment, address, or token makes it an unknown field.
            if (pair.Value.Count != 2)
            {
                return false;
            }

            var (method, load) = pair.Value[0];
            var (storeMethod, store) = pair.Value[1];
            if (method != storeMethod || GetGuardedSingletonLoad(load, store) is not { } singletonLoad ||
                !IsDelegateConstruction(holder, (FieldReference)load.Operand, singletonLoad))
            {
                return false;
            }

            if (!targets.TryGetValue(method, out var incoming))
                targets.Add(method, incoming = GetTargets(method));
            for (var instruction = load.Next; instruction != store.Next; instruction = instruction.Next)
                if (incoming.Contains(instruction))
                    return false;

            singletonUses.Add(singletonLoad);
        }

        return holder.Uses[holder.Singleton].All(use => singletonUses.Contains(use.Instruction));
    }

    private static Instruction? GetGuardedSingletonLoad(Instruction load, Instruction store)
    {
        // ldsfld cache; dup; brtrue continuation; pop;
        // ldsfld singleton; ldftn target; newobj delegate; dup; stsfld cache
        var duplicate = load.Next;
        var branch = duplicate?.Next;
        var discard = branch?.Next;
        var singletonLoad = discard?.Next;
        var function = singletonLoad?.Next;
        var construct = function?.Next;
        var duplicateDelegate = construct?.Next;
        return load.OpCode.Code == Code.Ldsfld && store.OpCode.Code == Code.Stsfld &&
               load.Operand is FieldReference cacheLoad && store.Operand is FieldReference cacheStore &&
               SameType(cacheLoad.DeclaringType, cacheStore.DeclaringType) &&
               duplicate?.OpCode.Code == Code.Dup && branch?.OpCode.Code is Code.Brtrue or Code.Brtrue_S &&
               store.Next != null && branch.Operand == store.Next && discard?.OpCode.Code == Code.Pop &&
               singletonLoad?.OpCode.Code == Code.Ldsfld && function?.OpCode.Code == Code.Ldftn &&
               construct?.OpCode.Code == Code.Newobj && duplicateDelegate?.OpCode.Code == Code.Dup &&
               duplicateDelegate.Next == store ? singletonLoad : null;
    }

    private static bool IsDelegateConstruction(Holder holder, FieldReference cache, Instruction singletonLoad)
    {
        if (singletonLoad.Operand is not FieldReference singleton ||
            singleton.Name != holder.Singleton.Name || !SameType(cache.DeclaringType, singleton.DeclaringType) ||
            singleton.Resolve() != holder.Singleton ||
            singletonLoad.Next.Operand is not MethodReference target ||
            !SameType(cache.DeclaringType, target.DeclaringType) ||
            target is GenericInstanceMethod || target.HasGenericParameters || !target.HasThis ||
            singletonLoad.Next.Next.Operand is not MethodReference {Name: ".ctor", HasThis: true, Parameters.Count: 2} constructor ||
            constructor.Parameters[0].ParameterType.MetadataType != MetadataType.Object ||
            constructor.Parameters[1].ParameterType.MetadataType != MetadataType.IntPtr ||
            !SameFieldType(constructor.DeclaringType, cache))
        {
            return false;
        }

        return target.Resolve() is {HasBody: true, IsStatic: false, IsConstructor: false, IsVirtual: false} method &&
               method.DeclaringType == holder.Type;
    }

    private static HashSet<Instruction> GetTargets(MethodDefinition method)
    {
        var result = new HashSet<Instruction>();
        foreach (var instruction in method.Body.Instructions)
        {
            if (instruction.Operand is Instruction target)
                result.Add(target);
            else if (instruction.Operand is Instruction[] branches)
                result.UnionWith(branches);
        }
        foreach (var handler in method.Body.ExceptionHandlers)
        {
            foreach (var boundary in new[] {handler.TryStart, handler.TryEnd, handler.HandlerStart, handler.HandlerEnd, handler.FilterStart})
                if (boundary != null)
                    result.Add(boundary);
        }
        return result;
    }

    private static FieldDefinition? FindCandidate(FieldReference field, ModuleDefinition module,
        Dictionary<(string Owner, string Name), FieldDefinition> candidates)
    {
        var owner = field.DeclaringType.GetElementType();
        return owner.Scope == module && candidates.TryGetValue((owner.FullName, field.Name), out var candidate) &&
               field.Resolve() == candidate ? candidate : null;
    }

    private static bool IsDelegate(TypeReference type)
    {
        if (type is TypeSpecification and not GenericInstanceType)
            return false;
        try
        {
            return type.Resolve()?.BaseType?.FullName == "System.MulticastDelegate";
        }
        catch (AssemblyResolutionException)
        {
            return false;
        }
    }

    private static bool SameFieldType(TypeReference actual, FieldReference field)
        => SameType(actual, field.FieldType, field.DeclaringType as GenericInstanceType);

    private static bool SameType(TypeReference left, TypeReference right, GenericInstanceType? fieldOwner = null)
    {
        // Field signatures use the holder's type parameters, while the delegate
        // constructor uses the instantiated types. Substitute only on that side.
        if (fieldOwner != null && right is GenericParameter {Type: GenericParameterType.Type} parameter)
            return parameter.Position >= 0 && parameter.Position < fieldOwner.GenericArguments.Count &&
                   SameType(left, fieldOwner.GenericArguments[parameter.Position]);

        if (left is GenericParameter || right is GenericParameter)
            return left is GenericParameter leftParameter && right is GenericParameter rightParameter &&
                   leftParameter.Type == rightParameter.Type && leftParameter.Position == rightParameter.Position &&
                   leftParameter.Owner == rightParameter.Owner;
        if (left is GenericInstanceType leftInstance && right is GenericInstanceType rightInstance)
            return SameType(leftInstance.ElementType, rightInstance.ElementType) &&
                   leftInstance.GenericArguments.Count == rightInstance.GenericArguments.Count &&
                   leftInstance.GenericArguments.Zip(rightInstance.GenericArguments,
                       (actual, expected) => SameType(actual, expected, fieldOwner)).All(equal => equal);
        // Unrecognized specifications, such as function pointers and modifiers,
        // are not cache evidence and must not reach the generic resolver.
        return left is not TypeSpecification && right is not TypeSpecification &&
               left.FullName == right.FullName && left.Scope == right.Scope;
    }
}
