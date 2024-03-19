using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Xunit;

namespace JsonSchemaMapper.Tests;

public abstract class JsonSchemaMapperTests
{
    protected abstract JsonSerializerOptions Options { get; }
    protected bool IsSourceGeneratedTestSuite => Options.TypeInfoResolver is JsonSerializerContext;

    [Theory]
    [MemberData(nameof(TestTypes.GetTestData), MemberType = typeof(TestTypes))]
    public void TestTypes_GeneratesExpectedJsonSchema(ITestData testData)
    {
        if (!testData.IsSourceGenSupported && IsSourceGeneratedTestSuite)
        {
            return;
        }

        JsonObject schema = Options.GetJsonSchema(testData.Type);
        Helpers.AssertValidJsonSchema(testData.Type, testData.ExpectedJsonSchema, schema);
    }

    [Theory]
    [MemberData(nameof(TestTypes.GetTestData), MemberType = typeof(TestTypes))]
    public void TestTypes_SerializedValueMatchesGeneratedSchema(ITestData testData)
    {
        JsonObject schema = Options.GetJsonSchema(testData.Type);
        JsonNode? instance = JsonSerializer.SerializeToNode(testData.Value, testData.Type, Options);
        Helpers.AssertDocumentMatchesSchema(schema, instance);
    }

    [Fact]
    public void GetJsonSchema_NullInputs_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ((JsonSerializerOptions)null!).GetJsonSchema(typeof(int)));
        Assert.Throws<ArgumentNullException>(() => Options.GetJsonSchema(null!));
    }

    [Fact]
    public void ToJsonSchema_NullInputs_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ((JsonTypeInfo)null!).ToJsonSchema());
    }

    [Fact]
    public void GetJsonSchema_RequiresReadOnlyOptions()
    {
        var options = new JsonSerializerOptions(Options);
        Assert.False(options.IsReadOnly);
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
}

public sealed class ReflectionJsonSchemaMapperTests : JsonSchemaMapperTests
{
    protected override JsonSerializerOptions Options => JsonSerializerOptions.Default;
}

public sealed class SourceGenJsonSchemaMapperTests : JsonSchemaMapperTests
{
    protected override JsonSerializerOptions Options => TestTypes.TestTypesContext.Default.Options;
}