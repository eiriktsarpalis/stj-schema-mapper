# stj-schema-mapper [![Build & Tests](https://github.com/eiriktsarpalis/stj-schema-mapper/actions/workflows/build.yml/badge.svg)](https://github.com/eiriktsarpalis/stj-schema-mapper/actions/workflows/build.yml)

A JSON schema generator for System.Text.Json, deriving metadata information from the `JsonTypeInfo` contract type. The library targets `netstandard2.0` and is safe to use in AOT applications.

For projects targeting System.Text.Json version 9 or later, the library is implemented as a shim over the new `JsonSchemaExporter` class. Users are encouraged to migrate System.Text.Json v9 and `JsonSchemaExporter` as soon as convenient. The shim implementation is enabled for `net9.0` targets or greater but can also be turned on universally using the `SYSTEM_TEXT_JSON_V9` build conditional.

## Using the library

The API surface is fairly straightforward, exposing a couple of extension methods acting on `JsonSerializerOptions` and `JsonTypeInfo`. Here is a simple example:

```C#
JsonSerializerOptions options = JsonSerializerOptions.Default;
JsonNode schema = options.GetJsonSchema(typeof(Person));
// { 
//   "$schema": "https://json-schema.org/draft/2020-12/schema",
//   "description": "A person record.",
//   "type": ["object","null"],
//   "properties": { 
//     "Name": {  "description": "The name", "type": "string" },
//     "Age": { "description": "The age", "type": "integer" },
//     "Address": { "description": "The address", "type": ["string", "null"], "default": null }
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
var config = new JsonSchemaMapperConfiguration { IncludeSchemaVersion = false, TreatNullObliviousAsNonNullable = true };
JsonNode schema = PersonContext.Default.Person.GetJsonSchema(config);
// { 
//   "type": "object",
//   "properties": { 
//     "Name": { "type": "string" },
//     "Age": { "type": "integer" },
//     "Address": { "type": ["string", "null"], "default": null }
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
JsonNode schema = JsonSerializerOptions.Default.GetJsonSchema(method);
// { 
//   "$schema": "https://json-schema.org/draft/2020-12/schema",
//   "title": "MyMethod",
//   "description": "A friendly method.",
//   "type": ["object","null"],
//   "properties": { 
//     "Name": { "type": "string" },
//     "Age": { "type": "integer" },
//     "Address": { "type": ["string", "null"], "default": "null" }
//   },
//   "required": ["Name", "Age"]
// }

public static class TestMethods
{
    [Description("A friendly method.")]
    public static void MyMethod(string name, int age, string? address = null) { }
}
```
