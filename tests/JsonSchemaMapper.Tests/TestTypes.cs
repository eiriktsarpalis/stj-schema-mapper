using System.Collections;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace JsonSchemaMapper.Tests;

internal static partial class TestTypes
{
    public static IEnumerable<object[]> GetTestData() => GetTestDataCore().Select(x => new object[] { x });

    public static IEnumerable<ITestData> GetTestDataCore()
    {
        // Primitives and built-in types
        yield return new TestData<object>(Value: new(), ExpectedJsonSchema: "{}");
        yield return new TestData<bool>(true);
        yield return new TestData<byte>(42);
        yield return new TestData<ushort>(42);
        yield return new TestData<uint>(42);
        yield return new TestData<ulong>(42);
        yield return new TestData<sbyte>(42, ExpectedJsonSchema: """{"type":"integer"}""");
        yield return new TestData<short>(42);
        yield return new TestData<int>(42);
        yield return new TestData<long>(42);
        yield return new TestData<float>(1.2f);
        yield return new TestData<double>(3.14159d);
        yield return new TestData<decimal>(3.14159M);
#if NETCOREAPP
        yield return new TestData<UInt128>(42, ExpectedJsonSchema: """{"type":"integer"}""");
        yield return new TestData<Int128>(42, ExpectedJsonSchema: """{"type":"integer"}""");
        yield return new TestData<Half>((Half)3.141, ExpectedJsonSchema: """{"type":"number"}""");
#endif
        yield return new TestData<string>("I am a string");
        yield return new TestData<char>('c', ExpectedJsonSchema: """{"type":"string"}""");
        yield return new TestData<byte[]>([1, 2, 3], ExpectedJsonSchema: """{"type":"string"}""");
        yield return new TestData<Memory<byte>>(new byte[] { 1, 2, 3 }, ExpectedJsonSchema: """{"type":"string"}""");
        yield return new TestData<ReadOnlyMemory<byte>>(new byte[] { 1, 2, 3 }, ExpectedJsonSchema: """{"type":"string"}""");
        yield return new TestData<DateTime>(new(2021, 1, 1));
        yield return new TestData<DateTimeOffset>(new(new DateTime(2021, 1, 1), TimeSpan.Zero), ExpectedJsonSchema: """{"type":"string","format": "date-time"}""");
        yield return new TestData<TimeSpan>(new(1, 2, 3, 4, 5), ExpectedJsonSchema: """{"type":"string","format": "time"}""");
#if NETCOREAPP
        yield return new TestData<DateOnly>(new(2021, 1, 1), ExpectedJsonSchema: """{"type":"string","format": "date"}""");
        yield return new TestData<TimeOnly>(new(1, 2, 3, 4, 5), ExpectedJsonSchema: """{"type":"string","format": "time"}""");
#endif
        yield return new TestData<Guid>(Guid.Empty);
        yield return new TestData<Uri>(new("http://example.com"));
        yield return new TestData<Version>(new(1, 2, 3, 4), ExpectedJsonSchema: """{"type":"string"}""");
        yield return new TestData<JsonDocument>(JsonDocument.Parse("""[{ "x" : 42 }]"""), ExpectedJsonSchema: "{}");
        yield return new TestData<JsonElement>(JsonDocument.Parse("""[{ "x" : 42 }]""").RootElement, ExpectedJsonSchema: "{}");
        yield return new TestData<JsonNode>(JsonNode.Parse("""[{ "x" : 42 }]"""));
        yield return new TestData<JsonValue>((JsonValue)42);
        yield return new TestData<JsonObject>(new() { ["x"] = 42 });
        yield return new TestData<JsonArray>([1, 2, 3]);

        // Enum types
        yield return new TestData<IntEnum>(IntEnum.A, ExpectedJsonSchema: """{"type":"integer"}""");
        yield return new TestData<StringEnum>(StringEnum.A);
        yield return new TestData<FlagsStringEnum>(FlagsStringEnum.A, ExpectedJsonSchema: """{"type":"string"}""");

        // Nullable<T> types
        yield return new TestData<bool?>(null, ExpectedJsonSchema: """{"type":["boolean","null"]}""");
        yield return new TestData<int?>(null, ExpectedJsonSchema: """{"type":["integer","null"]}""");
        yield return new TestData<double?>(null, ExpectedJsonSchema: """{"type":["number","null"]}""");
        yield return new TestData<Guid?>(null, ExpectedJsonSchema: """{"type":["string","null"],"format":"uuid"}""");
        yield return new TestData<JsonElement?>(null, ExpectedJsonSchema: "{}");
        yield return new TestData<IntEnum?>(null, ExpectedJsonSchema: """{"type":["integer","null"]}""");
        yield return new TestData<StringEnum?>(null, ExpectedJsonSchema: """{"enum":["A","B","C",null]}""");

        // User-defined POCOs
        yield return new TestData<SimplePoco>(new() { X = "string", Y = 42, Z = 3.14, W = true });
        yield return new TestData<SimpleRecord>(new(1, "two", true, 3.14));
        yield return new TestData<SimpleRecordStruct>(new(1, "two", true, 3.14));
        yield return new TestData<PocoWithRequiredMembers>(
            new() { X = "str1", Y = "str2" }, 
            ExpectedJsonSchema: """
            {
              "type": "object",
              "properties": {
                "Y": { "type": "string" },
                "Z": { "type": "integer" },
                "X": { "type": "string" }
              },
              "required": [ "Y", "Z", "X" ]
            }
            """);

        yield return new TestData<PocoWithIgnoredMembers>(new() { X = 1, Y = 2 });
        yield return new TestData<PocoWithCustomNaming>(new() { IntegerProperty = 1, StringProperty = "str" });
        yield return new TestData<PocoWithCustomNumberHandling>(
            Value: new() { X = 1 }, 
            ExpectedJsonSchema: """
                {
                    "type": "object",
                    "properties": { "X": { "type": ["string","integer"] } }
                }
                """);

        yield return new TestData<PocoWithCustomNumberHandlingOnProperties>(
            Value: new() { X = 1, Y = 2, Z = 3 },
            ExpectedJsonSchema: """
            {
              "type": "object",
              "properties": {
                "X": { "type": ["string", "integer"] },
                "Y": {
                  "anyOf": [
                    { "type": "integer" },
                    { "enum": ["NaN", "Infinity", "-Infinity"]}
                  ]
                },
                "Z": { "type": ["string", "integer"] }
              }
            }
            """);

        yield return new TestData<PocoWithRecursiveMembers>(
            Value: new() { Value = 1, Next = new() { Value = 2, Next = new() { Value = 3 } } },
            ExpectedJsonSchema: """{"type":["object","null"],"properties":{"Value":{"type":"integer"},"Next":{"$ref":"#"}}}""",
            Configuration: new() { AllowNullForReferenceTypes = true });

        yield return new TestData<PocoWithDescription>(
            Value: new() { X = 42 },
            ExpectedJsonSchema: """
            {
              "description": "The type description",
              "type": "object",
              "properties": {
                "X": {
                  "description": "The property description",
                  "type": "integer"
                }
              }
            }
            """,
            IsSourceGenSupported: false); // Cannot resolve property attributes from source gen metadata

        yield return new TestData<PocoWithCustomConverter>(new() { Value = 42 }, ExpectedJsonSchema: "{}");
        yield return new TestData<PocoWithCustomPropertyConverter>(new() { Value = 42 }, ExpectedJsonSchema: """{"type":"object","properties":{"Value":{}}}""");
        yield return new TestData<PocoWithEnums>(
            Value: new()
            {
                IntEnum = IntEnum.A,
                StringEnum = StringEnum.B,
                IntEnumUsingStringConverter = IntEnum.A,
                NullableIntEnumUsingStringConverter = IntEnum.B,
                StringEnumUsingIntConverter = StringEnum.A,
                NullableStringEnumUsingIntConverter = StringEnum.B
            },
            ExpectedJsonSchema: """
                {
                  "type": "object",
                  "properties": {
                    "IntEnum": { "type": "integer" },
                    "StringEnum": { "enum": [ "A", "B", "C" ] },
                    "IntEnumUsingStringConverter": { "enum": [ "A", "B", "C" ] },
                    "NullableIntEnumUsingStringConverter": { "enum": [ "A", "B", "C", null ] },
                    "StringEnumUsingIntConverter": { "type": "integer" },
                    "NullableStringEnumUsingIntConverter": { "type": [ "integer", "null" ] }
                  }
                }
                """);

        // Collection types
        yield return new TestData<int[]>([1, 2, 3]);
        yield return new TestData<List<bool>>([false, true, false]);
        yield return new TestData<HashSet<string>>(["one", "two", "three"]);
        yield return new TestData<Queue<double>>(new([1.1, 2.2, 3.3]));
        yield return new TestData<Stack<char>>(new(['x', '2', '+']), ExpectedJsonSchema: """{"type":"array","items":{"type":"string"}}""");
        yield return new TestData<ImmutableArray<int>>([1, 2, 3]);
        yield return new TestData<ImmutableList<string>>(["one", "two", "three"]);
        yield return new TestData<ImmutableQueue<bool>>([false, false, true]);
        yield return new TestData<object[]>([1, "two", 3.14], ExpectedJsonSchema: """{"type":"array","items":{}}""");
        yield return new TestData<System.Collections.ArrayList>([1, "two", 3.14], ExpectedJsonSchema: """{"type":"array","items":{}}""");

        // Dictionary types
        yield return new TestData<Dictionary<string, int>>(new() { ["one"] = 1, ["two"] = 2, ["three"] = 3 });
        yield return new TestData<StructDictionary<string, int>>(
            Value: new([new("one", 1), new("two", 2), new("three", 3)]),
            ExpectedJsonSchema: """{"type":"object","additionalProperties":{"type": "integer"}}""");

        yield return new TestData<SortedDictionary<int, string>>(
            Value: new() { [1] = "one", [2] = "two", [3] = "three" }, 
            ExpectedJsonSchema: """{"type":"object","additionalProperties":{"type": "string"}}""");

        yield return new TestData<Dictionary<string, SimplePoco>>(new()
        {
            ["one"] = new() { X = "string", Y = 42, Z = 3.14, W = true },
            ["two"] = new() { X = "string", Y = 42, Z = 3.14, W = true },
            ["three"] = new() { X = "string", Y = 42, Z = 3.14, W = true }
        });
        yield return new TestData<Dictionary<string, object>>(
            Value: new() { ["one"] = 1, ["two"] = "two", ["three"] = 3.14 }, 
            ExpectedJsonSchema: """{"type":"object","additionalProperties":{}}""");

        yield return new TestData<Hashtable>(
            Value: new() { ["one"] = 1, ["two"] = "two", ["three"] = 3.14 }, 
            ExpectedJsonSchema: """{"type":"object","additionalProperties":{}}""");
    }

