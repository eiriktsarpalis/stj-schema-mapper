﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace JsonSchemaMapper;

#if EXPOSE_JSON_SCHEMA_MAPPER
public
#else
    internal
#endif
    static partial class JsonSchemaMapper
{
    // Uses reflection to determine the element type of an enumerable or dictionary type
    // Workaround for https://github.com/dotnet/runtime/issues/77306#issuecomment-2007887560
    private static Type GetElementType(JsonTypeInfo typeInfo)
    {
        Debug.Assert(typeInfo.Kind is JsonTypeInfoKind.Enumerable or JsonTypeInfoKind.Dictionary);
        return (Type)typeof(JsonTypeInfo).GetProperty("ElementType", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(typeInfo)!;
    }

    // The source generator currently doesn't populate attribute providers for properties
    // cf. https://github.com/dotnet/runtime/issues/100095
    // Work around the issue by running a query for the relevant MemberInfo using the internal MemberName property
    // https://github.com/dotnet/runtime/blob/de774ff9ee1a2c06663ab35be34b755cd8d29731/src/libraries/System.Text.Json/src/System/Text/Json/Serialization/Metadata/JsonPropertyInfo.cs#L206
    [SuppressMessage("Trimming", "IL2075:'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.",
        Justification = "Members that already part of the source generated contract will not have been trimmed away.")]
    private static ICustomAttributeProvider? ResolveAttributeProvider(JsonTypeInfo typeInfo, JsonPropertyInfo propertyInfo)
    {
        if (propertyInfo.AttributeProvider is { } provider)
        {
            return provider;
        }

        PropertyInfo memberNameProperty = typeof(JsonPropertyInfo).GetProperty("MemberName", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var memberName = (string?)memberNameProperty.GetValue(propertyInfo);
        if (memberName is not null)
        {
            return typeInfo.Type.GetMember(memberName, MemberTypes.Property | MemberTypes.Field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault();
        }

        return null;
    }

    // Uses reflection to determine any custom converters specified for the element of a nullable type.
    private static JsonConverter? ExtractCustomNullableConverter(JsonConverter? converter)
    {
        Debug.Assert(converter is null || IsBuiltInConverter(converter));

        // There is unfortunately no way in which we can obtain the element converter from a nullable converter without resorting to private reflection
        // https://github.com/dotnet/runtime/blob/5fda47434cecc590095e9aef3c4e560b7b7ebb47/src/libraries/System.Text.Json/src/System/Text/Json/Serialization/Converters/Value/NullableConverter.cs#L15-L17
        if (converter != null && converter.GetType().Name == "NullableConverter`1")
        {
            FieldInfo? elementConverterField = converter.GetType().GetField("_elementConverter", BindingFlags.Instance | BindingFlags.NonPublic);
            Debug.Assert(elementConverterField != null);
            return (JsonConverter)elementConverterField!.GetValue(converter)!;
        }

        return null;
    }

    // Uses reflection to determine serialization configuration for enum types
    // cf. https://github.com/dotnet/runtime/blob/5fda47434cecc590095e9aef3c4e560b7b7ebb47/src/libraries/System.Text.Json/src/System/Text/Json/Serialization/Converters/Value/EnumConverter.cs#L23-L25
    private static bool TryGetStringEnumConverterValues(JsonTypeInfo typeInfo, JsonConverter converter, out JsonArray? values)
    {
        Debug.Assert(typeInfo.Type.IsEnum && IsBuiltInConverter(converter));

        if (converter is JsonConverterFactory factory)
        {
            converter = factory.CreateConverter(typeInfo.Type, typeInfo.Options)!;
        }

        FieldInfo? converterOptionsField = converter.GetType().GetField("_converterOptions", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo? namingPolicyField = converter.GetType().GetField("_namingPolicy", BindingFlags.Instance | BindingFlags.NonPublic);
        Debug.Assert(converterOptionsField != null);
        Debug.Assert(namingPolicyField != null);

        const int EnumConverterOptionsAllowStrings = 1;
        var converterOptions = (int)converterOptionsField!.GetValue(converter)!;
        if ((converterOptions & EnumConverterOptionsAllowStrings) != 0)
        {
            if (typeInfo.Type.GetCustomAttribute<FlagsAttribute>() is not null)
            {
                // For enums implemented as flags do not surface values in the JSON schema.
                values = null;
            }
            else
            {
                var namingPolicy = (JsonNamingPolicy?)namingPolicyField!.GetValue(converter)!;
                string[] names = Enum.GetNames(typeInfo.Type);
                values = new JsonArray();
                foreach (string name in names)
                {
                    string effectiveName = namingPolicy?.ConvertName(name) ?? name;
                    values.Add((JsonNode)effectiveName);
                }
            }

            return true;
        }

        values = null;
        return false;
    }

    // Resolves the parameters of the deserialization constructor for a type, if they exist.
    [SuppressMessage("Trimming", "IL2072:Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.",
        Justification = "The deserialization constructor should have already been referenced by the source generator and therefore will not have been trimmed.")]
    private static Func<JsonPropertyInfo, ParameterInfo?> ResolveJsonConstructorParameterMapper(JsonTypeInfo typeInfo)
    {
        Debug.Assert(typeInfo.Kind is JsonTypeInfoKind.Object);

        if (typeInfo.Properties.Count > 0 &&
            typeInfo.CreateObject is null && // Ensure that a default constructor isn't being used
            typeInfo.Type.TryGetDeserializationConstructor(useDefaultCtorInAnnotatedStructs: true, out ConstructorInfo? ctor))
        {
            ParameterInfo[]? parameters = ctor?.GetParameters();
            if (parameters?.Length > 0)
            {
                Dictionary<ParameterLookupKey, ParameterInfo> dict = new(parameters.Length);
                foreach (ParameterInfo parameter in parameters)
                {
                    if (parameter.Name is not null)
                    {
                        // We don't care about null parameter names or conflicts since they
                        // would have already been rejected by JsonTypeInfo configuration.
                        dict[new(parameter.Name, parameter.ParameterType)] = parameter;
                    }
                }

                return prop => dict.TryGetValue(new(prop.Name, prop.PropertyType), out ParameterInfo? parameter) ? parameter : null;
            }
        }

        return static _ => null;
    }

    // Parameter to property matching semantics as declared in
    // https://github.com/dotnet/runtime/blob/12d96ccfaed98e23c345188ee08f8cfe211c03e7/src/libraries/System.Text.Json/src/System/Text/Json/Serialization/Metadata/JsonTypeInfo.cs#L1007-L1030
    private readonly struct ParameterLookupKey : IEquatable<ParameterLookupKey>
    {
        public ParameterLookupKey(string name, Type type)
        {
            Name = name;
            Type = type;
        }

        public string Name { get; }
        public Type Type { get; }

        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
        public bool Equals(ParameterLookupKey other) => Type == other.Type && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
        public override bool Equals(object? obj) => obj is ParameterLookupKey key && Equals(key);
    }

    // Resolves the deserialization constructor for a type using logic copied from
    // https://github.com/dotnet/runtime/blob/e12e2fa6cbdd1f4b0c8ad1b1e2d960a480c21703/src/libraries/System.Text.Json/Common/ReflectionExtensions.cs#L227-L286
    private static bool TryGetDeserializationConstructor(
#if NETCOREAPP
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
#endif
        this Type type,
        bool useDefaultCtorInAnnotatedStructs,
        out ConstructorInfo? deserializationCtor)
    {
        ConstructorInfo? ctorWithAttribute = null;
        ConstructorInfo? publicParameterlessCtor = null;
        ConstructorInfo? lonePublicCtor = null;

        ConstructorInfo[] constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        if (constructors.Length == 1)
        {
            lonePublicCtor = constructors[0];
        }

        foreach (ConstructorInfo constructor in constructors)
        {
            if (HasJsonConstructorAttribute(constructor))
            {
                if (ctorWithAttribute != null)
                {
                    deserializationCtor = null;
                    return false;
                }

                ctorWithAttribute = constructor;
            }
            else if (constructor.GetParameters().Length == 0)
            {
                publicParameterlessCtor = constructor;
            }
        }

        // Search for non-public ctors with [JsonConstructor].
        foreach (ConstructorInfo constructor in type.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (HasJsonConstructorAttribute(constructor))
            {
                if (ctorWithAttribute != null)
                {
                    deserializationCtor = null;
                    return false;
                }

                ctorWithAttribute = constructor;
            }
        }

        // Structs will use default constructor if attribute isn't used.
        if (useDefaultCtorInAnnotatedStructs && type.IsValueType && ctorWithAttribute == null)
        {
            deserializationCtor = null;
            return true;
        }

        deserializationCtor = ctorWithAttribute ?? publicParameterlessCtor ?? lonePublicCtor;
        return true;

        static bool HasJsonConstructorAttribute(ConstructorInfo constructorInfo) =>
            constructorInfo.GetCustomAttribute<JsonConstructorAttribute>() != null;
    }

    private static bool IsBuiltInConverter(JsonConverter converter) =>
        converter.GetType().Assembly == typeof(JsonConverter).Assembly;

    private static bool TryGetNullableElement(Type type, [NotNullWhen(true)] out Type? elementType)
    {
        if (type.IsValueType && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            elementType = type.GetGenericArguments()[0];
            return true;
        }

        elementType = null;
        return false;
    }
}