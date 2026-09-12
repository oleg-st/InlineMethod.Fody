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

    // a lambda cache field: a static field of a sealed compiler-generated closure type holding a
    // delegate or another compiler-generated instance. The compiler emits a single store of a
    // single value for such a field, so the store gives the value of every load of it
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
}