    public enum IntEnum { A, B, C };

    [JsonConverter(typeof(JsonStringEnumConverter<StringEnum>))]
    public enum StringEnum { A, B, C };

    [Flags, JsonConverter(typeof(JsonStringEnumConverter<FlagsStringEnum>))]
    public enum FlagsStringEnum { A = 1, B = 2, C = 4 };

    public class SimplePoco
    {
        public string? X { get; set; }
        public int Y { get; set; }
        public double Z { get; set; }
        public bool W { get; set; }
    }

    public record SimpleRecord(int X, string Y, bool Z, double W);
    public record struct SimpleRecordStruct(int X, string Y, bool Z, double W);

    public class PocoWithRequiredMembers
    {
        [JsonInclude]
        public required string X;

        public required string Y { get; set; }

        [JsonRequired]
        public int Z { get; set; }
    }

    public class PocoWithIgnoredMembers
    {
        public int X { get; set; }

        [JsonIgnore]
        public int Y { get; set; }
    }

    public class PocoWithCustomNaming
    {
        [JsonPropertyName("int")]
        public int IntegerProperty { get; set; }

        [JsonPropertyName("str")]
        public string? StringProperty { get; set; }
    }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public class PocoWithCustomNumberHandling
    {
        public int X { get; set; }
    }

