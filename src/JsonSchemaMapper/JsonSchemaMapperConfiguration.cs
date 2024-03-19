using System.ComponentModel;

namespace JsonSchemaMapper;

/// <summary>
/// Controls the behavior of the <see cref="JsonSchemaMapper"/> class.
/// </summary>
public class JsonSchemaMapperConfiguration
{
    /// <summary>
    /// Gets the default configuration object used by <see cref="JsonSchemaMapper"/>.
    /// </summary>
    public static JsonSchemaMapperConfiguration Default { get; } = new();

    private readonly int _maxDepth = 64;

    /// <summary>
    /// Determines whether schema references using JSON pointers should be generated for repeated complex types.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="true"/>. Should be left enabled if recursive types (e.g. trees, linked lists) are expected.
    /// </remarks>
    public bool AllowSchemaReferences { get; init; } = true;

    /// <summary>
    /// Determines whether the '$schema' property should be included in the root schema document.
    /// </summary>
    /// <remarks>
    /// Defaults to true.
    /// </remarks>
    public bool IncludeSchemaVersion { get; init; } = true;

    /// <summary>
    /// Determines whether the <see cref="DescriptionAttribute"/> should be resolved for types and properties.
    /// </summary>
    /// <remarks>
    /// Defaults to true.
    /// </remarks>
    public bool ResolveDescriptionAttributes { get; init; } = true;

    /// <summary>
    /// Determines the maximum permitted depth when traversing the generated type graph.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is less than 0.</exception>
    /// <remarks>
    /// Defaults to 64.
    /// </remarks>
    public int MaxDepth
    {
        get => _maxDepth;
        init
        {
            if (value < 0)
            {
                Throw();
                static void Throw() => throw new ArgumentOutOfRangeException(nameof(value));
            }

            _maxDepth = value;
        }
    }
}
