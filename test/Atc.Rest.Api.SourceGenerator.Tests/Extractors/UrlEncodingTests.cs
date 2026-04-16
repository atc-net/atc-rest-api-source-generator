namespace Atc.Rest.Api.SourceGenerator.Tests.Extractors;

/// <summary>
/// Tests for URL encoding logic in the HTTP client generator.
/// URL encoding is critical for RFC 3986 compliance and prevents:
/// - Broken URLs when values contain special characters (&amp;, =, #, spaces, /, etc.)
/// - Parameter injection vulnerabilities
/// - Malformed requests.
/// </summary>
[SuppressMessage("", "SA1512:Single-line comments should not be followed by blank line", Justification = "OK")]
public class UrlEncodingTests
{
    // ========== NeedsUrlEncoding - String Types ==========

    [Theory]
    [InlineData("string", true)]           // Plain string needs encoding
    [InlineData("string?", true)]          // Nullable string needs encoding
    public void NeedsUrlEncoding_StringTypes_ReturnsTrue(
        string csharpType,
        bool expected)
    {
        // Act
        var result = HttpClientExtractor.NeedsUrlEncoding(csharpType);

        // Assert
        Assert.Equal(expected, result);
    }

    // ========== NeedsUrlEncoding - Value Types (URL-safe) ==========

    [Theory]
    [InlineData("int", false)]             // Integer ToString() is URL-safe
    [InlineData("int?", false)]            // Nullable int
    [InlineData("long", false)]            // Long ToString() is URL-safe
    [InlineData("long?", false)]           // Nullable long
    [InlineData("short", false)]           // Short ToString() is URL-safe
    [InlineData("byte", false)]            // Byte ToString() is URL-safe
    [InlineData("bool", false)]            // "true"/"false" are URL-safe
    [InlineData("bool?", false)]           // Nullable bool
    [InlineData("float", false)]           // Float ToString() is URL-safe
    [InlineData("double", false)]          // Double ToString() is URL-safe
    [InlineData("decimal", false)]         // Decimal ToString() is URL-safe
    [InlineData("Guid", false)]            // GUID is hex + hyphens, always URL-safe
    [InlineData("Guid?", false)]           // Nullable GUID
    public void NeedsUrlEncoding_ValueTypes_ReturnsFalse(
        string csharpType,
        bool expected)
    {
        // Act
        var result = HttpClientExtractor.NeedsUrlEncoding(csharpType);

        // Assert
        Assert.Equal(expected, result);
    }

    // ========== NeedsUrlEncoding - Array Types ==========

    [Theory]
    [InlineData("string[]", false)]        // Arrays need special handling (not simple encoding)
    [InlineData("int[]", false)]           // Value type arrays
    [InlineData("Guid[]", false)]          // GUID arrays
    public void NeedsUrlEncoding_ArrayTypes_ReturnsFalse(
        string csharpType,
        bool expected)
    {
        // Arrange - Arrays are excluded from simple encoding because they require
        // per-element encoding which needs different handling

        // Act
        var result = HttpClientExtractor.NeedsUrlEncoding(csharpType);

        // Assert
        Assert.Equal(expected, result);
    }

    // ========== NeedsUrlEncoding - Custom/Reference Types (defensive encoding) ==========

    [Theory]
    [InlineData("MyCustomType", true)]     // Custom types encoded defensively (ToString() may contain URL-unsafe chars)
    [InlineData("object", true)]           // Object encoded defensively
    [InlineData("Pet", true)]              // Model type encoded defensively
    [InlineData("DateTimeOffset", true)]   // DateTimeOffset contains '+' for timezone offset
    [InlineData("DateTime", true)]         // DateTime may contain special chars
    [InlineData("DateOnly", true)]         // Date types encoded for safety
    [InlineData("TimeOnly", true)]         // Time types encoded for safety
    public void NeedsUrlEncoding_CustomTypes_ReturnsTrue(
        string csharpType,
        bool expected)
    {
        // Act
        var result = HttpClientExtractor.NeedsUrlEncoding(csharpType);

        // Assert
        Assert.Equal(expected, result);
    }

    // ========== Uri.EscapeDataString Behavior Verification ==========

    [Theory]
    [InlineData("hello world", "hello%20world")]           // Spaces
    [InlineData("a&b=c", "a%26b%3Dc")]                     // Query string chars
    [InlineData("user/admin", "user%2Fadmin")]             // Path separator
    [InlineData("test#hash", "test%23hash")]               // Fragment char
    [InlineData("100%", "100%25")]                         // Percent sign
    [InlineData("key=value", "key%3Dvalue")]               // Equals sign
    [InlineData("foo+bar", "foo%2Bbar")]                   // Plus sign
    [InlineData("hello?query", "hello%3Fquery")]           // Question mark
    [InlineData("", "")]                                   // Empty string
    public void UriEscapeDataString_EncodesSpecialCharacters(
        string input,
        string expected)
    {
        // Act - This test verifies the encoding behavior we rely on
        var result = Uri.EscapeDataString(input);

        // Assert
        Assert.Equal(expected, result);
    }

    // ========== Edge Cases ==========

    [Theory]
    [InlineData("String", true)]           // PascalCase String is not a known safe type, encoded defensively
    [InlineData("INT", true)]              // Uppercase INT is not recognized as int, encoded defensively
    public void NeedsUrlEncoding_CaseSensitive_ReturnsExpected(
        string csharpType,
        bool expected)
    {
        // Act - Type comparison is case-sensitive
        var result = HttpClientExtractor.NeedsUrlEncoding(csharpType);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void UriEscapeDataString_PreservesUrlSafeCharacters()
    {
        // Arrange - These characters are URL-safe and should not be encoded
        const string urlSafe = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_.~";

        // Act
        var result = Uri.EscapeDataString(urlSafe);

        // Assert
        Assert.Equal(urlSafe, result);
    }

    [Fact]
    public void UriEscapeDataString_HandlesUnicodeCharacters()
    {
        // Arrange - Unicode characters should be percent-encoded
        const string unicode = "émoji";

        // Act
        var result = Uri.EscapeDataString(unicode);

        // Assert - Should be percent-encoded (UTF-8 bytes)
        Assert.Contains("%", result, StringComparison.Ordinal);
        Assert.NotEqual(unicode, result);
    }
}