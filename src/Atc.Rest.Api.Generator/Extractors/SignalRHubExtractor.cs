namespace Atc.Rest.Api.Generator.Extractors;

/// <summary>
/// Generates the C# contract of the SignalR hubs a specification declares with <c>x-signalr-hubs</c>, so the hub's
/// event names, method names and payload types are written once - in the specification - and not by hand on the
/// server and again in every client.
/// </summary>
/// <remarks>
/// Server, per hub: <c>I{Hub}HubClient</c> (the events, for <c>Hub&lt;T&gt;</c> and <c>IHubContext&lt;THub, T&gt;</c>),
/// <c>I{Hub}Hub</c> (the methods a client invokes, for the hub class to implement), and <c>{Hub}HubContract</c>
/// (the hub's path, the names on the wire, and <c>Send{Event}Async</c> for an untyped <c>IClientProxy</c>).
/// Client, per hub: <c>{Hub}HubClient</c> over a <c>HubConnection</c> with <c>On{Event}</c> per event and
/// <c>{Method}Async</c> per method.
/// </remarks>
public static class SignalRHubExtractor
{
    /// <summary>
    /// The server side of every hub, one file per hub.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The root namespace of the generated code.</param>
    /// <returns>Per hub: the hint name of its file and the file's content.</returns>
    public static List<(string HintName, string Content)> ExtractServer(
        OpenApiDocument openApiDoc,
        string projectName)
    {
        var hubs = SignalRHubHelper.Parse(openApiDoc);
        var models = SignalRHubHelper.ModelTypeNames(openApiDoc, projectName, hubs);
        return hubs
            .Select(hub => ($"{projectName}.Generated.Hubs.{TypeName(hub)}Hub.g.cs", ServerFile(projectName, hub, models)))
            .ToList();
    }

    /// <summary>
    /// The client side of every hub, one file per hub.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <param name="projectName">The root namespace of the generated code.</param>
    /// <param name="singleModelsNamespace">Whether every model is in one namespace (a single typed client).</param>
    /// <returns>Per hub: the hint name of its file and the file's content.</returns>
    public static List<(string HintName, string Content)> ExtractClient(
        OpenApiDocument openApiDoc,
        string projectName,
        bool singleModelsNamespace = false)
    {
        var hubs = SignalRHubHelper.Parse(openApiDoc);
        var models = SignalRHubHelper.ModelTypeNames(openApiDoc, projectName, hubs, singleModelsNamespace);
        return hubs
            .Select(hub => ($"{projectName}.Generated.Hubs.{TypeName(hub)}HubClient.g.cs", ClientFile(projectName, hub, models)))
            .ToList();
    }

    /// <summary>
    /// The names of the typed hub clients the document's hubs give, for a message that names them.
    /// </summary>
    /// <param name="openApiDoc">The OpenAPI document.</param>
    /// <returns>The class names.</returns>
    public static List<string> ClientTypeNames(OpenApiDocument openApiDoc)
        => SignalRHubHelper.Parse(openApiDoc)
            .Select(hub => TypeName(hub) + "HubClient")
            .ToList();

    /// <summary>
    /// The namespace the hub contracts are generated in.
    /// </summary>
    /// <param name="projectName">The root namespace of the generated code.</param>
    /// <returns>The namespace.</returns>
    public static string NamespaceOf(string projectName)
        => $"{NamespaceBuilder.BuildBase(projectName)}.Hubs";

    /// <summary>
    /// The C# type of a payload: the model of a component schema, a primitive by its type and format, or a read-only
    /// list of either.
    /// </summary>
    /// <param name="payload">The payload.</param>
    /// <param name="models">The C# type of each payload schema (<see cref="SignalRHubHelper.ModelTypeNames"/>).</param>
    /// <returns>The C# type.</returns>
    public static string CSharpType(
        SignalRHubPayload payload,
        IReadOnlyDictionary<string, string> models)
        => payload switch
        {
            { Items: { } items } => $"IReadOnlyList<{CSharpType(items, models)}>",
            { SchemaName: { } schema } => models.TryGetValue(schema, out var model) ? model : OpenApiSchemaExtensions.SanitizeSchemaName(schema),
            { Type: "integer", Format: "int64" } => "long",
            { Type: "integer" } => "int",
            { Type: "number", Format: "float" } => "float",
            { Type: "number" } => "double",
            { Type: "boolean" } => "bool",
            { Type: "string", Format: "uuid" } => "Guid",
            { Type: "string", Format: "date-time" } => "DateTimeOffset",
            { Type: "string", Format: "date" } => "DateOnly",
            { Type: "string", Format: "uri" } => "Uri",
            _ => "string",
        };

