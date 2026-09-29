namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Provides safe conversion of HTTP status code integers to valid C# identifier names.
/// Handles non-standard status codes (e.g., 499, 529) that are not defined in
/// <see cref="System.Net.HttpStatusCode"/> and would otherwise produce invalid identifiers.
/// </summary>
public static class HttpStatusCodeHelper
{
    /// <summary>
    /// Maps HTTP status code strings to their ASP.NET Core <c>StatusCodes</c> constant names
    /// (e.g. "200" → "Status200OK"). Single source of truth shared by the endpoint extractors.
    /// </summary>
    private static readonly Dictionary<string, string> StatusCodesConstants = new(StringComparer.Ordinal)
    {
        // 1xx Informational
        ["100"] = "Status100Continue",
        ["101"] = "Status101SwitchingProtocols",
        ["102"] = "Status102Processing",

        // 2xx Success
        ["200"] = "Status200OK",
        ["201"] = "Status201Created",
        ["202"] = "Status202Accepted",
        ["203"] = "Status203NonAuthoritative",
        ["204"] = "Status204NoContent",
        ["205"] = "Status205ResetContent",
        ["206"] = "Status206PartialContent",
        ["207"] = "Status207MultiStatus",
        ["208"] = "Status208AlreadyReported",
        ["226"] = "Status226IMUsed",

        // 3xx Redirection
        ["300"] = "Status300MultipleChoices",
        ["301"] = "Status301MovedPermanently",
        ["302"] = "Status302Found",
        ["303"] = "Status303SeeOther",
        ["304"] = "Status304NotModified",
        ["305"] = "Status305UseProxy",
        ["306"] = "Status306SwitchProxy",
        ["307"] = "Status307TemporaryRedirect",
        ["308"] = "Status308PermanentRedirect",

        // 4xx Client Error
        ["400"] = "Status400BadRequest",
        ["401"] = "Status401Unauthorized",
        ["402"] = "Status402PaymentRequired",
        ["403"] = "Status403Forbidden",
        ["404"] = "Status404NotFound",
        ["405"] = "Status405MethodNotAllowed",
        ["406"] = "Status406NotAcceptable",
        ["407"] = "Status407ProxyAuthenticationRequired",
        ["408"] = "Status408RequestTimeout",
        ["409"] = "Status409Conflict",
        ["410"] = "Status410Gone",
        ["411"] = "Status411LengthRequired",
        ["412"] = "Status412PreconditionFailed",
        ["413"] = "Status413PayloadTooLarge",
        ["414"] = "Status414UriTooLong",
        ["415"] = "Status415UnsupportedMediaType",
        ["416"] = "Status416RangeNotSatisfiable",
        ["417"] = "Status417ExpectationFailed",
        ["418"] = "Status418ImATeapot",
        ["421"] = "Status421MisdirectedRequest",
        ["422"] = "Status422UnprocessableEntity",
        ["423"] = "Status423Locked",
        ["424"] = "Status424FailedDependency",
        ["426"] = "Status426UpgradeRequired",
        ["428"] = "Status428PreconditionRequired",
        ["429"] = "Status429TooManyRequests",
        ["431"] = "Status431RequestHeaderFieldsTooLarge",
        ["451"] = "Status451UnavailableForLegalReasons",

        // 5xx Server Error
        ["500"] = "Status500InternalServerError",
        ["501"] = "Status501NotImplemented",
        ["502"] = "Status502BadGateway",
        ["503"] = "Status503ServiceUnavailable",
        ["504"] = "Status504GatewayTimeout",
        ["505"] = "Status505HttpVersionNotsupported",
        ["506"] = "Status506VariantAlsoNegotiates",
        ["507"] = "Status507InsufficientStorage",
        ["508"] = "Status508LoopDetected",
        ["510"] = "Status510NotExtended",
        ["511"] = "Status511NetworkAuthenticationRequired",
    };

    /// <summary>
    /// Whether the status is a redirect that carries a <c>Location</c> (RFC 9110 §15.4): 301, 302, 303, 307, 308.
    /// </summary>
    public static bool IsRedirect(int statusCode)
        => statusCode is 301 or 302 or 303 or 307 or 308;

    /// <summary>
    /// Converts an HTTP status code integer to a valid C# identifier name.
    /// For defined codes (e.g., 200 → "OK", 404 → "NotFound"), returns the enum name.
    /// For undefined codes (e.g., 499, 529), returns "Status{code}" (e.g., "Status499").
    /// </summary>
    public static string ToEnumName(int statusCode)
    {
        // Codes with an alias in HttpStatusCode (Ambiguous, Moved, Redirect, RedirectMethod, RedirectKeepVerb,
        // UnprocessableContent): always one fixed name, whichever alias Enum.ToString would pick.
        switch (statusCode)
        {
            case 300:
                return "MultipleChoices";
            case 301:
                return "MovedPermanently";
            case 302:
                return "Found";
            case 303:
                return "SeeOther";
            case 307:
                return "TemporaryRedirect";
            case 422:
                return "UnprocessableEntity";
        }

        var httpStatusCode = (System.Net.HttpStatusCode)statusCode;

        if (Enum.IsDefined(typeof(System.Net.HttpStatusCode), statusCode))
        {
            return httpStatusCode.ToString();
        }

        return $"Status{statusCode}";
    }

    /// <summary>
    /// Tries to map an HTTP status code string (e.g. "404") to its ASP.NET Core
    /// <c>StatusCodes</c> constant name (e.g. "Status404NotFound").
    /// </summary>
    /// <param name="statusCode">The status code string from the OpenAPI spec.</param>
    /// <param name="constant">The matching <c>StatusCodes</c> constant name, if found.</param>
    /// <returns><see langword="true"/> if the code is known; otherwise <see langword="false"/>.</returns>
    public static bool TryGetStatusCodesConstant(
        string statusCode,
        out string? constant)
        => StatusCodesConstants.TryGetValue(statusCode, out constant);

    /// <summary>
    /// Gets the C# expression for a status code as a <c>System.Net.HttpStatusCode</c> value in generated
    /// client code: the enum member when one exists (e.g. <c>HttpStatusCode.NotFound</c>), otherwise a cast
    /// of the numeric value (e.g. <c>(HttpStatusCode)418</c>, <c>(HttpStatusCode)499</c>).
    /// </summary>
    /// <param name="statusCode">The numeric status code.</param>
    /// <returns>The C# expression.</returns>
    public static string ToHttpStatusCodeExpression(int statusCode)
        => Enum.IsDefined(typeof(System.Net.HttpStatusCode), statusCode)
            ? $"HttpStatusCode.{(System.Net.HttpStatusCode)statusCode}"
            : $"(HttpStatusCode){statusCode.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Gets the C# expression for a status code in generated ASP.NET Core code: the <c>StatusCodes</c>
    /// constant when one exists (e.g. <c>StatusCodes.Status404NotFound</c>), otherwise the numeric literal
    /// (e.g. <c>425</c>, <c>499</c>) so that non-standard and unmapped codes are still emitted.
    /// </summary>
    /// <param name="statusCode">The numeric status code.</param>
    /// <returns>The C# expression.</returns>
    public static string ToStatusCodesExpression(int statusCode)
        => StatusCodesConstants.TryGetValue(statusCode.ToString(CultureInfo.InvariantCulture), out var constant)
            ? $"StatusCodes.{constant}"
            : statusCode.ToString(CultureInfo.InvariantCulture);
}