    public class PocoWithCustomNumberHandlingOnProperties
    {
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int X { get; set; }

        [JsonNumberHandling(JsonNumberHandling.AllowNamedFloatingPointLiterals)]
        public int Y { get; set; }

        [JsonNumberHandling(JsonNumberHandling.WriteAsString)]
        public int Z { get; set; }
    }

    public class PocoWithRecursiveMembers
    {
        public int Value { get; init; }
        public PocoWithRecursiveMembers? Next { get; init; }
    }

    [Description("The type description")]
    public class PocoWithDescription
    {
        [Description("The property description")]
        public int X { get; set; }
    }

    [JsonConverter(typeof(CustomConverter))]
    public class PocoWithCustomConverter
    {
        public int Value { get; set; }

        public class CustomConverter : JsonConverter<PocoWithCustomConverter>
        {
            public override PocoWithCustomConverter Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
                new PocoWithCustomConverter { Value = reader.GetInt32() };

            public override void Write(Utf8JsonWriter writer, PocoWithCustomConverter value, JsonSerializerOptions options) =>
                writer.WriteNumberValue(value.Value);
        }
    }

    public class PocoWithCustomPropertyConverter
    {
        [JsonConverter(typeof(CustomConverter))]
        public int Value { get; set; }

        public class CustomConverter : JsonConverter<int>
        {
            public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                => int.Parse(reader.GetString()!);

