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
        Assert.True(configuration.AllowSchemaReferences);
        Assert.True(configuration.IncludeSchemaVersion);
        Assert.True(configuration.ResolveDescriptionAttributes);
        Assert.Equal(64, configuration.MaxDepth);
    }

    [Fact]
    public static void JsonSchemaMapperConfiguration_Singleton_ReturnsSameInstance()
    {
        Assert.Same(JsonSchemaMapperConfiguration.Default, JsonSchemaMapperConfiguration.Default);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void JsonSchemaMapperConfiguration_AllowSchemaReferences(bool allowSchemaReferences)
    {
        JsonSchemaMapperConfiguration configuration = new() { AllowSchemaReferences = allowSchemaReferences };
        Assert.Equal(allowSchemaReferences, configuration.AllowSchemaReferences);
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
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public static void JsonSchemaMapperConfiguration_MaxDepth_ValidValue(int maxDepth)
    {
        JsonSchemaMapperConfiguration configuration = new() { MaxDepth = maxDepth };
        Assert.Equal(maxDepth, configuration.MaxDepth);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-2)]
    [InlineData(int.MinValue)]
    public static void JsonSchemaMapperConfiguration_MaxDepth_InvalidValue_ThrowsArgumentOutOfRangeException(int maxDepth)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new JsonSchemaMapperConfiguration { MaxDepth = maxDepth });
    }
}