    private static string ServerFile(
        string projectName,
        SignalRHub hub,
        Dictionary<string, string> models)
    {
        var name = TypeName(hub);
        var sb = Header(projectName, "Microsoft.AspNetCore.SignalR");
        var members = Named(hub);

        // The events, as the interface of a strongly typed hub: SignalR sends a method of it under the method's
        // name, so only an event whose name is a C# identifier can be one
        var typedEvents = members.Events.Where(e => e.NameIsIdentifier).ToList();
        var hasUntypedMethod = members.Methods.Any(m => !m.NameIsIdentifier);
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// What the {hub.Key} hub sends its clients: the client interface of a strongly typed hub");
        sb.AppendLine($"/// (<c>Hub&lt;I{name}HubClient&gt;</c>, <c>IHubContext&lt;THub, I{name}HubClient&gt;</c>). A method is sent under its name.");
        if (typedEvents.Count < members.Events.Count)
        {
            sb.AppendLine("/// An event whose name is no C# identifier cannot be a method of it: send it with");
            sb.AppendLine($"/// <see cref=\"{name}HubContract\"/>'s <c>Send…Async</c> on an <c>IClientProxy</c>.");
        }

        sb.AppendLine("/// </summary>");
        sb.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        sb.AppendLine($"public interface I{name}HubClient");
        sb.AppendLine("{");
        AppendMembers(
            sb,
            typedEvents,
            e => $"Task {e.Member.Name}({ParameterList(Arguments(e.Member, models))});",
            e => $"The <c>{e.Member.Name}</c> event.");
        sb.AppendLine("}");
        sb.AppendLine();

        // The methods a client invokes: what the hub class implements
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// What a client invokes on the {hub.Key} hub: implement it on the hub class, so the hub cannot drift from");
        sb.AppendLine("/// the specification.");
        if (hasUntypedMethod)
        {
            sb.AppendLine("/// A method whose name is no C# identifier is named in PascalCase here and needs");
            sb.AppendLine("/// <c>[HubMethodName]</c> with its name on the wire on the hub's method.");
        }

        sb.AppendLine("/// </summary>");
        sb.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        sb.AppendLine($"public interface I{name}Hub");
        sb.AppendLine("{");
        AppendMembers(
            sb,
            members.Methods,
            m => $"{ReturnType(m.Member.Returns, models)} {(m.NameIsIdentifier ? m.Member.Name : m.Identifier)}({ParameterList(Arguments(m.Member, models))});",
            m => m.NameIsIdentifier ? $"The <c>{m.Member.Name}</c> method." : $"The <c>{m.Member.Name}</c> method (<c>[HubMethodName({CSharpLiteralHelper.ToStringLiteral(m.Member.Name)})]</c>).");
        sb.AppendLine("}");
        sb.AppendLine();

        // The path, the names, and the events for an untyped proxy
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// The {hub.Key} hub as the specification declares it: where it is mapped, what its events and methods are");
        sb.AppendLine("/// called on the wire, and its events on an untyped <c>IClientProxy</c> (<c>Clients.All</c>, <c>IHubContext&lt;THub&gt;</c>).");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        sb.AppendLine($"public static class {name}HubContract");
        sb.AppendLine("{");
        AppendNames(sb, hub, members);
        foreach (var e in members.Events)
        {
            sb.AppendLine();
            sb.AppendLine(4, "/// <summary>");
            sb.AppendLine(4, $"/// Sends the <c>{e.Member.Name}</c> event.");
            sb.AppendLine(4, "/// </summary>");
            var arguments = Arguments(e.Member, models, "clients", "cancellationToken");
            sb.AppendLine(4, $"public static Task Send{e.Identifier}Async(");
            sb.AppendLine(8, "this IClientProxy clients,");
            foreach (var (type, argument) in arguments)
            {
                sb.AppendLine(8, $"{type} {argument},");
            }

            sb.AppendLine(8, "CancellationToken cancellationToken = default)");
            sb.AppendLine(8, $"=> clients.SendAsync(Events.{e.Identifier}, {string.Concat(arguments.Select(a => a.Name + ", "))}cancellationToken);");
        }

        sb.Append('}');
        return sb.ToString();
    }

