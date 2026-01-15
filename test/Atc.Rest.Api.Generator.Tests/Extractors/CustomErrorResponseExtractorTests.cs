namespace Atc.Rest.Api.Generator.Tests.Extractors;

public class CustomErrorResponseExtractorTests
{
    [Fact]
    public void Extract_WithValidConfig_ReturnsClassParameters()
    {
        // Arrange
        var config = new CustomErrorResponseModelConfig
        {
            Name = "ApiError",
            Description = "Custom error response",
            Schema = new Dictionary<string, CustomErrorPropertyConfig>(StringComparer.Ordinal)
            {
                ["code"] = new() { DataType = "string?", Description = "Error code" },
                ["message"] = new() { DataType = "string?", Description = "Error message" },
            },
        };

        // Act
        var result = CustomErrorResponseExtractor.Extract(config, "TestApi.Generated");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ApiError", result.ClassTypeName);
        Assert.Equal("TestApi.Generated", result.Namespace);
        Assert.NotNull(result.Properties);
        Assert.Equal(2, result.Properties.Count);
    }

    [Fact]
    public void Extract_PropertyNames_ArePascalCased()
    {
        // Arrange
        var config = new CustomErrorResponseModelConfig
        {
            Name = "ErrorResponse",
            Schema = new Dictionary<string, CustomErrorPropertyConfig>(StringComparer.Ordinal)
            {
                ["error_code"] = new() { DataType = "string?" },
                ["errorMessage"] = new() { DataType = "string?" },
            },
        };

        // Act
        var result = CustomErrorResponseExtractor.Extract(config, "TestApi.Generated");

        // Assert
        Assert.NotNull(result.Properties);
        var propertyNames = result.Properties.Select(p => p.Name).ToList();
        Assert.Contains("ErrorCode", propertyNames);
        Assert.Contains("ErrorMessage", propertyNames);
    }

    [Fact]
    public void Extract_SetsJsonNameWhenKeyDiffersFromPascalCasedName()
    {
        // Arrange
        var config = new CustomErrorResponseModelConfig
        {
            Name = "ErrorResponse",
            Schema = new Dictionary<string, CustomErrorPropertyConfig>(StringComparer.Ordinal)
            {
                ["error_code"] = new() { DataType = "string?" }, // snake_case -> ErrorCode
                ["message"] = new() { DataType = "string?" },    // lowercase -> Message
                ["Status"] = new() { DataType = "int" },         // Already PascalCase
            },
        };

        // Act
        var result = CustomErrorResponseExtractor.Extract(config, "TestApi.Generated");

        // Assert
        Assert.NotNull(result.Properties);

        // error_code -> ErrorCode, key differs so JsonName is set
        var errorCodeProp = result.Properties.First(p => p.Name == "ErrorCode");
        Assert.Equal("error_code", errorCodeProp.JsonName);

        // message -> Message, key differs (case) so JsonName is set
        var messageProp = result.Properties.First(p => p.Name == "Message");
        Assert.Equal("message", messageProp.JsonName);

        // Status -> Status, key matches exactly so no JsonName needed
        var statusProp = result.Properties.First(p => p.Name == "Status");
        Assert.Null(statusProp.JsonName);
    }

    [Fact]
    public void Extract_HandlesNullableTypes()
    {
        // Arrange
        var config = new CustomErrorResponseModelConfig
        {
            Name = "ErrorResponse",
            Schema = new Dictionary<string, CustomErrorPropertyConfig>(StringComparer.Ordinal)
            {
                ["code"] = new() { DataType = "string?" },
                ["status"] = new() { DataType = "int" },
                ["context"] = new() { DataType = "object?" },
            },
        };

        // Act
        var result = CustomErrorResponseExtractor.Extract(config, "TestApi.Generated");

        // Assert
        Assert.NotNull(result.Properties);

        var codeProp = result.Properties.First(p => p.Name == "Code");
        Assert.Equal("string", codeProp.TypeName);
        Assert.True(codeProp.IsNullableType);

        var statusProp = result.Properties.First(p => p.Name == "Status");
        Assert.Equal("int", statusProp.TypeName);
        Assert.False(statusProp.IsNullableType);

        var contextProp = result.Properties.First(p => p.Name == "Context");
        Assert.Equal("object", contextProp.TypeName);
        Assert.True(contextProp.IsNullableType);
    }

    [Fact]
    public void Extract_DefaultsDataTypeToNullableString()
    {
        // Arrange
        var config = new CustomErrorResponseModelConfig
        {
            Name = "ErrorResponse",
            Schema = new Dictionary<string, CustomErrorPropertyConfig>(StringComparer.Ordinal)
            {
                ["code"] = new() { DataType = null! }, // No data type specified
            },
        };

        // Act
        var result = CustomErrorResponseExtractor.Extract(config, "TestApi.Generated");

        // Assert
        Assert.NotNull(result.Properties);
        var codeProp = result.Properties.First(p => p.Name == "Code");
        Assert.Equal("string", codeProp.TypeName);
        Assert.True(codeProp.IsNullableType);
    }

