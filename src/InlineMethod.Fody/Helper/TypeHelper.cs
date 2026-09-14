using Mono.Cecil;
using System.Collections.Generic;
using System.Linq;

namespace InlineMethod.Fody.Helper;

public static class TypeHelper
{
    public static IEnumerable<TypeReference> GetBaseTypes(TypeReference type)
    {
        while (true)
        {
            var baseType = type.Resolve().BaseType;
            if (baseType != null)
            {
                yield return baseType;
                type = baseType;
                continue;
            }

            break;
        }
    }

    public static bool IsDelegateType(TypeReference type) =>
        GetBaseTypes(type).Any(t => t.FullName == typeof(System.Delegate).ToString());

    public static bool IsCompilerGenerated(TypeReference type) =>
        type.Resolve().CustomAttributes.Any(a =>
            a.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");

    // Looks like a lambda cache field: a static field of a sealed compiler-generated closure type
    // holding a delegate or another compiler-generated instance. Accessing one has no effect a
    // caller can observe, so the access can be moved or dropped. Deliberately loose: a field that
    // wrongly matches here only costs an optimization, it cannot authorize a wrong one. Inferring
    // a field's value from its stores needs IsSingleAssignmentCacheField instead.
    public static bool IsCompilerGeneratedCacheField(FieldReference fieldReference)
    {
        var declaringType = fieldReference.DeclaringType.Resolve();
        if (declaringType is not {IsSealed: true} || !IsCompilerGenerated(declaringType))
        {
            return false;
        }

        var fieldType = fieldReference.FieldType.Resolve();
        return fieldType != null && (IsDelegateType(fieldType) || IsCompilerGenerated(fieldType));
    }

    // A cache field the compiler emits a single store of a single value for, so that store gives
    // the value of every load that observes one, whichever store actually ran.
    //
    // The shape checked by IsCompilerGeneratedCacheField does not establish that on its own: a
    // hand-written sealed [CompilerGenerated] class with a mutable static delegate field matches
    // it while staying assignable from anywhere. The name is what establishes it. '<' cannot
    // appear in a C# identifier, so a field named this way cannot have been declared in source -
    // only a compiler can emit it, and it is the compiler's own single-assignment cache.
    public static bool IsSingleAssignmentCacheField(FieldDefinition? fieldDefinition)
        => fieldDefinition is {IsStatic: true, Name.Length: > 0} &&
           fieldDefinition.Name[0] == '<' &&
           IsCompilerGeneratedCacheField(fieldDefinition);
}