    private static string ClientFile(
        string projectName,
        SignalRHub hub,
        Dictionary<string, string> models)
    {
        var name = TypeName(hub);
        var sb = Header(projectName, "Microsoft.AspNetCore.SignalR.Client");
        var members = Named(hub);

        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// The {hub.Key} hub from a client: its events and methods by name and type, over a <c>HubConnection</c> the");
        sb.AppendLine("/// caller builds (the address, the access token, the reconnect policy are the caller's).");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <example>");
        sb.AppendLine("/// <code>");
        sb.AppendLine($"/// var hub = new {name}HubClient(new HubConnectionBuilder().WithUrl(new Uri(baseAddress, {name}HubClient.Url)).WithAutomaticReconnect().Build());");
        if (members.Events.Count > 0)
        {
            var first = members.Events[0];
            var names = string.Join(", ", Arguments(first.Member, models).Select(a => a.Name));
            sb.AppendLine(first.Member.Arguments.Count == 1
                ? $"/// using var subscription = hub.On{first.Identifier}({names} => Show({names}));"
                : $"/// using var subscription = hub.On{first.Identifier}(({names}) => Show({names}));");
        }

        sb.AppendLine("/// await hub.StartAsync(cancellationToken);");
        sb.AppendLine("/// </code>");
        sb.AppendLine("/// </example>");
        sb.AppendLine($"[GeneratedCode(\"{GeneratorInfo.Name}\", \"{GeneratorInfo.Version}\")]");
        sb.AppendLine($"public sealed class {name}HubClient : IAsyncDisposable");
        sb.AppendLine("{");
        AppendNames(sb, hub, members);
        sb.AppendLine();
        sb.AppendLine(4, $"public {name}HubClient(HubConnection connection)");
        sb.AppendLine(8, "=> Connection = connection ?? throw new ArgumentNullException(nameof(connection));");
        sb.AppendLine();
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// The connection: its state, its <c>Closed</c> / <c>Reconnecting</c> / <c>Reconnected</c> events.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, "public HubConnection Connection { get; }");
        sb.AppendLine();
        sb.AppendLine(4, "public Task StartAsync(CancellationToken cancellationToken = default)");
        sb.AppendLine(8, "=> Connection.StartAsync(cancellationToken);");
        sb.AppendLine();
        sb.AppendLine(4, "public Task StopAsync(CancellationToken cancellationToken = default)");
        sb.AppendLine(8, "=> Connection.StopAsync(cancellationToken);");
        sb.AppendLine();
        sb.AppendLine(4, "public ValueTask DisposeAsync()");
        sb.AppendLine(8, "=> Connection.DisposeAsync();");

        foreach (var e in members.Events)
        {
            var types = string.Join(", ", Arguments(e.Member, models).Select(a => a.Type));
            sb.AppendLine();
            sb.AppendLine(4, "/// <summary>");
            sb.AppendLine(4, $"/// Subscribes to the <c>{e.Member.Name}</c> event; dispose what is returned to unsubscribe.");
            sb.AppendLine(4, "/// </summary>");
            sb.AppendLine(4, types.Length == 0
                ? $"public IDisposable On{e.Identifier}(Action handler)"
                : $"public IDisposable On{e.Identifier}(Action<{types}> handler)");
            sb.AppendLine(8, $"=> Connection.On(Events.{e.Identifier}, handler);");
            sb.AppendLine();
            sb.AppendLine(4, "/// <summary>");
            sb.AppendLine(4, $"/// Subscribes to the <c>{e.Member.Name}</c> event with an asynchronous handler; dispose what is returned to unsubscribe.");
            sb.AppendLine(4, "/// </summary>");
            sb.AppendLine(4, types.Length == 0
                ? $"public IDisposable On{e.Identifier}(Func<Task> handler)"
                : $"public IDisposable On{e.Identifier}(Func<{types}, Task> handler)");
            sb.AppendLine(8, $"=> Connection.On(Events.{e.Identifier}, handler);");
        }

        foreach (var m in members.Methods)
        {
            var returns = m.Member.Returns is null ? null : CSharpType(m.Member.Returns, models);
            sb.AppendLine();
            sb.AppendLine(4, "/// <summary>");
            sb.AppendLine(4, $"/// Invokes the hub's <c>{m.Member.Name}</c> method and waits for it.");
            sb.AppendLine(4, "/// </summary>");
            sb.AppendLine(4, $"public {ReturnType(m.Member.Returns, models)} {m.Identifier}Async(");
            var parameters = Arguments(m.Member, models, "cancellationToken");
            foreach (var (type, argument) in parameters)
            {
                sb.AppendLine(8, $"{type} {argument},");
            }

            sb.AppendLine(8, "CancellationToken cancellationToken = default)");
            var arguments = string.Concat(parameters.Select(a => a.Name + ", "));
            sb.AppendLine(8, returns is null
                ? $"=> Connection.InvokeAsync(Methods.{m.Identifier}, {arguments}cancellationToken);"
                : $"=> Connection.InvokeAsync<{returns}>(Methods.{m.Identifier}, {arguments}cancellationToken);");
        }

        sb.Append('}');
        return sb.ToString();
    }

    private static StringBuilder Header(
        string projectName,
        string signalRNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.CodeDom.Compiler;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine($"using {signalRNamespace};");

        sb.AppendLine();
        sb.AppendLine($"namespace {NamespaceOf(projectName)};");
        sb.AppendLine();
        return sb;
    }

