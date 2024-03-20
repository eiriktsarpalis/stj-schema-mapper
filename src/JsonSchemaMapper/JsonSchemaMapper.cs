using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace JsonSchemaMapper;

/// <summary>
/// Maps .NET types to JSON schema objects using contract metadata from <see cref="JsonTypeInfo"/> instances.
/// </summary>
public static class JsonSchemaMapper
{
    /// <summary>
    /// The JSON schema draft version used by the generated schemas.
    /// </summary>
    public const string SchemaVersion = "https://json-schema.org/draft/2020-12/schema";

    /// <summary>
    /// Generates a JSON schema corresponding to the contract metadata of the specified type.
    /// </summary>
    /// <param name="options">The options instance from which to resolve the contract metadata.</param>
    /// <param name="type">The root type for which to generate the JSON schema.</param>
    /// <param name="configuration">The configuration object controlling the schema generation.</param>
    /// <returns>A new <see cref="JsonObject"/> instance defining the JSON schema for <paramref name="type"/>.</returns>
    /// <exception cref="ArgumentNullException">One of the specified parameters is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">The <paramref name="options"/> instance is not marked as read-only.</exception>
    public static JsonObject GetJsonSchema(this JsonSerializerOptions options, Type type, JsonSchemaMapperConfiguration? configuration = null)
    {
        if (options is null)
        {
           ThrowHelpers.ThrowArgumentNullException(nameof(options));
        }

        if (type is null)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(type));
        }

        if (!options.IsReadOnly)
        {
            Throw();
            static void Throw() => throw new InvalidOperationException("The options instance must be read-only");
        }

        JsonTypeInfo typeInfo = options.GetTypeInfo(type);
        return ToJsonSchemaCore(typeInfo, configuration);
    }

    /// <summary>
    /// Generates a JSON schema corresponding to the specified contract metadata.   
    /// </summary>
    /// <param name="typeInfo">The contract metadata for which to generate the schema.</param>
    /// <param name="configuration">The configuration object controlling the schema generation.</param>
    /// <returns>A new <see cref="JsonObject"/> instance defining the JSON schema for <paramref name="typeInfo"/>.</returns>
    /// <exception cref="ArgumentNullException">One of the specified parameters is <see langword="null" />.</exception>
    public static JsonObject ToJsonSchema(this JsonTypeInfo typeInfo, JsonSchemaMapperConfiguration? configuration = null)
    {
        if (typeInfo is null)
        {
            ThrowHelpers.ThrowArgumentNullException(nameof(typeInfo));
        }

        return ToJsonSchemaCore(typeInfo, configuration);
    }

    private static JsonObject ToJsonSchemaCore(JsonTypeInfo typeInfo, JsonSchemaMapperConfiguration? configuration)
    {
        typeInfo.MakeReadOnly();
        var state = new GenerationState(configuration ?? JsonSchemaMapperConfiguration.Default);
        return MapJsonSchemaCore(typeInfo, ref state);
    }

    private static JsonObject MapJsonSchemaCore(
        JsonTypeInfo typeInfo,
        ref GenerationState state,
        string? description = null,
        JsonConverter? customConverter = null,
        JsonNumberHandling? customNumberHandling = null)
    {
        Debug.Assert(typeInfo.IsReadOnly);

        JsonConverter effectiveConverter = customConverter ?? typeInfo.Converter;
        JsonNumberHandling? effectiveNumberHandling = customNumberHandling ?? typeInfo.NumberHandling;

        if (!IsBuiltInConverter(effectiveConverter))
        {
            return new JsonObject(); // We can't make any schema determinations if a custom converter is used
        }

        if (state.TryGetGeneratedSchemaPath(typeInfo.Type, effectiveConverter, out string? typePath))
        {
            // Schema for type has already been generated, return a reference to it.
            return new JsonObject { [RefPropertyName] = typePath };
        }

        if (state.Configuration.ResolveDescriptionAttributes)
        {
            description ??= typeInfo.Type.GetCustomAttribute<DescriptionAttribute>()?.Description;
        }

        if (TryGetNullableElement(typeInfo.Type, out Type? nullableElementType))
        {
            // Special handling for Nullable<T>, just return the schema for
            // the element type with `null` appended to the declared types.
            JsonTypeInfo nullableElementTypeInfo = typeInfo.Options.GetTypeInfo(nullableElementType);
            customConverter = ExtractCustomNullableConverter(customConverter);
            JsonObject elementSchema = MapJsonSchemaCore(nullableElementTypeInfo, ref state, description, customConverter);

            if (nullableElementType.IsEnum && elementSchema.TryGetPropertyValue(EnumPropertyName, out JsonNode? nullaleElementEnumValues))
            {
                // Special case nullable string enum types which require appending the null value to the "enum" property
                Debug.Assert(nullableElementType.IsEnum && nullaleElementEnumValues is JsonArray);
                ((JsonArray)nullaleElementEnumValues!).Add(item: null);
            }
            else if (elementSchema.TryGetPropertyValue(TypePropertyName, out JsonNode? elementTypeValue))
            {
                // Insert null to the "type" property
                Debug.Assert(elementTypeValue?.GetValueKind() is JsonValueKind.String or JsonValueKind.Array);
                if (elementTypeValue is JsonArray arr)
                {
                    // Type property is already an array, append "null" to it
                    Debug.Assert(arr.All(elem => (string)elem! != "null"));
                    arr.Add((JsonNode)"null");
                }
                else
                {
                    // Convert the type property from string to array
                    Debug.Assert((string)elementTypeValue! != "null");
                    elementSchema[TypePropertyName] = new JsonArray { elementTypeValue?.DeepClone(), (JsonNode)"null" };
                }
            }

            return elementSchema;
        }

        JsonSchemaType schemaType = JsonSchemaType.Any;
        string? format = null;
        JsonObject? properties = null;
        JsonArray? requiredProperties = null;
        JsonObject? arrayItems = null;
        JsonObject? additionalProperties = null;
        JsonArray? enumValues = null;
        JsonArray? anyOfTypes = null;

        switch (typeInfo.Kind)
        {
            case JsonTypeInfoKind.None:
                if (s_simpleTypeInfo.TryGetValue(typeInfo.Type, out var simpleTypeInfo))
                {
                    schemaType = simpleTypeInfo.SchemaType;
                    format = simpleTypeInfo.Format;

                    if (effectiveNumberHandling is JsonNumberHandling numberHandling &&
                        schemaType is JsonSchemaType.Integer or JsonSchemaType.Number)
                    {
                        if ((numberHandling & (JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)) != 0)
                        {
                            schemaType |= JsonSchemaType.String;
                        }
                        else if (numberHandling is JsonNumberHandling.AllowNamedFloatingPointLiterals)
                        {
                            anyOfTypes = 
                                [
                                    new JsonObject { [TypePropertyName] = MapSchemaType(schemaType) },
                                    new JsonObject 
                                    {
                                        [EnumPropertyName] = new JsonArray { (JsonNode)"NaN", (JsonNode)"Infinity", (JsonNode)"-Infinity" }
                                    }
                                ];

                            schemaType = JsonSchemaType.Any; // reset the parent setting
                        }
                    }
                }
                else if (typeInfo.Type.IsEnum)
                {
                    if (TryGetStringEnumConverterValues(typeInfo, effectiveConverter, out JsonArray? values))
                    {
                        if (values is null)
                        {
                            // enum declared with the flags attribute -- do not surface enum values in the JSON schema.
                            schemaType = JsonSchemaType.String;
                        }
                        else
                        {
                            enumValues = values;
                        }
                    }
                    else
                    {
                        schemaType = JsonSchemaType.Integer;
                    }
                }

                break;

            case JsonTypeInfoKind.Object:
                schemaType = JsonSchemaType.Object;

                state.RegisterTypePath(typeInfo.Type, effectiveConverter);
                state.Push(PropertiesPropertyName);
                foreach (JsonPropertyInfo property in typeInfo.Properties)
                {
                    if (property is { Get: null, Set: null })
                    {
                        continue; // Skip [JsonIgnore] property
                    }

                    if (property.IsExtensionData)
                    {
                        continue; // Extension data properties have no impact on the schema
                    }

                    JsonNumberHandling? propertyNumberHandling = property.NumberHandling ?? effectiveNumberHandling;
                    JsonTypeInfo propertyTypeInfo = typeInfo.Options.GetTypeInfo(property.PropertyType);
                    string? propertyDescription = state.Configuration.ResolveDescriptionAttributes
                        ? property.AttributeProvider?.GetCustomAttributes(inherit: true).OfType<DescriptionAttribute>().FirstOrDefault()?.Description
                        : null;

                    state.Push(property.Name);
                    JsonObject propertySchema = MapJsonSchemaCore(propertyTypeInfo, ref state, propertyDescription, property.CustomConverter, propertyNumberHandling);
                    state.Pop();

                    (properties ??= []).Add(property.Name, propertySchema);

                    if (property.IsRequired)
                    {
                        (requiredProperties ??= []).Add((JsonNode)property.Name);
                    }
                }
                state.Pop();

                break;

            case JsonTypeInfoKind.Enumerable:
                schemaType = JsonSchemaType.Array;
                Type elementType = GetElementType(typeInfo);
                JsonTypeInfo elementTypeInfo = typeInfo.Options.GetTypeInfo(elementType);

                state.RegisterTypePath(typeInfo.Type, effectiveConverter);
                state.Push(ItemsPropertyName);
                arrayItems = MapJsonSchemaCore(elementTypeInfo, ref state);
                state.Pop();

                break;

            case JsonTypeInfoKind.Dictionary:
                schemaType = JsonSchemaType.Object;
                Type valueType = GetElementType(typeInfo);
                JsonTypeInfo valueTypeInfo = typeInfo.Options.GetTypeInfo(valueType);

                state.RegisterTypePath(typeInfo.Type, effectiveConverter);
                state.Push(AdditionalPropertiesPropertyName);
                additionalProperties = MapJsonSchemaCore(valueTypeInfo, ref state);
                state.Pop();

                break;

            default:
                Debug.Fail("Unreachable code");
                break;
        }

        if (state.Configuration.AllowNullForReferenceTypes && schemaType != JsonSchemaType.Any && !typeInfo.Type.IsValueType)
        {
            // TODO read nullability information from the contract
            // cf. https://github.com/dotnet/runtime/issues/1256
            schemaType |= JsonSchemaType.Null;
        }

        return CreateSchemaDocument(
            description,
            schemaType,
            format,
            properties,
            requiredProperties,
            arrayItems,
            additionalProperties,
            enumValues,
            anyOfTypes,
            ref state);
    }

    private ref struct GenerationState(JsonSchemaMapperConfiguration configuration)
    {
        private int _currentDepth = 0;
        private readonly List<string>? _currentPath = configuration.AllowSchemaReferences ? new() : null;
        private readonly Dictionary<(Type, JsonConverter), string>? _typePaths = configuration.AllowSchemaReferences ? new() : null;

        public readonly JsonSchemaMapperConfiguration Configuration => configuration;
        public readonly int CurrentDepth => _currentDepth;

        public void Push(string nodeId)
        {
            if (_currentDepth == configuration.MaxDepth)
            {
                Throw();
                static void Throw() => throw new InvalidOperationException("The maximum depth of the schema has been reached.");
            }

            _currentDepth++;

            if (configuration.AllowSchemaReferences)
            {
                Debug.Assert(_currentPath != null);
                _currentPath!.Add(nodeId);
            }
        }

        public void Pop()
        {
            Debug.Assert(_currentDepth > 0);
            _currentDepth--;

            if (configuration.AllowSchemaReferences)
            {
                Debug.Assert(_currentPath != null);
                _currentPath!.RemoveAt(_currentPath.Count - 1);
            }
        }

        public readonly void RegisterTypePath(Type type, JsonConverter converter)
        {
            if (Configuration.AllowSchemaReferences)
            {
                Debug.Assert(_currentPath != null);
                Debug.Assert(_typePaths != null);

                string pointer = _currentDepth == 0 ? "#" : "#/" + string.Join("/", _currentPath);
                _typePaths!.Add((type, converter), pointer);
            }
        }

        public readonly bool TryGetGeneratedSchemaPath(Type type, JsonConverter converter, [NotNullWhen(true)]out string? value)
        {
            if (Configuration.AllowSchemaReferences)
            {
                Debug.Assert(_typePaths != null);
                return _typePaths!.TryGetValue((type, converter), out value);
            }

            value = null;
            return false;
        }
    }

    private static JsonObject CreateSchemaDocument(
        string? description,
        JsonSchemaType schemaType,
        string? format,
        JsonObject? properties,
        JsonArray? requiredProperties,
        JsonObject? arrayItems,
        JsonObject? additionalProperties,
        JsonArray? enumValues,
        JsonArray? anyOfSchema,
        ref readonly GenerationState state)
    {
        var schema = new JsonObject();

        if (state.CurrentDepth == 0 && state.Configuration.IncludeSchemaVersion)
        {
            schema.Add(SchemaPropertyName, SchemaVersion);
        }

        if (description is not null)
        {
            schema.Add(DescriptionPropertyName, description);
        }

        if (MapSchemaType(schemaType) is JsonNode type)
        {
            schema.Add(TypePropertyName, type);
        }

        if (format is not null)
        {
            schema.Add(FormatPropertyName, format);
        }

        if (properties is not null)
        {
            schema.Add(PropertiesPropertyName, properties);
        }

        if (requiredProperties is not null)
        {
            schema.Add(RequiredPropertyName, requiredProperties);
        }

        if (arrayItems is not null)
        {
            schema.Add(ItemsPropertyName, arrayItems);
        }

        if (additionalProperties is not null)
        {
            schema.Add(AdditionalPropertiesPropertyName, additionalProperties);
        }

        if (enumValues is not null)
        {
            schema.Add(EnumPropertyName, enumValues);
        }

        if (anyOfSchema is not null)
        {
            schema.Add(AnyOfPropertyName, anyOfSchema);
        }

        return schema;
    }

    [Flags]
    private enum JsonSchemaType
    {
        Any = 0, // No type declared on the schema
        String = 1,
        Integer = 2,
        Number = 4,
        Boolean = 8,
        Array = 16,
        Object = 32,
        Null = 64,
    }

    private static JsonNode? MapSchemaType(JsonSchemaType schemaType)
    {
        return schemaType switch
        {
            JsonSchemaType.Any => null,
            JsonSchemaType.Null => "null",
            JsonSchemaType.Boolean => "boolean",
            JsonSchemaType.Integer => "integer",
            JsonSchemaType.Number => "number",
            JsonSchemaType.String => "string",
            JsonSchemaType.Array => "array",
            JsonSchemaType.Object => "object",
            _ => MapCompositeSchemaType(schemaType),
        };

        static JsonArray MapCompositeSchemaType(JsonSchemaType schemaType)
        {
            var array = new JsonArray();
            foreach (JsonSchemaType type in s_schemaValues)
            {
                if ((schemaType & type) != 0)
                {
                    array.Add(MapSchemaType(type));
                }
            }

            return array;
        }
    }

    private readonly static JsonSchemaType[] s_schemaValues =
