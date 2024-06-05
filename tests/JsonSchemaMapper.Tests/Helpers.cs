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
        JsonNode? expectedJsonSchemaNode = expectedJsonSchema != null
            ? JsonNode.Parse(expectedJsonSchema)
            : JsonSerializer.SerializeToNode(new JsonSchemaBuilder().FromType(type), Context.Default.JsonSchema);

        // Trim the $schema property from actual schema since it's not included by the generator.
        actualJsonSchema.Remove("$schema");

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
        EvaluationResults results = EvaluateSchemaCore(schema, instance);
        if (!results.IsValid)
        {
            IEnumerable<string> errors = results.Details
                .Where(d => d.HasErrors)
                .SelectMany(d => d.Errors!.Select(error => $"Path:${d.InstanceLocation} {error.Key}:{error.Value}"));

            throw new XunitException($"""
                Instance JSON document does not match the specified schema.
                Schema:
                {FormatJson(schema)}
                Instance:
                {FormatJson(instance)}
                Errors:
                {string.Join(Environment.NewLine, errors)}
                """);
        }
    }

    public static void AssertDoesNotMatchSchema(JsonObject schema, JsonNode? instance)
    {
        EvaluationResults results = EvaluateSchemaCore(schema, instance);
        if (results.IsValid)
        {
            throw new XunitException($"""
                Instance JSON document matches the specified schema.
                Schema:
                {FormatJson(schema)}
                Instance:
                {FormatJson(instance)}
                """);
        }
    }

    private static EvaluationResults EvaluateSchemaCore(JsonNode schema, JsonNode? instance)
    {
        JsonSchema jsonSchema = JsonSerializer.Deserialize(schema, Context.Default.JsonSchema)!;
        EvaluationOptions options = new()
        {
            OutputFormat = OutputFormat.List,
            RequireFormatValidation = true // format does not validate by default
        };
        return jsonSchema.Evaluate(instance, options);
    }

    private static string FormatJson(JsonNode? node) =>
        JsonSerializer.Serialize(node, Context.Default.JsonNode!);

    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(JsonNode))]
    [JsonSerializable(typeof(JsonSchema))]
    [JsonSourceGenerationOptions(WriteIndented = true)]
    private partial class Context : JsonSerializerContext;
}