    // The hub's path and its names on the wire, the same constants on both sides
    private static void AppendNames(
        StringBuilder sb,
        SignalRHub hub,
        (List<NamedMember> Events, List<NamedMember> Methods) members)
    {
        sb.AppendLine(4, "/// <summary>");
        sb.AppendLine(4, "/// The path the hub is mapped at.");
        sb.AppendLine(4, "/// </summary>");
        sb.AppendLine(4, $"public const string Url = {CSharpLiteralHelper.ToStringLiteral(hub.Path)};");
        foreach (var (title, summary, list) in new[]
                 {
                     ("Events", "The names of the events the hub sends, as they are on the wire.", members.Events),
                     ("Methods", "The names of the methods a client invokes, as they are on the wire.", members.Methods),
                 })
        {
            sb.AppendLine();
            sb.AppendLine(4, "/// <summary>");
            sb.AppendLine(4, $"/// {summary}");
            sb.AppendLine(4, "/// </summary>");
            sb.AppendLine(4, $"public static class {title}");
            sb.AppendLine(4, "{");
            foreach (var member in list)
            {
                sb.AppendLine(8, $"public const string {member.Identifier} = {CSharpLiteralHelper.ToStringLiteral(member.Member.Name)};");
            }

            sb.AppendLine(4, "}");
        }
    }

    private static void AppendMembers(
        StringBuilder sb,
        List<NamedMember> members,
        Func<NamedMember, string> declaration,
        Func<NamedMember, string> summary)
    {
        for (var i = 0; i < members.Count; i++)
        {
            if (i > 0)
            {
                sb.AppendLine();
            }

            sb.AppendLine(4, "/// <summary>");
            sb.AppendLine(4, $"/// {summary(members[i])}");
            sb.AppendLine(4, "/// </summary>");
            sb.AppendLine(4, declaration(members[i]));
        }
    }

    // A member's arguments as C# parameters, in their order on the wire: the specification's names in camel case (a
    // keyword with @), numbered where a generated member takes the name itself (cancellationToken, clients) or two
    // names give one
    private static List<(string Type, string Name)> Arguments(
        SignalRHubMember member,
        Dictionary<string, string> models,
        params string[] reserved)
    {
        var taken = new HashSet<string>(reserved, StringComparer.Ordinal);
        var parameters = new List<(string Type, string Name)>();
        foreach (var argument in member.Arguments)
        {
            var pascal = argument.Name.ToPascalCaseForDotNet().WithIdentifierStart("Argument");
            var name = taken.Claim(char.ToLowerInvariant(pascal[0]) + pascal.Substring(1));
            parameters.Add((CSharpType(argument.Payload, models), CSharpIdentifierHelper.EscapeIfKeyword(name)));
        }

        return parameters;
    }

    private static string ParameterList(
        List<(string Type, string Name)> parameters)
        => string.Join(", ", parameters.Select(p => $"{p.Type} {p.Name}"));

    private static string ReturnType(
        SignalRHubPayload? returns,
        Dictionary<string, string> models)
        => returns is null ? "Task" : $"Task<{CSharpType(returns, models)}>";

    private static string TypeName(SignalRHub hub)
        => hub.Key.ToPascalCaseForDotNet();

    // Each event and method with the C# identifier it gets: its own name when that is one, else its PascalCase form;
    // two names that give one identifier are told apart by a number, and so is a name a generated class already has a
    // member of (an event called Events, a method called Start)
    private static (List<NamedMember> Events, List<NamedMember> Methods) Named(
        SignalRHub hub)
        => (Named(hub.Events, "Events"), Named(hub.Methods, "Methods", "Start", "Stop", "Dispose"));

    private static List<NamedMember> Named(
        IReadOnlyList<SignalRHubMember> members,
        params string[] reserved)
    {
        var taken = new HashSet<string>(reserved, StringComparer.Ordinal);
        var named = new List<NamedMember>();
        foreach (var member in members)
        {
            var isIdentifier = IsIdentifier(member.Name);
            var identifier = isIdentifier ? member.Name : member.Name.ToPascalCaseForDotNet();
            if (!IsIdentifier(identifier))
            {
                identifier = "Member" + identifier;
            }

            named.Add(new NamedMember(member, taken.Claim(identifier), isIdentifier));
        }

        return named;
    }

    private static bool IsIdentifier(string name)
        => name.Length > 0 &&
           (char.IsLetter(name[0]) || name[0] == '_') &&
           name.All(c => char.IsLetterOrDigit(c) || c == '_') &&
           !CSharpKeywords.IsKeyword(name);

    private sealed record NamedMember(
        SignalRHubMember Member,
        string Identifier,
        bool NameIsIdentifier);
}