#if NETCOREAPP
        Enum.GetValues<JsonSchemaType>()
#else
        Enum.GetValues(typeof(JsonSchemaType))
        .Cast<JsonSchemaType>()
#endif
        .Where(value => value != 0)
        .ToArray();

    private static bool IsBuiltInConverter(JsonConverter converter)
        => converter.GetType().Assembly == typeof(JsonConverter).Assembly;

    private static Type GetElementType(JsonTypeInfo typeInfo)
    {
        // Workaround for https://github.com/dotnet/runtime/issues/77306#issuecomment-2007887560
        Debug.Assert(typeInfo.Kind is JsonTypeInfoKind.Enumerable or JsonTypeInfoKind.Dictionary);
        return (Type)typeof(JsonTypeInfo).GetProperty("ElementType", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(typeInfo)!;
    }

    private static bool TryGetStringEnumConverterValues(JsonTypeInfo typeInfo, JsonConverter converter, out JsonArray? values)
    {
        Debug.Assert(typeInfo.Type.IsEnum && IsBuiltInConverter(converter));

        if (converter is JsonConverterFactory factory)
        {
            converter = factory.CreateConverter(typeInfo.Type, typeInfo.Options)!;
        }

        // There is unfortunately no way in which we can obtain enum converter configuration without resorting to private reflection
        // https://github.com/dotnet/runtime/blob/5fda47434cecc590095e9aef3c4e560b7b7ebb47/src/libraries/System.Text.Json/src/System/Text/Json/Serialization/Converters/Value/EnumConverter.cs#L23-L25
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

    private const string SchemaPropertyName = "$schema";
    private const string RefPropertyName = "$ref";
    private const string DescriptionPropertyName = "description";
    private const string TypePropertyName = "type";
    private const string FormatPropertyName = "format";
    private const string PropertiesPropertyName = "properties";
    private const string RequiredPropertyName = "required";
    private const string ItemsPropertyName = "items";
    private const string AdditionalPropertiesPropertyName = "additionalProperties";
    private const string EnumPropertyName = "enum";
    private const string AnyOfPropertyName = "anyOf";

    private static Dictionary<Type, (JsonSchemaType SchemaType, string? Format)> s_simpleTypeInfo = new()
    {
        [typeof(object)] = (JsonSchemaType.Any, null),
        [typeof(bool)] = (JsonSchemaType.Boolean, null),
        [typeof(byte)] = (JsonSchemaType.Integer, null),
        [typeof(ushort)] = (JsonSchemaType.Integer, null),
        [typeof(uint)] = (JsonSchemaType.Integer, null),
        [typeof(ulong)] = (JsonSchemaType.Integer, null),
        [typeof(sbyte)] = (JsonSchemaType.Integer, null),
        [typeof(short)] = (JsonSchemaType.Integer, null),
        [typeof(int)] = (JsonSchemaType.Integer, null),
        [typeof(long)] = (JsonSchemaType.Integer, null),
        [typeof(float)] = (JsonSchemaType.Number, null),
        [typeof(double)] = (JsonSchemaType.Number, null),
        [typeof(decimal)] = (JsonSchemaType.Number, null),
#if NETCOREAPP
        [typeof(UInt128)] = (JsonSchemaType.Integer, null),
        [typeof(Int128)] = (JsonSchemaType.Integer, null),
        [typeof(Half)] = (JsonSchemaType.Number, null),
#endif
        [typeof(char)] = (JsonSchemaType.String, null),
        [typeof(string)] = (JsonSchemaType.String, null),
        [typeof(byte[])] = (JsonSchemaType.String, null),
        [typeof(Memory<byte>)] = (JsonSchemaType.String, null),
        [typeof(ReadOnlyMemory<byte>)] = (JsonSchemaType.String, null),
        [typeof(DateTime)] = (JsonSchemaType.String, Format: "date-time"),
        [typeof(DateTimeOffset)] = (JsonSchemaType.String, Format: "date-time"),
        [typeof(TimeSpan)] = (JsonSchemaType.String, Format: "time"),
#if NETCOREAPP
        [typeof(DateOnly)] = (JsonSchemaType.String, Format: "date"),
        [typeof(TimeOnly)] = (JsonSchemaType.String, Format: "time"),
#endif
        [typeof(Guid)] = (JsonSchemaType.String, Format: "uuid"),
        [typeof(Uri)] = (JsonSchemaType.String, Format: "uri"),
        [typeof(Version)] = (JsonSchemaType.String, null),
        [typeof(JsonDocument)] = (JsonSchemaType.Any, null),
        [typeof(JsonElement)] = (JsonSchemaType.Any, null),
        [typeof(JsonNode)] = (JsonSchemaType.Any, null),
        [typeof(JsonValue)] = (JsonSchemaType.Any, null),
        [typeof(JsonObject)] = (JsonSchemaType.Object, null),
        [typeof(JsonArray)] = (JsonSchemaType.Array, null),
    };

    private static class ThrowHelpers
    {
        [DoesNotReturn]
        public static void ThrowArgumentNullException(string name) => throw new ArgumentNullException(name);
    }
}