    [Fact]
    public void Extract_IncludesDocumentationTags()
    {
        // Arrange
        var config = new CustomErrorResponseModelConfig
        {
            Name = "ErrorResponse",
            Description = "Represents an error from the API",
            Schema = new Dictionary<string, CustomErrorPropertyConfig>(StringComparer.Ordinal)
            {
                ["code"] = new() { DataType = "string?", Description = "The error code" },
            },
        };

        // Act
        var result = CustomErrorResponseExtractor.Extract(config, "TestApi.Generated");

        // Assert
        Assert.NotNull(result.DocumentationTags);
        Assert.Equal("Represents an error from the API", result.DocumentationTags.Summary);

        Assert.NotNull(result.Properties);
        var codeProp = result.Properties.First(p => p.Name == "Code");
        Assert.NotNull(codeProp.DocumentationTags);
        Assert.Equal("The error code", codeProp.DocumentationTags.Summary);
    }

    [Fact]
    public void Extract_HasGeneratedCodeAttribute()
    {
        // Arrange
        var config = new CustomErrorResponseModelConfig
        {
            Name = "ErrorResponse",
            Schema = new Dictionary<string, CustomErrorPropertyConfig>(StringComparer.Ordinal)
            {
                ["code"] = new() { DataType = "string?" },
            },
        };

        // Act
        var result = CustomErrorResponseExtractor.Extract(config, "TestApi.Generated");

        // Assert
        Assert.NotNull(result.Attributes);
        var generatedCodeAttr = result.Attributes.FirstOrDefault(a => a.Name == "GeneratedCode");
        Assert.NotNull(generatedCodeAttr);
    }

    [Fact]
    public void Extract_ThrowsOnNullConfig()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            CustomErrorResponseExtractor.Extract(null!, "TestApi.Generated"));
    }

    [Fact]
    public void Generate_ProducesValidCSharpCode()
    {
        // Arrange
        var config = new CustomErrorResponseModelConfig
        {
            Name = "ApiError",
            Description = "Represents an API error",
            Schema = new Dictionary<string, CustomErrorPropertyConfig>(StringComparer.Ordinal)
            {
                ["status"] = new() { DataType = "string?", Description = "Status code" },
                ["message"] = new() { DataType = "string?", Description = "Error message" },
                ["error_code"] = new() { DataType = "string?", Description = "Error code" },
            },
        };

        // Act
        var code = CustomErrorResponseExtractor.Generate(config, "TestApi.Generated");

        // Assert
        Assert.NotNull(code);
        Assert.Contains("namespace TestApi.Generated;", code, StringComparison.Ordinal);
        Assert.Contains("public sealed class ApiError", code, StringComparison.Ordinal);
        Assert.Contains("public string? Status { get; set; }", code, StringComparison.Ordinal);
        Assert.Contains("public string? Message { get; set; }", code, StringComparison.Ordinal);
        Assert.Contains("public string? ErrorCode { get; set; }", code, StringComparison.Ordinal);
        Assert.Contains("[JsonPropertyName(\"error_code\")]", code, StringComparison.Ordinal);
        Assert.Contains("[GeneratedCode(", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_IncludesRequiredUsings()
    {
        // Arrange
        var config = new CustomErrorResponseModelConfig
        {
            Name = "ErrorResponse",
            Schema = new Dictionary<string, CustomErrorPropertyConfig>(StringComparer.Ordinal)
            {
                ["code"] = new() { DataType = "string?" },
            },
        };

        // Act
        var code = CustomErrorResponseExtractor.Generate(config, "TestApi.Generated");

        // Assert
        Assert.Contains("using System.CodeDom.Compiler;", code, StringComparison.Ordinal);
        Assert.Contains("using System.Text.Json.Serialization;", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_HandlesEmptySchema()
    {
        // Arrange
        var config = new CustomErrorResponseModelConfig
        {
            Name = "EmptyError",
            Schema = new Dictionary<string, CustomErrorPropertyConfig>(StringComparer.Ordinal),
        };

        // Act
        var code = CustomErrorResponseExtractor.Generate(config, "TestApi.Generated");

        // Assert
        Assert.NotNull(code);
        Assert.Contains("public sealed class EmptyError", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_HandlesNullSchema()
    {
        // Arrange
        var config = new CustomErrorResponseModelConfig
        {
            Name = "NoSchemaError",
            Schema = null,
        };

        // Act
        var code = CustomErrorResponseExtractor.Generate(config, "TestApi.Generated");

        // Assert
        Assert.NotNull(code);
        Assert.Contains("public sealed class NoSchemaError", code, StringComparison.Ordinal);
    }
}