# stj-schema-mapper

A JSON schema generator for System.Text.Json, deriving metadata information from the `JsonTypeInfo` contract type. The library targets `netstandard2.0` and is safe to use in AOT applications.

## Using the library

The API surface is fairly straightforward, exposing a couple of extension methods acting on `JsonSerializerOptions` and `JsonTypeInfo`. Here is a simple example:

```C#
var options = JsonSerializerOptions.Default;
JsonObject schema = options.GetJsonSchema(typeof(Person));
// { 
//   "$schema": "https://json-schema.org/draft/2020-12/schema",
//   "description": "A person record.",
//   "type": "object",
//   "properties": { 
//     "Name": { "type": "string", "description": "The name" },
//     "Age": { "type": "integer", "description": "The age" },
//     "Address": { "type": ["string", "null"], "description": "The address (default value: null)" }
//   },
//   "required": ["Name", "Age"]
// }

[Description("A person record.")]
public record Person(
    [Description("The name")] string Name, 
    [Description("The age")] int Age, 
    [Description("The address")] string? Address = null);
```

Schema generation can be configured using using the `JsonSchemaMapperConfiguration` type:

```C#
var config = new JsonSchemaMapperConfiguration { IncludeSchemaVersion = false, ResolveDescriptionAttributes = false };
JsonObject schema = PersonContext.Default.Person.GetJsonSchema(config);
// { 
//   "type": "object",
//   "properties": { 
//     "Name": { "type": "string" },
//     "Age": { "type": "integer" },
//     "Address": { "type": ["string", "null"], "description": "default value: null" }
//   },
//   "required": ["Name", "Age"]  
// }

[JsonSerializable(typeof(Person))]
public partial class PersonContext : JsonSerializerContext;
```

## Method schema generation

The library also supports generating JSON schemas for methods, for example:

```C#
MethodInfo method = typeof(TestMethods).GetMethod(nameof(TestMethods.MyMethod))!;
JsonObject schema = JsonSerializerOptions.Default.GetJsonSchema(method);
// { 
//   "$schema": "https://json-schema.org/draft/2020-12/schema",
//   "description": "A friendly method.",
//   "type": "object",
//   "properties": { 
//     "Name": { "type": "string" },
//     "Age": { "type": "integer" },
//     "Address": { "type": ["string", "null"], "description": "default value: null" }
//   },
//   "required": ["Name", "Age"]
// }

public static class TestMethods
{
    [Description("A friendly method.")]
    public static void MyMethod(string name, int age, string? address = null) { }
}
```