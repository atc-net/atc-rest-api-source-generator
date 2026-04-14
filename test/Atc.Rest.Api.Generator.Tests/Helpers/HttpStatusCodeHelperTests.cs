namespace Atc.Rest.Api.Generator.Tests.Helpers;

public class HttpStatusCodeHelperTests
{
    // ========== Standard/Defined Status Codes ==========
    [Theory]
    [InlineData(200, "OK")]
    [InlineData(201, "Created")]
    [InlineData(202, "Accepted")]
    [InlineData(204, "NoContent")]
    [InlineData(301, "MovedPermanently")]
    [InlineData(302, "Found")]
    [InlineData(304, "NotModified")]
    [InlineData(400, "BadRequest")]
    [InlineData(401, "Unauthorized")]
    [InlineData(403, "Forbidden")]
    [InlineData(404, "NotFound")]
    [InlineData(405, "MethodNotAllowed")]
    [InlineData(409, "Conflict")]
    [InlineData(410, "Gone")]
    [InlineData(412, "PreconditionFailed")]
    [InlineData(413, "RequestEntityTooLarge")]
    [InlineData(415, "UnsupportedMediaType")]
    [InlineData(422, "UnprocessableEntity")]
    [InlineData(429, "TooManyRequests")]
    [InlineData(500, "InternalServerError")]
    [InlineData(501, "NotImplemented")]
    [InlineData(502, "BadGateway")]
    [InlineData(503, "ServiceUnavailable")]
    [InlineData(504, "GatewayTimeout")]
    public void ToEnumName_DefinedStatusCode_ReturnsEnumName(
        int statusCode,
        string expected)
    {
        // Act
        var result = HttpStatusCodeHelper.ToEnumName(statusCode);

        // Assert
        Assert.Equal(expected, result);
    }

    // ========== Non-Standard/Undefined Status Codes ==========
    [Theory]
    [InlineData(499, "Status499")]
    [InlineData(529, "Status529")]
    [InlineData(530, "Status530")]
    [InlineData(598, "Status598")]
    [InlineData(599, "Status599")]
    [InlineData(999, "Status999")]
    [InlineData(218, "Status218")]
    [InlineData(419, "Status419")]
    [InlineData(420, "Status420")]
    [InlineData(440, "Status440")]
    [InlineData(444, "Status444")]
    [InlineData(460, "Status460")]
    [InlineData(520, "Status520")]
    [InlineData(521, "Status521")]
    [InlineData(522, "Status522")]
    [InlineData(523, "Status523")]
    [InlineData(524, "Status524")]
    public void ToEnumName_UndefinedStatusCode_ReturnsStatusPrefix(
        int statusCode,
        string expected)
    {
        // Act
        var result = HttpStatusCodeHelper.ToEnumName(statusCode);

        // Assert
        Assert.Equal(expected, result);
    }

    // ========== Boundary Codes ==========
    [Theory]
    [InlineData(100, "Continue")]
    [InlineData(101, "SwitchingProtocols")]
    public void ToEnumName_InformationalCodes_ReturnsEnumName(
        int statusCode,
        string expected)
    {
        // Act
        var result = HttpStatusCodeHelper.ToEnumName(statusCode);

        // Assert
        Assert.Equal(expected, result);
    }

    // ========== Result Is Always a Valid C# Identifier ==========
    [Theory]
    [InlineData(200)]
    [InlineData(404)]
    [InlineData(499)]
    [InlineData(529)]
    [InlineData(999)]
    public void ToEnumName_AlwaysReturnsValidCSharpIdentifier(int statusCode)
    {
        // Act
        var result = HttpStatusCodeHelper.ToEnumName(statusCode);

        // Assert — must start with a letter and contain only letters/digits
        Assert.Matches(@"^[A-Za-z][A-Za-z0-9]*$", result);
    }
}