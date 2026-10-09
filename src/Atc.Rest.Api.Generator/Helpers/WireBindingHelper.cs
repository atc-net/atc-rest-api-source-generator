namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// The server-side step that runs before the framework binds an operation's parameters, for what its binder
/// cannot do: read an enum by the value in the specification (it reads member names only, so <c>dark-blue</c> is a
/// 400), read a cookie (there is no cookie binding source), take an array query parameter's items as sent (a
/// repeated key is one item each, never split on a comma inside; a declared delimiter - comma, pipe, space - is
/// honored), and answer a value it cannot bind with a problem that names the parameter.
/// <para>
/// The generated endpoint calls <c>.WithWireBinding&lt;TParameters&gt;()</c>. It rewrites an enum's wire value in
/// the route values, the query string or a header to the member name, copies each cookie parameter to the request
/// header its property is bound from (<see cref="CookieHeaderPrefix"/> + the cookie name), joins an array's items
/// with the item separator the generated list type splits on, and checks every scalar before the binder sees it.
/// </para>
/// </summary>
public static class WireBindingHelper
{
    /// <summary>
    /// The prefix of the request header a cookie parameter is bound from.
    /// </summary>
    public const string CookieHeaderPrefix = "x-cookie-";

    /// <summary>
    /// The query key an OAS 3.2 <c>in: querystring</c> parameter is bound from: one no request can send (it starts
    /// with the unit separator), which the step fills with the raw query string.
    /// </summary>
    public const string QueryStringKey = "\u001Fquerystring";

    private static readonly HashSet<string> ParsableTypes = new(StringComparer.Ordinal)
    {
        "int", "long", "short", "byte", "sbyte", "uint", "ulong", "ushort", "decimal", "double", "float", "char",
        "Guid", "DateTime", "DateTimeOffset", "DateOnly", "TimeOnly", "TimeSpan", "Half", "Int128", "UInt128",
    };

    private static readonly HashSet<string> TextTypes = new(StringComparer.Ordinal)
    {
        "string", "Uri", "object", "byte[]", "IFormFile",
    };

