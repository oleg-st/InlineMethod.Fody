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
}
