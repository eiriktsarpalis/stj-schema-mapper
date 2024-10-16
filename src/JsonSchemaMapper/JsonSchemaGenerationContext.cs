// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace JsonSchemaMapper;

    /// <summary>
    /// Defines the context in which a JSON schema within a type graph is being generated.
    /// </summary>
#if EXPOSE_JSON_SCHEMA_MAPPER
public
#else
internal
#endif
    readonly struct JsonSchemaGenerationContext
{
    internal readonly string[] _path;

    internal JsonSchemaGenerationContext(
        JsonTypeInfo typeInfo,
        JsonTypeInfo? baseTypeInfo,
        Type? declaringType,
        JsonPropertyInfo? propertyInfo,
        ParameterInfo? parameterInfo,
        ICustomAttributeProvider? propertyAttributeProvider,
        string[] path)
    {
        TypeInfo = typeInfo;
        DeclaringType = declaringType;
        BaseTypeInfo = baseTypeInfo;
        PropertyInfo = propertyInfo;
        ParameterInfo = parameterInfo;
        PropertyAttributeProvider = propertyAttributeProvider;
        _path = path;
    }

    /// <summary>
    /// The path to the schema document currently being generated.
    /// </summary>
    public ReadOnlySpan<string> Path => _path;

    /// <summary>
    /// The <see cref="JsonTypeInfo"/> for the type being processed.
    /// </summary>
    public JsonTypeInfo TypeInfo { get; }

    /// <summary>
    /// The declaring type of the property or parameter being processed.
    /// </summary>
    public Type? DeclaringType { get; }

    /// <summary>
    /// The type info for the polymorphic base type if generated as a derived type.
    /// </summary>
    public JsonTypeInfo? BaseTypeInfo { get; }

    /// <summary>
    /// The <see cref="JsonPropertyInfo"/> if the schema is being generated for a property.
    /// </summary>
    public JsonPropertyInfo? PropertyInfo { get; }

    /// <summary>
    /// The <see cref="System.Reflection.ParameterInfo"/> if a constructor parameter
    /// has been associated with the accompanying <see cref="PropertyInfo"/>.
    /// </summary>
    public ParameterInfo? ParameterInfo { get; }

    /// <summary>
    /// The <see cref="ICustomAttributeProvider"/> corresponding to the property or field being processed.
    /// </summary>
    public ICustomAttributeProvider? PropertyAttributeProvider { get; }

    /// <summary>
    /// Checks if the type, property, or parameter has the specified attribute applied.
    /// </summary>
    /// <typeparam name="TAttribute">The type of the attribute to resolve.</typeparam>
    /// <param name="inherit">Whether to look up the hierarchy chain for the inherited custom attribute.</param>
    /// <returns>True if the attribute is defined by the current context.</returns>
    public bool IsDefined<TAttribute>(bool inherit = false)
        where TAttribute : Attribute =>
        GetCustomAttributes(typeof(TAttribute), inherit).Any();

    /// <summary>
    /// Checks if the type, property, or parameter has the specified attribute applied.
    /// </summary>
    /// <typeparam name="TAttribute">The type of the attribute to resolve.</typeparam>
    /// <param name="inherit">Whether to look up the hierarchy chain for the inherited custom attribute.</param>
    /// <returns>The first attribute resolved from the current context, or null.</returns>
    public TAttribute? GetAttribute<TAttribute>(bool inherit = false)
        where TAttribute : Attribute =>
        (TAttribute?)GetCustomAttributes(typeof(TAttribute), inherit).FirstOrDefault();

    /// <summary>
    /// Resolves any custom attributes that might have been applied to the type, property, or parameter.
    /// </summary>
    /// <param name="type">The attribute type to resolve.</param>
    /// <param name="inherit">Whether to look up the hierarchy chain for the inherited custom attribute.</param>
    /// <returns>An enumerable of all custom attributes defined by the context.</returns>
    public IEnumerable<Attribute> GetCustomAttributes(Type type, bool inherit = false)
    {
        // Resolves attributes starting from the property, then the parameter, and finally the type itself.
        return GetAttrs(PropertyAttributeProvider)
            .Concat(GetAttrs(ParameterInfo))
            .Concat(GetAttrs(TypeInfo.Type))
            .Cast<Attribute>();

        object[] GetAttrs(ICustomAttributeProvider? provider) =>
            provider?.GetCustomAttributes(type, inherit) ?? Array.Empty<object>();
    }
}