            public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
                => writer.WriteStringValue(value.ToString());
        }
    }

    public class PocoWithEnums
    {
        public IntEnum IntEnum { get; init; }
        public StringEnum StringEnum { get; init; }

        [JsonConverter(typeof(JsonStringEnumConverter<IntEnum>))]
        public IntEnum IntEnumUsingStringConverter { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter<IntEnum>))]
        public IntEnum? NullableIntEnumUsingStringConverter { get; set; }

        [JsonConverter(typeof(JsonNumberEnumConverter<StringEnum>))]
        public StringEnum StringEnumUsingIntConverter { get; set; }

        [JsonConverter(typeof(JsonNumberEnumConverter<StringEnum>))]
        public StringEnum? NullableStringEnumUsingIntConverter { get; set; }
    }

    public readonly struct StructDictionary<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> values)
        : IReadOnlyDictionary<TKey, TValue>
        where TKey : notnull
    {
        private readonly IReadOnlyDictionary<TKey, TValue> _dictionary = values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        public TValue this[TKey key] => _dictionary[key];
        public IEnumerable<TKey> Keys => _dictionary.Keys;
        public IEnumerable<TValue> Values => _dictionary.Values;
        public int Count => _dictionary.Count;
        public bool ContainsKey(TKey key) => _dictionary.ContainsKey(key);
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _dictionary.GetEnumerator();
#if NETCOREAPP
        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => _dictionary.TryGetValue(key, out value);
#else
        public bool TryGetValue(TKey key, out TValue value) => _dictionary.TryGetValue(key, out value);
#endif
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_dictionary).GetEnumerator();
    }

    [JsonSerializable(typeof(object))]
    [JsonSerializable(typeof(bool))]
    [JsonSerializable(typeof(byte))]
    [JsonSerializable(typeof(ushort))]
    [JsonSerializable(typeof(uint))]
    [JsonSerializable(typeof(ulong))]
    [JsonSerializable(typeof(sbyte))]
    [JsonSerializable(typeof(short))]
    [JsonSerializable(typeof(int))]
    [JsonSerializable(typeof(long))]
    [JsonSerializable(typeof(float))]
    [JsonSerializable(typeof(double))]
    [JsonSerializable(typeof(decimal))]
#if NETCOREAPP
    [JsonSerializable(typeof(UInt128))]
    [JsonSerializable(typeof(Int128))]
    [JsonSerializable(typeof(Half))]
#endif
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(char))]
    [JsonSerializable(typeof(byte[]))]
    [JsonSerializable(typeof(Memory<byte>))]
    [JsonSerializable(typeof(ReadOnlyMemory<byte>))]
    [JsonSerializable(typeof(DateTime))]
    [JsonSerializable(typeof(DateTimeOffset))]
    [JsonSerializable(typeof(TimeSpan))]
