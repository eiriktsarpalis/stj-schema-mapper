using Xunit;

namespace JsonSchemaMapper.Tests;

public static class JsonSchemaMapperConfigurationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void JsonSchemaMapperConfiguration_DefaultValues(bool useSingleton)
    {
        JsonSchemaMapperConfiguration configuration = useSingleton ? JsonSchemaMapperConfiguration.Default : new();
        Assert.True(configuration.IncludeSchemaVersion);
        Assert.True(configuration.ResolveDescriptionAttributes);
        Assert.False(configuration.TreatNullObliviousAsNonNullable);
        Assert.False(configuration.IncludeTypeInEnums);
    }

    [Fact]
    public static void JsonSchemaMapperConfiguration_Singleton_ReturnsSameInstance()
    {
        Assert.Same(JsonSchemaMapperConfiguration.Default, JsonSchemaMapperConfiguration.Default);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void JsonSchemaMapperConfiguration_IncludeSchemaVersion(bool includeSchemaVersion)
    {
        JsonSchemaMapperConfiguration configuration = new() { IncludeSchemaVersion = includeSchemaVersion };
        Assert.Equal(includeSchemaVersion, configuration.IncludeSchemaVersion);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void JsonSchemaMapperConfiguration_ResolveDescriptionAttributes(bool resolveDescriptionAttributes)
    {
        JsonSchemaMapperConfiguration configuration = new() { ResolveDescriptionAttributes = resolveDescriptionAttributes };
        Assert.Equal(resolveDescriptionAttributes, configuration.ResolveDescriptionAttributes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void JsonSchemaMapperConfiguration_TreatNullObliviousAsNonNullable(bool treatNullObliviousAsNonNullable)
    {
        JsonSchemaMapperConfiguration configuration = new() { TreatNullObliviousAsNonNullable = treatNullObliviousAsNonNullable };
        Assert.Equal(treatNullObliviousAsNonNullable, configuration.TreatNullObliviousAsNonNullable);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void JsonSchemaMapperConfiguration_IncludeTypeInEnums(bool includeTypeInEnums)
    {
        JsonSchemaMapperConfiguration configuration = new() { IncludeTypeInEnums = includeTypeInEnums };
        Assert.Equal(includeTypeInEnums, configuration.IncludeTypeInEnums);
    }
}
