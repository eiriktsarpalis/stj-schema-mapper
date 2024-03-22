using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace JsonSchemaMapper.Tests;

internal static class TestMethods
{
    public static IEnumerable<object[]> GetTestData()
    {
        yield return Create(
            nameof(ZeroParameters),
            """
            {
                "description" : "ZeroParameters",
                "type": "object"
            }
            """);

        yield return Create(
            nameof(OneIntegerParameter),
            """
            {
                "description" : "OneIntegerParameter",
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
                "description" : "OneStringParameter",
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
                "description" : "OnePocoParameter",
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
                "description" : "MultipleParameters",
                "type": "object",
                "properties": {
                    "x1": { "type": "string" },
                    "x2": { "type": "integer" },
                    "x3": { "description": "default value: null", "type": ["string", "null"] },
                    "x4": { "description": "default value: 42", "type": ["integer", "null"] },
                    "x5": { "description": "default value: \"A\"", "enum": ["A", "B", "C"] },
                    "x6": {
                        "description": "default value: null",
                        "type": ["object","null"],
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
                "description" : "Method description",
                "type": "object",
                "properties": {
                    "x1": { "description": "x1 description", "type": "string" },
                    "x2": { "description": "x2 description", "type": "integer" },
                    "x3": { "description": "x3 description (default value: null)", "type": ["string", "null"] },
                    "x4": { "description": "x4 description (default value: 42)", "type": ["integer", "null"] },
                    "x5": { "description": "x5 description (default value: \"A\")", "enum": ["A", "B", "C"] },
                    "x6": {
                        "description": "x6 description (default value: null)",
                        "type": ["object","null"],
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