#if NETCOREAPP
    [JsonSerializable(typeof(DateOnly))]
    [JsonSerializable(typeof(TimeOnly))]
#endif
    [JsonSerializable(typeof(Guid))]
    [JsonSerializable(typeof(Uri))]
    [JsonSerializable(typeof(Version))]
    [JsonSerializable(typeof(JsonDocument))]
    [JsonSerializable(typeof(JsonElement))]
    [JsonSerializable(typeof(JsonNode))]
    [JsonSerializable(typeof(JsonValue))]
    [JsonSerializable(typeof(JsonObject))]
    [JsonSerializable(typeof(JsonArray))]
    // Enum types
    [JsonSerializable(typeof(IntEnum))]
    [JsonSerializable(typeof(StringEnum))]
    [JsonSerializable(typeof(FlagsStringEnum))]
    // Nullable<T> types
    [JsonSerializable(typeof(bool?))]
    [JsonSerializable(typeof(int?))]
    [JsonSerializable(typeof(double?))]
    [JsonSerializable(typeof(Guid?))]
    [JsonSerializable(typeof(JsonElement?))]
    [JsonSerializable(typeof(IntEnum?))]
    [JsonSerializable(typeof(StringEnum?))]
    // User-defined POCOs
    [JsonSerializable(typeof(SimplePoco))]
    [JsonSerializable(typeof(SimpleRecord))]
    [JsonSerializable(typeof(SimpleRecordStruct))]
    [JsonSerializable(typeof(PocoWithRequiredMembers))]
    [JsonSerializable(typeof(PocoWithIgnoredMembers))]
    [JsonSerializable(typeof(PocoWithCustomNaming))]
    [JsonSerializable(typeof(PocoWithCustomNumberHandling))]
    [JsonSerializable(typeof(PocoWithCustomNumberHandlingOnProperties))]
    [JsonSerializable(typeof(PocoWithRecursiveMembers))]
    [JsonSerializable(typeof(PocoWithDescription))]
    [JsonSerializable(typeof(PocoWithCustomConverter))]
    [JsonSerializable(typeof(PocoWithCustomPropertyConverter))]
    [JsonSerializable(typeof(PocoWithEnums))]
    // Collection types
    [JsonSerializable(typeof(int[]))]
    [JsonSerializable(typeof(List<bool>))]
    [JsonSerializable(typeof(HashSet<string>))]
    [JsonSerializable(typeof(Queue<double>))]
    [JsonSerializable(typeof(Stack<char>))]
    [JsonSerializable(typeof(ImmutableArray<int>))]
    [JsonSerializable(typeof(ImmutableList<string>))]
    [JsonSerializable(typeof(ImmutableQueue<bool>))]
    [JsonSerializable(typeof(object[]))]
    [JsonSerializable(typeof(System.Collections.ArrayList))]
    [JsonSerializable(typeof(Dictionary<string, int>))]
    [JsonSerializable(typeof(SortedDictionary<int, string>))]
    [JsonSerializable(typeof(Dictionary<string, SimplePoco>))]
    [JsonSerializable(typeof(Dictionary<string, object>))]
    [JsonSerializable(typeof(Hashtable))]
    [JsonSerializable(typeof(StructDictionary<string, int>))]
    public partial class TestTypesContext : JsonSerializerContext;
}

public record TestData<T>(
    T? Value, 
    [StringSyntax("Json")] string? ExpectedJsonSchema = null,
    JsonSchemaMapperConfiguration? Configuration = null,
    bool IsSourceGenSupported = true) : ITestData
{
    public Type Type => typeof(T);
    object? ITestData.Value => Value;
}

public interface ITestData
{
    Type Type { get; }

    object? Value { get; }

    /// <summary>
    /// The expected JSON schema for the value.
    /// Fall back to JsonSchemaGenerator as the source of truth if null.
    /// </summary>
    string? ExpectedJsonSchema { get; }

    bool IsSourceGenSupported { get; }

    JsonSchemaMapperConfiguration? Configuration { get; }
}
