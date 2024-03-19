using Json.More;
using Json.Schema;
using Json.Schema.Generation;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Xunit.Sdk;

namespace JsonSchemaMapper.Tests;

internal static partial class Helpers
{
    public static void AssertValidJsonSchema(Type type, string? expectedJsonSchema, JsonObject actualJsonSchema)
    {
        // If an expected schema is provided, use that. Otherwise, generate a schema from the type.
        JsonSchema expectedJsonSchemaNet = expectedJsonSchema != null
            ? ParseSchemaCore(expectedJsonSchema)
            : new JsonSchemaBuilder().FromType(type);

        // Trim the $schema property from actual schema since it's not included by the schema builder.
        actualJsonSchema.Remove("$schema");

        JsonNode? expectedJsonSchemaNode = JsonSerializer.SerializeToNode(expectedJsonSchemaNet, Context.Default.JsonSchema);
        if (!JsonNode.DeepEquals(expectedJsonSchemaNode, actualJsonSchema))
        {
            throw new XunitException($"""
                Generated schema does not match the expected specification.
                Expected:
                {FormatJson(expectedJsonSchemaNode)}
                Actual:
                {FormatJson(actualJsonSchema)}
                """);
        }
    }

    public static void AssertDocumentMatchesSchema(JsonObject schema, JsonNode? instance)
    {
        JsonSchema jsonSchema = ParseSchemaCore(schema);
        EvaluationResults results = jsonSchema.Evaluate(instance);
        if (results.HasErrors)
        {
            throw new XunitException($"""
                Instance JSON document does not match the specified schema.
                Schema:
                {FormatJson(schema)}
                Instance:
                {FormatJson(instance)}
                """);
        }
    }

    private static JsonSchema ParseSchemaCore(JsonNode schema)
    {
        try
        {
            return JsonSerializer.Deserialize(schema, Context.Default.JsonSchema)!;
        }
        catch (Exception ex)
        {
            throw new XunitException($"""
                Document is not a valid JSON schema:
                {FormatJson(schema)}
                """, ex);
        }
    }

    private static JsonSchema ParseSchemaCore(string schema)
    {
        try
        {
            return JsonSerializer.Deserialize(schema, Context.Default.JsonSchema)!;
        }
        catch (Exception ex)
        {
            throw new XunitException($"""
                Document is not a valid JSON schema:
                {FormatJson(schema)}
                """, ex);
        }
    }

    private static string FormatJson(string json) =>
        FormatJson(JsonSerializer.Deserialize(json, Context.Default.JsonNode));

    private static string FormatJson(JsonNode? node) =>
        JsonSerializer.Serialize(node, Context.Default.JsonNode!);

    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(JsonNode))]
    [JsonSerializable(typeof(JsonSchema))]
    [JsonSourceGenerationOptions(WriteIndented = true)]
    private partial class Context : JsonSerializerContext;
}