    /// <summary>
    /// Whether an operation needs the step: it has a parameter in the path, the query string, a header or a cookie.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="pathItem">The path item (its parameters apply to the operation too).</param>
    /// <returns><see langword="true"/> when the endpoint has to call <c>WithWireBinding</c>.</returns>
    public static bool IsNeeded(
        OpenApiOperation operation,
        IOpenApiPathItem? pathItem)
    {
        if (operation is null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        return (pathItem?.Parameters ?? [])
            .Concat(operation.Parameters ?? [])
            .Select(p => p.Resolve().Parameter)
            .Any(p => p is { In: ParameterLocation.Path or ParameterLocation.Query or ParameterLocation.QueryString or ParameterLocation.Header or ParameterLocation.Cookie });
    }

    /// <summary>
    /// Whether any operation of the document needs the step (or the form step).
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns><see langword="true"/> when the helper has to be generated.</returns>
    public static bool IsNeeded(OpenApiDocument openApiDoc)
    {
        if (openApiDoc?.Paths is null)
        {
            return false;
        }

        foreach (var pathItem in openApiDoc.Paths.Values)
        {
            if (pathItem?.Operations is null)
            {
                continue;
            }

            if (pathItem.Operations.Values.Any(operation => operation is not null && (IsNeeded(operation, pathItem) || operation.RequiresFormBindingFlattening())))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The fields of an operation's form body (bound field by field) as the arguments of <c>WithWireFormBinding</c>:
    /// the field described by its type (an item's for a list, see <see cref="ParameterExpression"/>), whether the
    /// specification requires it, and whether it is a file - checked for presence only.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <returns>C# tuple literals, <c>(WireParameter.Parsable&lt;int&gt;("form", "count"), false, false)</c>.</returns>
    public static IReadOnlyList<string> FormFieldArguments(
        OpenApiOperation operation)
    {
        if (operation is null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        var arguments = new List<string>();
        var (_, properties, required) = operation.GetFormBodySchemaInfo();
        foreach (var property in properties ?? new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal))
        {
            var (isFile, _) = property.Value.GetFileUploadInfo();
            var isArray = property.Value.IsOfType(JsonSchemaType.Array);
            var name = CSharpLiteralHelper.ToStringLiteral(property.Key);
            var field = isFile
                ? $"WireParameter.Text(\"form\", {name})"
                : Describe(
                    "form",
                    name,
                    (isArray ? property.Value.Items?.ToCSharpType(isRequired: true, registry: null) ?? "string" : property.Value.ToCSharpType(isRequired: true, registry: null)).TrimEnd('?'),
                    isArray);
            arguments.Add($"({field}, {(required.Contains(property.Key) ? "true" : "false")}, {(isFile ? "true" : "false")})");
        }

        return arguments;
    }

    /// <summary>
    /// The array query parameters whose items are delimited inside one value - form with <c>explode: false</c>
    /// (comma), <c>pipeDelimited</c> (pipe), <c>spaceDelimited</c> (space) - as the arguments of <c>WithWireBinding</c>.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="pathItem">The path item.</param>
    /// <returns>C# tuple literals, <c>("csv", ',')</c>.</returns>
    public static IReadOnlyList<string> DelimitedArrayArguments(
        OpenApiOperation operation,
        IOpenApiPathItem? pathItem)
    {
        if (operation is null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        var arguments = new List<string>();
        foreach (var parameter in (pathItem?.Parameters ?? []).Concat(operation.Parameters ?? []).Select(p => p.Resolve().Parameter))
        {
            if (parameter is not { Name: { } name } ||
                parameter.GetArrayDelimiter() is not { } delimiter)
            {
                continue;
            }

            arguments.Add($"({CSharpLiteralHelper.ToStringLiteral(name)}, '{delimiter}')");
        }

        return arguments;
    }

    /// <summary>
    /// A bound parameter as the step reads it, by its C# type: no parser for a string (or a type without one, such as
    /// <c>Uri</c>), <c>bool.TryParse</c> for a boolean, <c>T.TryParse</c> with the invariant culture for a parsable
    /// type, the wire values and member names for an enum - and an array query parameter (<c>ParsableList&lt;T&gt;</c>,
    /// <c>ParsableEnumList&lt;T&gt;</c>) by its item. Nothing is found through reflection, so the step works in a
    /// trimmed or Native AOT app.
    /// </summary>
    /// <param name="location">Where the parameter is: <c>path</c>, <c>query</c>, <c>header</c>.</param>
    /// <param name="nameLiteral">Its wire name, as a C# string literal.</param>
    /// <param name="typeName">The C# type of its property.</param>
    /// <returns>The C# expression, <c>WireParameter.Parsable&lt;int&gt;("query", "limit")</c>.</returns>
    public static string ParameterExpression(
        string location,
        string nameLiteral,
        string typeName)
    {
        if (typeName is null)
        {
            throw new ArgumentNullException(nameof(typeName));
        }

        var type = typeName.TrimEnd('?');
        foreach (var list in new[] { "ParsableList<", "ParsableEnumList<" })
        {
            if (type.StartsWith(list, StringComparison.Ordinal) && type.EndsWith(">", StringComparison.Ordinal))
            {
                return Describe(location, nameLiteral, type.Substring(list.Length, type.Length - list.Length - 1), isArray: true);
            }
        }

        return Describe(location, nameLiteral, type, isArray: false);
    }

    /// <summary>
    /// The description of an <c>[AsParameters]</c> record's bound parameters - <c>{Record}Wire.Parameters</c>, which
    /// the endpoint passes to <c>WithWireBinding</c> - or null when it binds none.
    /// </summary>
    /// <param name="record">The parameters record.</param>
    /// <param name="namespaceName">The record's namespace.</param>
    /// <param name="headerContent">The record file's header (its usings name the enum types).</param>
    /// <param name="queryObjects">The record's objects in the query, by wire name (see <see cref="OpenApiParameterExtensions.QueryObject"/>).</param>
    /// <param name="registry">The conflict registry the record's types were named with.</param>
    /// <returns>The class, or null.</returns>
    public static ClassParameters? ParameterDescriptionClass(
        RecordParameters record,
        string namespaceName,
        string? headerContent,
        IReadOnlyDictionary<string, OpenApiParameterExtensions.QueryObject>? queryObjects = null,
        TypeConflictRegistry? registry = null)
    {
        if (record is null)
        {
            throw new ArgumentNullException(nameof(record));
        }

        var entries = new List<string>();
        foreach (var parameter in record.Parameters ?? [])
        {
            var binding = parameter.Attributes?.FirstOrDefault(a => a.Name is "FromRoute" or "FromQuery" or "FromHeader");
            if (binding?.Content is not { } content ||
                !content.StartsWith("Name = ", StringComparison.Ordinal))
            {
                continue;
            }

            var location = binding.Name switch
            {
                "FromRoute" => "path",
                "FromQuery" => "query",
                _ => "header",
            };
            var nameLiteral = content.Substring("Name = ".Length);
            if (location == "query" &&
                queryObjects is not null &&
                queryObjects.TryGetValue(nameLiteral.Trim('"'), out var queryObject))
            {
                entries.Add(ObjectExpression(nameLiteral, queryObject, registry));
                continue;
            }

            entries.Add(ParameterExpression(location, nameLiteral, parameter.TypeName));
        }

        if (entries.Count == 0)
        {
            return null;
        }

        var initializer = "[" + Environment.NewLine +
                          string.Concat(entries.Select(e => $"        {e},{Environment.NewLine}")) +
                          "    ]";

        return new ClassParameters(
            HeaderContent: headerContent,
            Namespace: namespaceName,
            DocumentationTags: new CodeDocumentationTags($"The bound parameters of <see cref=\"{record.Name}\"/>, as the endpoint's wire-binding step reads them."),
            Attributes: [new AttributeParameters("GeneratedCode", $"\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\"")],
            DeclarationModifier: DeclarationModifiers.PublicStaticClass,
            ClassTypeName: record.Name + "Wire",
            GenericTypeName: null,
            InheritedClassTypeName: null,
            InheritedGenericClassTypeName: null,
            InheritedInterfaceTypeName: null,
            Constructors: null,
            Properties:
            [
                new PropertyParameters(
                    DocumentationTags: null,
                    Attributes: null,
                    DeclarationModifier: DeclarationModifiers.PublicStatic,
                    GenericTypeName: "IReadOnlyList",
                    TypeName: "WireParameter",
                    IsNullableType: false,
                    Name: "Parameters",
                    JsonName: null,
                    DefaultValue: initializer,
                    IsReferenceType: true,
                    IsGenericListType: true,
                    UseAutoProperty: true,
                    UseGet: true,
                    UseSet: false,
                    UseExpressionBody: false,
                    UseRequired: false,
                    Content: null),
            ],
            Methods: null,
            GenerateToStringMethod: false);
    }

    /// <summary>
    /// The source of the generated helper.
    /// </summary>
    /// <param name="projectName">The project name (root namespace).</param>
    /// <returns>The C# source.</returns>
    public static string GenerateContent(string projectName)
        => $$"""
            // <auto-generated />
            #nullable enable

            using System;
            using System.CodeDom.Compiler;
            using System.Collections.Generic;
            using System.Diagnostics.CodeAnalysis;
            using System.Globalization;
            using System.Linq;
            using System.Reflection;
            using System.Runtime.Serialization;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;
            using Microsoft.AspNetCore.Mvc;
            using Microsoft.Extensions.Primitives;

            namespace {{projectName}}.Generated;

            /// <summary>
            /// A parameter the wire-binding step reads: where it is (path, query, header, form), its wire name, whether a value
            /// parses (null: any text), the type a problem names, an enum's member names by wire value, and whether it is an
            /// array. Built from the parameter's type, without reflection.
            /// </summary>
            [GeneratedCode("{{GeneratorInfo.Name}}", "{{GeneratorInfo.Version}}")]
            public sealed record WireParameter(string In, string Name, Func<string, bool>? Parses, string TypeName, Dictionary<string, string>? Members, bool IsArray, IReadOnlyList<(WireParameter Property, bool Required)>? Properties = null, bool DeepObject = false)
            {
                /// <summary>
                /// An object in the query: each property by its own key - <c>{name}[{property}]</c> (deepObject) or the
                /// property's name (form, explode) - checked as the property's type, and handed to the binder as one value
                /// (<c>property=value</c> after the item separator, each) the model's <c>TryParse</c> reads.
                /// </summary>
                public static WireParameter Object(string @in, string name, bool deepObject, params (WireParameter Property, bool Required)[] properties)
                    => new(@in, name, null, "Object", null, false, properties, deepObject);

                /// <summary>
                /// A value any text is (a string, or a type without a parser).
                /// </summary>
                public static WireParameter Text(string @in, string name, bool isArray = false)
                    => new(@in, name, null, "String", null, isArray);

                /// <summary>
                /// A boolean.
                /// </summary>
                public static WireParameter Boolean(string @in, string name, bool isArray = false)
                    => new(@in, name, value => bool.TryParse(value, out _), "Boolean", null, isArray);

                /// <summary>
                /// A value its type parses with the invariant culture.
                /// </summary>
                public static WireParameter Parsable<T>(string @in, string name, bool isArray = false)
                    where T : IParsable<T>
                    => new(@in, name, value => T.TryParse(value, CultureInfo.InvariantCulture, out _), typeof(T).Name, null, isArray);

                /// <summary>
                /// An enum, by the wire value the specification writes or by its member name.
                /// </summary>
                public static WireParameter EnumValue<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T>(string @in, string name, bool isArray = false)
                    where T : struct, Enum
                {
                    var members = MemberNames<T>();
                    return new(@in, name, value => members.ContainsKey(value) || (Enum.TryParse<T>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)), typeof(T).Name, members, isArray);
                }

                // The wire values of an enum that differ from the member name, by wire value
                private static Dictionary<string, string> MemberNames<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T>()
                    where T : struct, Enum
                {
                    var members = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static))
                    {
                        if (field.GetCustomAttribute<EnumMemberAttribute>()?.Value is { } wireValue &&
                            !string.Equals(wireValue, field.Name, StringComparison.Ordinal))
                        {
                            members[wireValue] = field.Name;
                        }
                    }

                    return members;
                }
            }

            /// <summary>
            /// Runs before the framework binds an operation's parameters, for what its binder cannot do: read an enum
            /// by the value in the specification (it reads member names only), read a cookie (a cookie parameter is
            /// bound from the request header "{{CookieHeaderPrefix}}{name}", which this step fills from the cookie),
            /// take an array's items as sent (a repeated key is one item each; a declared delimiter is honored), and
            /// answer a value it cannot bind with a problem that names the parameter.
            /// </summary>
            [GeneratedCode("{{GeneratorInfo.Name}}", "{{GeneratorInfo.Version}}")]
            public static class WireBindingExtensions
            {
                private const string CookieHeaderPrefix = "{{CookieHeaderPrefix}}";

                private const string QueryStringKey = "\u001Fquerystring";

                private const char ItemSeparator = '\u001F';

                private sealed record BoundParameter(string In, string Name, Func<string, bool>? Parses, string TypeName, Dictionary<string, string>? Members, char? Delimiter, bool IsArray, IReadOnlyList<(WireParameter Property, bool Required)>? Properties = null, bool DeepObject = false);

                /// <summary>
                /// Adds the step for the parameters an endpoint binds (its <c>{Record}Wire.Parameters</c>); an array whose
                /// items are delimited inside one value is listed with its delimiter.
                /// </summary>
                public static TBuilder WithWireBinding<TBuilder>(this TBuilder builder, IReadOnlyList<WireParameter> wireParameters, params (string Name, char Delimiter)[] delimitedArrays)
                    where TBuilder : IEndpointConventionBuilder
                {
                    var parameters = new List<BoundParameter>();
                    var cookies = new List<string>();
                    foreach (var wire in wireParameters)
                    {
                        if (wire.In == "header" && wire.Name.StartsWith(CookieHeaderPrefix, StringComparison.Ordinal))
                        {
                            cookies.Add(wire.Name[CookieHeaderPrefix.Length..]);
                        }

                        var delimited = Array.Find(delimitedArrays, d => string.Equals(d.Name, wire.Name, StringComparison.OrdinalIgnoreCase));
                        parameters.Add(new BoundParameter(wire.In, wire.Name, wire.Parses, wire.TypeName, wire.Members, delimited.Name is null ? null : delimited.Delimiter, wire.IsArray, wire.Properties, wire.DeepObject));
                    }

                    if (parameters.Count == 0)
                    {
                        return builder;
                    }

                    builder.Add(endpoint =>
                    {
                        if (endpoint.RequestDelegate is not { } next)
                        {
                            return;
                        }

                        endpoint.RequestDelegate = context =>
                        {
                            var errors = Rewrite(context.Request, parameters, cookies);
                            return errors is null
                                ? next(context)
                                : global::Microsoft.AspNetCore.Http.Results.ValidationProblem(errors).ExecuteAsync(context);
                        };
                    });

                    return builder;
                }

                /// <summary>
                /// Adds the step for a form bound field by field (multipart/form-data, application/x-www-form-urlencoded):
                /// a required field that is missing, or a value that does not parse, is answered with a problem naming
                /// the field, and an enum's wire value is rewritten to the member name the binder reads.
                /// </summary>
                public static TBuilder WithWireFormBinding<TBuilder>(this TBuilder builder, params (WireParameter Field, bool Required, bool IsFile)[] fields)
                    where TBuilder : IEndpointConventionBuilder
                {
                    var checks = new List<FormField>();
                    foreach (var (field, required, isFile) in fields)
                    {
                        checks.Add(new FormField(field.Name, required, isFile, isFile ? null : field.Parses, field.TypeName, field.Members, field.IsArray));
                    }

                    builder.Add(endpoint =>
                    {
                        if (endpoint.RequestDelegate is not { } next)
                        {
                            return;
                        }

                        endpoint.RequestDelegate = async context =>
                        {
                            // A request whose antiforgery token the middleware rejected (an endpoint that validates it -
                            // useAntiforgery) is a 400, as the framework's own binding answers it; even asking whether
                            // the body is a form would throw
                            if (context.Features.Get<global::Microsoft.AspNetCore.Antiforgery.IAntiforgeryValidationFeature>() is { IsValid: false } antiforgery)
                            {
                                await global::Microsoft.AspNetCore.Http.Results.Problem(
                                    detail: antiforgery.Error?.Message,
                                    statusCode: global::Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest,
                                    title: "Invalid antiforgery token").ExecuteAsync(context);
                                return;
                            }

                            // Another media type is the framework's 415 (the Accepts metadata)
                            if (!context.Request.HasFormContentType)
                            {
                                await next(context);
                                return;
                            }

                            // A form the framework cannot read (no multipart boundary, more fields or a longer value
                            // than its limits, a body cut short) is the caller's mistake: a 400 naming it, never a 500
                            global::Microsoft.AspNetCore.Http.IFormCollection form;
                            try
                            {
                                form = await context.Request.ReadFormAsync(context.RequestAborted);
                            }
                            catch (global::System.Exception ex) when (ex is global::System.IO.InvalidDataException or global::System.IO.IOException or global::Microsoft.AspNetCore.Http.BadHttpRequestException)
                            {
                                await global::Microsoft.AspNetCore.Http.Results.Problem(
                                    detail: ex.Message,
                                    statusCode: global::Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest,
                                    title: "The form could not be read").ExecuteAsync(context);
                                return;
                            }

                            var errors = CheckForm(form, checks, out var rewritten);
                            if (errors is not null)
                            {
                                await global::Microsoft.AspNetCore.Http.Results.ValidationProblem(errors).ExecuteAsync(context);
                                return;
                            }

                            if (rewritten is not null)
                            {
                                context.Request.Form = rewritten;
                            }

                            await next(context);
                        };
                    });

                    return builder;
                }

                private sealed record FormField(string Name, bool Required, bool IsFile, Func<string, bool>? Parses, string TypeName, Dictionary<string, string>? Members, bool IsArray);

                // The form as the binder reads it (an enum by its member name); the values it cannot bind and the
                // required fields that are missing, by field name, or null
                private static Dictionary<string, string[]>? CheckForm(
                    IFormCollection form,
                    List<FormField> fields,
                    out IFormCollection? rewritten)
                {
                    rewritten = null;
                    Dictionary<string, string[]>? errors = null;
                    Dictionary<string, StringValues>? values = null;
                    foreach (var field in fields)
                    {
                        if (field.IsFile)
                        {
                            if (field.Required && form.Files.GetFiles(field.Name).Count == 0)
                            {
                                errors ??= new Dictionary<string, string[]>(StringComparer.Ordinal);
                                errors[field.Name] = [$"The {field.Name} field is required."];
                            }

                            continue;
                        }

                        if (!form.TryGetValue(field.Name, out var raw))
                        {
                            if (field.Required)
                            {
                                errors ??= new Dictionary<string, string[]>(StringComparer.Ordinal);
                                errors[field.Name] = [$"The {field.Name} field is required."];
                            }

                            continue;
                        }

                        var items = raw.Where(v => !string.IsNullOrEmpty(v)).Select(v => v!).ToArray();
                        if (field.Parses is { } parses && Array.Find(items, item => !parses(item)) is { } invalid)
                        {
                            errors ??= new Dictionary<string, string[]>(StringComparer.Ordinal);
                            errors[field.Name] = [$"'{invalid}' is not a valid {field.TypeName}."];
                            continue;
                        }

                        // A list: its items as one value with the item separator before each (the parsable list type
                        // splits on it, nothing else; form binding would read a List<T> from indexed keys). An enum by
                        // its member name.
                        if (field.IsArray)
                        {
                            values ??= form.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                            values[field.Name] = string.Concat(items.Select(item => ItemSeparator + item));
                        }
                        else if (field.Members is { Count: > 0 } members)
                        {
                            values ??= form.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                            values[field.Name] = new StringValues(items.Select(item => members.TryGetValue(item, out var member) ? member : item).ToArray());
                        }
                    }

                    if (values is not null)
                    {
                        rewritten = new FormCollection(values, form.Files);
                    }

                    return errors;
                }

                // An object's properties from their keys: "{property}={value}" after the item separator, each (an enum by
                // its member name), or null when none was sent; the values that do not parse, and the required
                // properties missing from an object that was sent, by key
                private static Dictionary<string, string[]>? CollectObject(
                    IQueryCollection query,
                    BoundParameter parameter,
                    IReadOnlyList<(WireParameter Property, bool Required)> properties,
                    out string? collected)
                {
                    collected = null;
                    Dictionary<string, string[]>? errors = null;
                    var missing = new List<string>();
                    var pairs = new System.Text.StringBuilder();
                    var sent = false;
                    foreach (var (property, required) in properties)
                    {
                        var key = parameter.DeepObject ? $"{parameter.Name}[{property.Name}]" : property.Name;
                        if (!query.TryGetValue(key, out var values) || values.Count == 0 || string.IsNullOrEmpty(values[0]))
                        {
                            if (required)
                            {
                                missing.Add(key);
                            }

                            continue;
                        }

                        sent = true;
                        var value = values[0]!;
                        if (value.Contains(ItemSeparator, StringComparison.Ordinal) ||
                            (property.Parses is { } parses && !parses(value)))
                        {
                            errors ??= new Dictionary<string, string[]>(StringComparer.Ordinal);
                            errors[key] = [$"'{value}' is not a valid {property.TypeName}."];
                            continue;
                        }

                        if (property.Members is { Count: > 0 } members && members.TryGetValue(value, out var member))
                        {
                            value = member;
                        }

                        pairs.Append(ItemSeparator).Append(property.Name).Append('=').Append(value);
                    }

                    if (!sent)
                    {
                        return null;
                    }

                    foreach (var key in missing)
                    {
                        errors ??= new Dictionary<string, string[]>(StringComparer.Ordinal);
                        errors[key] = [$"The {key} field is required."];
                    }

                    collected = pairs.ToString();
                    return errors;
                }

                // The request as the binder reads it; the values it cannot bind, by parameter name, or null
                private static Dictionary<string, string[]>? Rewrite(
                    HttpRequest request,
                    List<BoundParameter> parameters,
                    List<string> cookies)
                {
                    // A cookie parameter is bound from its header: the cookie decides, never a header a caller sent
                    foreach (var cookie in cookies)
                    {
                        if (request.Cookies.TryGetValue(cookie, out var value))
                        {
                            request.Headers[CookieHeaderPrefix + cookie] = value;
                        }
                        else
                        {
                            request.Headers.Remove(CookieHeaderPrefix + cookie);
                        }
                    }

                    Dictionary<string, StringValues>? query = null;
                    Dictionary<string, string[]>? errors = null;
                    foreach (var parameter in parameters)
                    {
                        // An object in the query: its properties' keys, as one value the model reads (none: no object)
                        if (parameter.Properties is { } properties)
                        {
                            query ??= request.Query.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                            var objectErrors = CollectObject(request.Query, parameter, properties, out var collected);
                            if (objectErrors is not null)
                            {
                                errors ??= new Dictionary<string, string[]>(StringComparer.Ordinal);
                                foreach (var error in objectErrors)
                                {
                                    errors[error.Key] = error.Value;
                                }
                            }

                            if (collected is null)
                            {
                                query.Remove(parameter.Name);
                            }
                            else
                            {
                                query[parameter.Name] = collected;
                            }

                            continue;
                        }

                        // OAS 3.2 in: querystring - the whole query string, as sent
                        if (parameter.In == "query" && parameter.Name == QueryStringKey)
                        {
                            if (request.QueryString.Value is { Length: > 1 } rawQuery)
                            {
                                query ??= request.Query.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                                query[QueryStringKey] = rawQuery[1..];
                            }

                            continue;
                        }

                        var values = parameter.In switch
                        {
                            "path" => request.RouteValues.TryGetValue(parameter.Name, out var routeValue) && routeValue is string routeText ? new StringValues(routeText) : StringValues.Empty,
                            "query" => request.Query.TryGetValue(parameter.Name, out var queryValue) ? queryValue : StringValues.Empty,
                            _ => request.Headers.TryGetValue(parameter.Name, out var headerValue) ? headerValue : StringValues.Empty,
                        };
                        if (values.Count == 0)
                        {
                            continue;
                        }

                        // An array: its items as one value with the item separator before each (the list type splits on
                        // it, nothing else). A declared delimiter splits each value; a path or header array is its items
                        // joined by commas (style: simple); in the query without one (explode: true) every key is one
                        // item, commas and all. An item keeps its spaces; an empty one is no item.
                        var arrayDelimiter = parameter.Delimiter ?? (parameter.In is "path" or "header" ? ',' : null);
                        var items = parameter.IsArray
                            ? arrayDelimiter is { } delimiter
                                ? values.SelectMany(v => (v ?? string.Empty).Split(delimiter, StringSplitOptions.RemoveEmptyEntries)).ToArray()
                                : values.Where(v => !string.IsNullOrEmpty(v)).Select(v => v!).ToArray()
                            : values.Where(v => v is not null).Select(v => v!).ToArray();

                        if (parameter.Parses is { } parses && Array.Find(items, item => item.Length > 0 && !parses(item)) is { } invalid)
                        {
                            errors ??= new Dictionary<string, string[]>(StringComparer.Ordinal);
                            errors[parameter.Name] = [$"'{invalid}' is not a valid {parameter.TypeName}."];
                            continue;
                        }

                        // An enum by its wire value: the member name the binder reads
                        if (parameter.Members is { Count: > 0 } members)
                        {
                            items = items.Select(item => members.TryGetValue(item, out var member) ? member : item).ToArray();
                        }

                        if (parameter.IsArray)
                        {
                            var joined = string.Concat(items.Select(item => ItemSeparator + item));
                            switch (parameter.In)
                            {
                                case "path":
                                    request.RouteValues[parameter.Name] = joined;
                                    break;
                                case "query":
                                    query ??= request.Query.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                                    query[parameter.Name] = joined;
                                    break;
                                default:
                                    request.Headers[parameter.Name] = joined;
                                    break;
                            }
                        }
                        else if (parameter.Members is { Count: > 0 })
                        {
                            switch (parameter.In)
                            {
                                case "path":
                                    request.RouteValues[parameter.Name] = items[0];
                                    break;
                                case "query":
                                    query ??= request.Query.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
                                    query[parameter.Name] = new StringValues(items);
                                    break;
                                default:
                                    request.Headers[parameter.Name] = new StringValues(items);
                                    break;
                            }
                        }
                    }

                    if (query is not null)
                    {
                        request.Query = new QueryCollection(query);
                    }

                    return errors;
                }
            }
            """;

    // WireParameter.Object("query", "filter", deepObject: true, (WireParameter.Text("query", "name"), false), ...)
    private static string ObjectExpression(
        string nameLiteral,
        OpenApiParameterExtensions.QueryObject queryObject,
        TypeConflictRegistry? registry)
    {
        var properties = queryObject.Properties.Select(property =>
            $"({ParameterExpression("query", CSharpLiteralHelper.ToStringLiteral(property.WireName), property.Schema.ToCSharpTypeForModel(property.Required, registry))}, {(property.Required ? "true" : "false")})");
        return $"WireParameter.Object(\"query\", {nameLiteral}, deepObject: {(queryObject.DeepObject ? "true" : "false")}, {string.Join(", ", properties)})";
    }

    private static string Describe(
        string location,
        string nameLiteral,
        string itemType,
        bool isArray)
    {
        var simple = itemType.Replace("global::", string.Empty);
        simple = simple.StartsWith("System.", StringComparison.Ordinal) ? simple.Substring("System.".Length) : simple;
        var factory = simple switch
        {
            "bool" or "Boolean" => "WireParameter.Boolean",
            _ when ParsableTypes.Contains(simple) => $"WireParameter.Parsable<{itemType}>",
            _ when TextTypes.Contains(simple) || simple.EndsWith("IFormFile", StringComparison.Ordinal) => "WireParameter.Text",
            _ => $"WireParameter.EnumValue<{itemType}>",
        };

        return $"{factory}(\"{location}\", {nameLiteral}{(isArray ? ", isArray: true" : string.Empty)})";
    }
}