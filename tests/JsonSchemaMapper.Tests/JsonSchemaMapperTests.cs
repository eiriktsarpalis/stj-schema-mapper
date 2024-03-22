using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Xunit;

namespace JsonSchemaMapper.Tests;

public abstract class JsonSchemaMapperTests
{
    protected abstract JsonSerializerOptions Options { get; }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestData), MemberType = typeof(TestTypes))]
    public void TestTypes_GeneratesExpectedJsonSchema(ITestData testData)
    {
        JsonObject schema = Options.GetJsonSchema(testData.Type, testData.Configuration);
        Helpers.AssertValidJsonSchema(testData.Type, testData.ExpectedJsonSchema, schema);
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestDataUsingAllValues), MemberType = typeof(TestTypes))]
    public void TestTypes_SerializedValueMatchesGeneratedSchema(ITestData testData)
    {
        JsonObject schema = Options.GetJsonSchema(testData.Type, testData.Configuration);
        JsonNode? instance = JsonSerializer.SerializeToNode(testData.Value, testData.Type, Options);
        Helpers.AssertDocumentMatchesSchema(schema, instance);
    }

    [Theory]
    [MemberData(nameof(TestMethods.GetTestData), MemberType = typeof(TestMethods))]
    public void TestMethods_GeneratesExpectedJsonSchema(MethodBase method, string expectedJsonSchema)
    {
        JsonObject schema = Options.GetJsonSchema(method);
        Helpers.AssertValidJsonSchema(null!, expectedJsonSchema, schema);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IncludeSchemaVersion_ControlsSchemaProperty(bool includeSchemaVersion)
    {
        var config = new JsonSchemaMapperConfiguration { IncludeSchemaVersion = includeSchemaVersion };
        JsonObject schema = Options.GetJsonSchema(typeof(TestTypes.SimplePoco), config);
        Assert.Equal(includeSchemaVersion, schema.ContainsKey("$schema"));
        if (includeSchemaVersion)
        {
            Assert.Equal(JsonSchemaMapper.SchemaVersion, (string)schema["$schema"]!);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResolveDescriptionAttributes_ControlsDescriptionProperty(bool resolveDescriptionAttributes)
    {
        var config = new JsonSchemaMapperConfiguration { ResolveDescriptionAttributes = resolveDescriptionAttributes };
        JsonObject schema = Options.GetJsonSchema(typeof(TestTypes.PocoWithDescription), config);
        Assert.Equal(resolveDescriptionAttributes, schema.ContainsKey("description"));
        if (resolveDescriptionAttributes)
        {
            Assert.Equal("The type description", (string)schema["description"]!);
        }
    }

    [Theory]
    [InlineData(typeof(string), "string")]
    [InlineData(typeof(int[]), "array")]
    [InlineData(typeof(Dictionary<string, int>), "object")]
    [InlineData(typeof(TestTypes.SimplePoco), "object")]
    public void ResolveNullableReferenceTypes_Disabled_MarksAllReferenceTypesAsNullable(Type referenceType, string expectedType)
    {
        Assert.True(!referenceType.IsValueType);
        var config = new JsonSchemaMapperConfiguration { ResolveNullableReferenceTypes = false };
        JsonObject schema = Options.GetJsonSchema(referenceType, config);
        JsonArray arr = Assert.IsType<JsonArray>(schema["type"]);
        Assert.Equal([expectedType, "null"], arr.Select(e => (string)e!));
    }

    [Theory]
    [InlineData(typeof(int), "integer")]
    [InlineData(typeof(double), "number")]
    [InlineData(typeof(bool), "boolean")]
    [InlineData(typeof(ImmutableArray<int>), "array")]
    [InlineData(typeof(TestTypes.StructDictionary<string, int>), "object")]
    [InlineData(typeof(TestTypes.SimpleRecordStruct), "object")]
    public void ResolveNullableReferenceTypes_Disabled_DoesNotImpactNonReferenceTypes(Type referenceType, string expectedType)
    {
        Assert.True(referenceType.IsValueType);
        var config = new JsonSchemaMapperConfiguration { ResolveNullableReferenceTypes = false };
        JsonObject schema = Options.GetJsonSchema(referenceType, config);
        JsonValue value = Assert.IsAssignableFrom<JsonValue>(schema["type"]);
        Assert.Equal(expectedType, (string)value!);
    }

    [Fact]
    public void ResolveNullableReferenceTypes_Disabled_DoesNotImpactObjectType()
    {
        var config = new JsonSchemaMapperConfiguration { ResolveNullableReferenceTypes = false };
        JsonObject schema = Options.GetJsonSchema(typeof(object), config);
        Assert.DoesNotContain("type", schema);
    }

    [Theory]
    [InlineData(typeof(TestTypes.SimpleRecord))]
    [InlineData(typeof(TestTypes.RecordWithOptionalParameters))]
    public void RequireConstructorParameters_Disabled_TypeWithConstructorHasNoRequiredProperties(Type type)
    {
        var config = new JsonSchemaMapperConfiguration { RequireConstructorParameters = false };
        JsonObject schema = Options.GetJsonSchema(type, config);
        Assert.DoesNotContain("required", schema);
    }

    [Fact]
    public void TypeWithDisallowUnmappedMembers_AdditionalPropertiesFailValidation()
    {
        JsonObject schema = Options.GetJsonSchema(typeof(TestTypes.PocoDisallowingUnmappedMembers));
        JsonNode? jsonWithUnmappedProperties = JsonNode.Parse("""{ "UnmappedProperty" : {} }""");
        Helpers.AssertDoesNotMatchSchema(schema, jsonWithUnmappedProperties);
    }

    [Fact]
    public void GetJsonSchema_NullInputs_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ((JsonSerializerOptions)null!).GetJsonSchema(typeof(int)));
        Assert.Throws<ArgumentNullException>(() => ((JsonSerializerOptions)null!).GetJsonSchema(typeof(int).GetMethods().First()));
        Assert.Throws<ArgumentNullException>(() => Options.GetJsonSchema((Type)null!));
        Assert.Throws<ArgumentNullException>(() => Options.GetJsonSchema((MethodBase)null!));
        Assert.Throws<ArgumentNullException>(() => ((JsonTypeInfo)null!).GetJsonSchema());
    }

    [Fact]
    public void GetJsonSchema_NoResolver_ThrowInvalidOperationException()
    {
        var options = new JsonSerializerOptions();
        Assert.Throws<InvalidOperationException>(() => options.GetJsonSchema(typeof(int)));
    }

    [Fact]
    public void AllowSchemaReferences_Disabled_RecursiveType_ThrowsInvalidOperationException()
    {
        var config = new JsonSchemaMapperConfiguration { AllowSchemaReferences = false };
        var ex = Assert.Throws<InvalidOperationException>(() => Options.GetJsonSchema(typeof(TestTypes.PocoWithRecursiveMembers), config));
        Assert.Contains("The maximum depth of the schema has been reached", ex.Message);
    }

    [Fact]
    public void MaxDepth_SetToZero_NonTrivialSchema_ThrowsInvalidOperationException()
    {
        var config = new JsonSchemaMapperConfiguration { MaxDepth = 0 };
        var ex = Assert.Throws<InvalidOperationException>(() => Options.GetJsonSchema(typeof(TestTypes.SimplePoco), config));
        Assert.Contains("The maximum depth of the schema has been reached", ex.Message);
    }

    [Fact]
    public void ReferenceHandlePreserve_Enabled_ThrowsNotSupportedException()
    {
        var options = new JsonSerializerOptions(Options) { ReferenceHandler = ReferenceHandler.Preserve };
        options.MakeReadOnly();

        var ex = Assert.Throws<NotSupportedException>(() => options.GetJsonSchema(typeof(TestTypes.SimplePoco)));
        Assert.Contains("ReferenceHandler.Preserve", ex.Message);
    }
}

public sealed class ReflectionJsonSchemaMapperTests : JsonSchemaMapperTests
{
    protected override JsonSerializerOptions Options => JsonSerializerOptions.Default;
}

public sealed class SourceGenJsonSchemaMapperTests : JsonSchemaMapperTests
{
    protected override JsonSerializerOptions Options => TestTypes.TestTypesContext.Default.Options;
}