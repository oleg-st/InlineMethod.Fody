#nullable disable
// ReSharper disable All 
using System;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace InlineMethod.Fody.Helper.Cecil
{
	/**
	 * Source
	 * https://github.com/jbevain/cecil/blob/master/Mono.Cecil/GenericParameterResolver.cs
	 */
	internal sealed class GenericParameterResolver
	{
		internal static TypeReference ResolveReturnTypeIfNeeded(MethodReference methodReference)
		{
			if (methodReference.DeclaringType.IsArray && methodReference.Name == "Get")
				return methodReference.ReturnType;

			var genericInstanceMethod = methodReference as GenericInstanceMethod;
			var declaringGenericInstanceType = methodReference.DeclaringType as GenericInstanceType;

			if (genericInstanceMethod == null && declaringGenericInstanceType == null)
				return methodReference.ReturnType;

			return ResolveIfNeeded(genericInstanceMethod, declaringGenericInstanceType, methodReference.ReturnType);
		}

		internal static TypeReference ResolveFieldTypeIfNeeded(FieldReference fieldReference)
		{
			return ResolveIfNeeded(null, fieldReference.DeclaringType as GenericInstanceType, fieldReference.FieldType);
		}

		internal static TypeReference ResolveParameterTypeIfNeeded(MethodReference method, ParameterReference parameter)
		{
			var genericInstanceMethod = method as GenericInstanceMethod;
			var declaringGenericInstanceType = method.DeclaringType as GenericInstanceType;

			if (genericInstanceMethod == null && declaringGenericInstanceType == null)
				return parameter.ParameterType;

			return ResolveIfNeeded(genericInstanceMethod, declaringGenericInstanceType, parameter.ParameterType);
		}

		internal static TypeReference ResolveVariableTypeIfNeeded(MethodReference method, VariableReference variable)
		{
			var genericInstanceMethod = method as GenericInstanceMethod;
			var declaringGenericInstanceType = method.DeclaringType as GenericInstanceType;

			if (genericInstanceMethod == null && declaringGenericInstanceType == null)
				return variable.VariableType;

			return ResolveIfNeeded(genericInstanceMethod, declaringGenericInstanceType, variable.VariableType);
		}

		private static TypeReference ResolveIfNeeded(IGenericInstance genericInstanceMethod, IGenericInstance declaringGenericInstanceType, TypeReference parameterType)
		{
            if (parameterType is ByReferenceType byRefType)
				return ResolveIfNeeded(genericInstanceMethod, declaringGenericInstanceType, byRefType);

            if (parameterType is ArrayType arrayType)
				return ResolveIfNeeded(genericInstanceMethod, declaringGenericInstanceType, arrayType);

            if (parameterType is GenericInstanceType genericInstanceType)
				return ResolveIfNeeded(genericInstanceMethod, declaringGenericInstanceType, genericInstanceType);

            if (parameterType is GenericParameter genericParameter)
				return ResolveIfNeeded(genericInstanceMethod, declaringGenericInstanceType, genericParameter);

            if (parameterType is RequiredModifierType requiredModifierType && ContainsGenericParameters(requiredModifierType))
				return ResolveIfNeeded(genericInstanceMethod, declaringGenericInstanceType, requiredModifierType.ElementType);

			if (ContainsGenericParameters(parameterType))
				throw new Exception("Unexpected generic parameter.");

			return parameterType;
		}

		private static TypeReference ResolveIfNeeded(IGenericInstance genericInstanceMethod, IGenericInstance genericInstanceType, GenericParameter genericParameterElement)
		{
			return (genericParameterElement.MetadataType == MetadataType.MVar)
				? (genericInstanceMethod != null ? genericInstanceMethod.GenericArguments[genericParameterElement.Position] : genericParameterElement)
				: genericInstanceType.GenericArguments[genericParameterElement.Position];
		}

		private static ArrayType ResolveIfNeeded(IGenericInstance genericInstanceMethod, IGenericInstance genericInstanceType, ArrayType arrayType)
		{
			return new ArrayType(ResolveIfNeeded(genericInstanceMethod, genericInstanceType, arrayType.ElementType), arrayType.Rank);
		}

		private static ByReferenceType ResolveIfNeeded(IGenericInstance genericInstanceMethod, IGenericInstance genericInstanceType, ByReferenceType byReferenceType)
		{
			return new ByReferenceType(ResolveIfNeeded(genericInstanceMethod, genericInstanceType, byReferenceType.ElementType));
		}

		private static GenericInstanceType ResolveIfNeeded(IGenericInstance genericInstanceMethod, IGenericInstance genericInstanceType, GenericInstanceType genericInstanceType1)
		{
			if (!ContainsGenericParameters(genericInstanceType1))
				return genericInstanceType1;

			var newGenericInstance = new GenericInstanceType(genericInstanceType1.ElementType);

			foreach (var genericArgument in genericInstanceType1.GenericArguments)
			{
				if (!genericArgument.IsGenericParameter)
				{
					newGenericInstance.GenericArguments.Add(ResolveIfNeeded(genericInstanceMethod, genericInstanceType, genericArgument));
					continue;
				}

				var genParam = (GenericParameter)genericArgument;

				switch (genParam.Type)
				{
					case GenericParameterType.Type:
						{
							if (genericInstanceType == null)
								throw new NotSupportedException();

							newGenericInstance.GenericArguments.Add(genericInstanceType.GenericArguments[genParam.Position]);
						}
						break;

					case GenericParameterType.Method:
						{
							if (genericInstanceMethod == null)
								newGenericInstance.GenericArguments.Add(genParam);
							else
								newGenericInstance.GenericArguments.Add(genericInstanceMethod.GenericArguments[genParam.Position]);
						}
						break;
				}
			}

			return newGenericInstance;
		}

		private static bool ContainsGenericParameters(TypeReference typeReference)
		{
			var genericParameter = typeReference as GenericParameter;
			if (genericParameter != null)
				return true;

            if (typeReference is ArrayType arrayType)
				return ContainsGenericParameters(arrayType.ElementType);

            if (typeReference is PointerType pointerType)
				return ContainsGenericParameters(pointerType.ElementType);

            if (typeReference is ByReferenceType byRefType)
				return ContainsGenericParameters(byRefType.ElementType);

            if (typeReference is SentinelType sentinelType)
				return ContainsGenericParameters(sentinelType.ElementType);

            if (typeReference is PinnedType pinnedType)
				return ContainsGenericParameters(pinnedType.ElementType);

            if (typeReference is RequiredModifierType requiredModifierType)
				return ContainsGenericParameters(requiredModifierType.ElementType);

            if (typeReference is GenericInstanceType genericInstance)
			{
				foreach (var genericArgument in genericInstance.GenericArguments)
				{
					if (ContainsGenericParameters(genericArgument))
						return true;
				}

				return false;
			}

			if (typeReference is TypeSpecification)
				throw new NotSupportedException();

			return false;
		}
	}
}
