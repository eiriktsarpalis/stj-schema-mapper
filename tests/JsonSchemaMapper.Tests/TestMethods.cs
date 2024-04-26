using System.ComponentModel;
using System.Reflection;

namespace JsonSchemaMapper.Tests;

internal static class TestMethods
{
    public static IEnumerable<object[]> GetTestData()
    {
        yield return Create(
            nameof(ZeroParameters),
            """
            {
                "title" : "ZeroParameters",
                "type": "object"
            }
            """);

        yield return Create(
            nameof(OneIntegerParameter),
            """
            {
                "title" : "OneIntegerParameter",
                "type": "object",
                "properties": {
                    "value": { "type": "integer" }
                },
                "required": ["value"]
            }
            """);

        yield return Create(
            nameof(OneStringParameter),
            """
            {
                "title" : "OneStringParameter",
                "type": "object",
                "properties": {
                    "value": { "type": "string" }
                },
                "required": ["value"]
            }
            """);

        yield return Create(
            nameof(OnePocoParameter),
            """
            {
                "title" : "OnePocoParameter",
                "type": "object",
                "properties": {
                    "value": {
                        "type": "object",
                        "properties": {
                            "String": { "type": "string" },
                            "StringNullable": { "type": ["string", "null"] },
                            "Int": { "type": "integer" },
                            "Double": { "type": "number" },
                            "Boolean": { "type": "boolean" }
                        }
                    }
                },
                "required": ["value"]
            }
            """);

        yield return Create(
            nameof(MultipleParameters),
            """
            {
                "title" : "MultipleParameters",
                "type": "object",
                "properties": {
                    "x1": { "type": "string" },
                    "x2": { "type": "integer" },
                    "x3": { "type": ["string", "null"], "default": null },
                    "x4": { "type": ["integer", "null"], "default" : 42 },
                    "x5": { "type": "string", "enum": ["A", "B", "C"], "default" : "A" },
                    "x6": {
                        "type": ["object","null"],
                        "default" : null,
                        "properties": {
                            "String": { "type": "string" },
                            "StringNullable": { "type": ["string", "null"] },
                            "Int": { "type": "integer" },
                            "Double": { "type": "number" },
                            "Boolean": { "type": "boolean" }
                        }
                    }
                },
                "required": ["x1", "x2"]
            }
            """);

        yield return Create(
            nameof(DescriptionAttributeParameters),
            """
            {
                "title" : "DescriptionAttributeParameters",
                "description" : "Method description",
                "type": "object",
                "properties": {
                    "x1": { "description": "x1 description", "type": "string" },
                    "x2": { "description": "x2 description", "type": "integer" },
                    "x3": { "description": "x3 description", "type": ["string", "null"], "default": null },
                    "x4": { "description": "x4 description", "type": ["integer", "null"], "default": 42 },
                    "x5": { "description": "x5 description", "type": "string", "enum": ["A", "B", "C"], "default": "A" },
                    "x6": {
                        "description": "x6 description",
                        "type": ["object","null"],
                        "default": null,
                        "properties": {
                            "String": { "type": "string" },
                            "StringNullable": { "type": ["string", "null"] },
                            "Int": { "type": "integer" },
                            "Double": { "type": "number" },
                            "Boolean": { "type": "boolean" }
                        }
                    }
                },
                "required": ["x1", "x2"]
            }
            """);

        static object[] Create(string methodName, string expectedJsonSchema)
        {
            MethodBase method = typeof(TestMethods).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)!;
            return [method, expectedJsonSchema];
        }
    }

    public static void ZeroParameters() => throw new NotImplementedException();
    public static void OneIntegerParameter(int value) => throw new NotImplementedException();
    public static void OneStringParameter(string value) => throw new NotImplementedException();
    public static void OnePocoParameter(TestTypes.SimplePoco value) => throw new NotImplementedException();
    public static void MultipleParameters(
        string x1, 
        int x2, 
        string? x3 = null, 
        int? x4 = 42, 
        TestTypes.StringEnum x5 = TestTypes.StringEnum.A,
        TestTypes.SimplePoco? x6 = null) => 
        
        throw new NotImplementedException();

    [Description("Method description")]
    public static void DescriptionAttributeParameters(
        [Description("x1 description")] string x1,
        [Description("x2 description")] int x2,
        [Description("x3 description")] string? x3 = null,
        [Description("x4 description")] int? x4 = 42,
        [Description("x5 description")] TestTypes.StringEnum x5 = TestTypes.StringEnum.A,
        [Description("x6 description")] TestTypes.SimplePoco? x6 = null) => 

        throw new NotImplementedException();
}
