namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Emits clients for SignalR-style server-push hubs declared via the <c>x-signalr-hubs</c> document-level vendor
/// extension. Each hub becomes a framework-neutral <c>{hub}Hub.ts</c> (<c>create&lt;HubKey&gt;HubConnection</c>: a
/// connection whose inbound events go to optional caller-supplied callbacks; with <c>methods</c>,
/// <c>&lt;hubKey&gt;HubMethods</c>: the hub's methods over a connection) and, when the client has React hooks, a
/// <c>use&lt;HubKey&gt;Hub</c> hook that owns the connection lifecycle. The extension is read by
/// <see cref="SignalRHubHelper"/>, the same reader the C# generators use. Distinctive surface — no other OpenAPI TS
/// generator emits this.
/// </summary>
/// <remarks>
/// Expected extension shape (document-level):
/// <code>
/// x-signalr-hubs:
///   notifications:
///     url: /hubs/notifications
///     events:
///       - name: SystemNotification
///         payload: '#/components/schemas/SystemNotification'
///       - name: user-activity
///         payload: '#/components/schemas/UserActivityEvent'
/// </code>
/// The callbacks are <c>{ onSystemNotification?: (n: SystemNotification) =&gt; void; onUserActivity?: ... }</c>: an
/// event name that is no identifier is written in PascalCase for its callback, and stays as it is on the wire. An
/// event declared twice is generated once; two names that give one callback are told apart by a number, as in C#.
/// The hook returns <c>{ connectionState, isConnected, connect, disconnect }</c>, and <c>methods</c> when the hub has
/// any.
/// </remarks>
public static class TypeScriptSignalRHubExtractor
{
    /// <summary>
    /// Parses the <c>x-signalr-hubs</c> document extension and returns the files for every hub: the framework-neutral
    /// <c>{hub}Hub</c> and, with <paramref name="includeReactHooks"/>, the <c>use{Hub}Hub</c> hook. Returns empty when
    /// the extension is missing, malformed, or declares no hubs.
    /// </summary>
    public static List<(string FileName, string Content)> Extract(
        OpenApiDocument openApiDoc,
        string? headerContent,
        TypeScriptWireNames? wireNames = null,
        bool includeReactHooks = true)
    {
        ArgumentNullException.ThrowIfNull(openApiDoc);

        var results = new List<(string FileName, string Content)>();
        foreach (var hub in ParseHubs(openApiDoc))
        {
            var hubPascal = hub.Key.ToPascalCase();
            results.Add((hubPascal.EnsureFirstCharacterToLower() + "Hub", GenerateConnectionFile(openApiDoc, hub, hubPascal, headerContent, wireNames)));
            if (includeReactHooks)
            {
                results.Add(("use" + hubPascal + "Hub", GenerateHookFile(hub, hubPascal, headerContent)));
            }
        }

        return results;
    }

    // Each event with its callback and each method with its function name: an identifier from the name, numbered when
    // two names give one
    private static List<HubDefinition> ParseHubs(OpenApiDocument openApiDoc)
        => SignalRHubHelper.Parse(openApiDoc)
            .Select(hub => new HubDefinition(
                hub.Key,
                hub.Path,
                Named(hub.Events, name => "on" + name),
                Named(hub.Methods, name => name.EnsureFirstCharacterToLower())))
            .ToList();

    private static List<HubMember> Named(
        IReadOnlyList<SignalRHubMember> members,
        Func<string, string> identifier)
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);
        var named = new List<HubMember>();
        foreach (var member in members)
        {
            named.Add(new HubMember(member.Name, taken.Claim(identifier(ToIdentifier(member.Name))), ArgumentsOf(member), member.Returns));
        }

        return named;
    }

    // The short form's one argument keeps the names it always had (`event` in a callback, `payload` in a method)
    private static bool IsShortForm(HubMember member)
        => member.Arguments.Count == 1 && member.Arguments[0].Name == "payload";

    // A member's arguments as TypeScript parameters: the specification's names in camel case, numbered where the
    // generated code uses the name itself (connection, callbacks, event), where it is a reserved word, or where two
    // names give one
    private static List<HubArgument> ArgumentsOf(SignalRHubMember member)
    {
        var taken = new HashSet<string>(["connection", "callbacks", "event", "answer"], StringComparer.Ordinal);
        var arguments = new List<HubArgument>();
        foreach (var argument in member.Arguments)
        {
            var name = ToIdentifier(argument.Name).EnsureFirstCharacterToLower();
            if (ReservedWords.Contains(name))
            {
                name += "Value";
            }

            arguments.Add(new HubArgument(argument.Name, taken.Claim(name), argument.Payload));
        }

        return arguments;
    }

    private static readonly HashSet<string> ReservedWords = new(StringComparer.Ordinal)
    {
        "arguments", "await", "break", "case", "catch", "class", "const", "continue", "debugger", "default", "delete", "do",
        "else", "enum", "eval", "export", "extends", "false", "finally", "for", "function", "if", "implements", "import", "in",
        "instanceof", "interface", "let", "new", "null", "package", "private", "protected", "public", "return", "static",
        "super", "switch", "this", "throw", "true", "try", "typeof", "var", "void", "while", "with", "yield",
    };

    /// <summary>
    /// The framework-neutral hub: its callbacks, its connection states, <c>create{Hub}HubConnection</c> and, with
    /// methods, <c>{hub}HubMethods</c>.
    /// </summary>
    private static string GenerateConnectionFile(
        OpenApiDocument openApiDoc,
        HubDefinition hub,
        string hubPascal,
        string? headerContent,
        TypeScriptWireNames? wireNames)
    {
        var sb = new StringBuilder();
        sb.Append(headerContent);

        // A payload whose model renames keys travels with the API's property names: renamed on the way in and out
        string? ShapeOf(SignalRHubPayload? payload)
            => payload is null || wireNames is null ? null : wireNames.ShapeOf(SchemaOf(openApiDoc, payload));

        var usesFromWire = hub.Events.Any(e => e.Arguments.Any(a => ShapeOf(a.Payload) is not null)) || hub.Methods.Any(m => ShapeOf(m.Returns) is not null);
        var usesToWire = hub.Methods.Any(m => m.Arguments.Any(a => ShapeOf(a.Payload) is not null));

        sb.AppendLine("import { HubConnectionBuilder, HubConnection } from '@microsoft/signalr';");
        if (usesFromWire || usesToWire)
        {
            var helpers = new List<string>();
            if (usesFromWire)
            {
                helpers.Add("fromWire");
            }

            if (usesToWire)
            {
                helpers.Add("toWire");
            }

            sb.Append("import { ").Append(string.Join(", ", helpers)).AppendLine(" } from '../helpers/wireNames';");
        }

        var payloadImports = new SortedSet<string>(
            hub.Events.Concat(hub.Methods).SelectMany(m => m.Arguments.Select(a => a.Payload).Append(m.Returns)).SelectMany(p => p?.Schemas ?? []),
            StringComparer.Ordinal);
        if (payloadImports.Count > 0)
        {
            sb.Append("import type { ").Append(string.Join(", ", payloadImports)).AppendLine(" } from '../models';");
        }

        sb.AppendLine();
        sb.Append("export type ").Append(hubPascal).AppendLine("HubConnectionState = 'Disconnected' | 'Connecting' | 'Connected' | 'Reconnecting';");
        sb.AppendLine();

        sb.Append("export interface ").Append(hubPascal).AppendLine("HubCallbacks {");
        foreach (var ev in hub.Events)
        {
            var parameters = ev.Arguments.Count == 0
                ? "event: unknown"
                : IsShortForm(ev)
                    ? "event: " + TypeOf(ev.Arguments[0].Payload)
                    : string.Join(", ", ev.Arguments.Select(a => a.Identifier + ": " + TypeOf(a.Payload)));
            sb.Append("  ").Append(ev.Identifier).Append("?: (").Append(parameters).AppendLine(") => void;");
        }

        sb.AppendLine("}");
        sb.AppendLine();

        // The callbacks are read when an event arrives, so a caller may swap them while the connection is open
        sb.AppendLine("/**");
        sb.Append(" * A connection to the ").Append(hub.Key).AppendLine(" hub (not started): each event goes to its callback, read when");
        sb.AppendLine(" * the event arrives.");
        sb.AppendLine(" */");
        sb.Append("export function create").Append(hubPascal).Append("HubConnection(baseUrl: string, callbacks: () => ").Append(hubPascal).AppendLine("HubCallbacks): HubConnection {");
        sb.AppendLine("  const connection = new HubConnectionBuilder()");
        sb.Append("    .withUrl(`${baseUrl}").Append(hub.Url.Replace("`", "\\`", StringComparison.Ordinal)).AppendLine("`)");
        sb.AppendLine("    .withAutomaticReconnect()");
        sb.AppendLine("    .build();");
        sb.AppendLine();
        foreach (var ev in hub.Events)
        {
            if (ev.Arguments.Count > 1 || (ev.Arguments.Count == 1 && !IsShortForm(ev)))
            {
                // Several arguments (or one by its own name), each read as its type
                var received = string.Join(", ", ev.Arguments.Select(a => a.Identifier + ": unknown"));
                var passed = string.Join(", ", ev.Arguments.Select(a => ShapeOf(a.Payload) is { } argumentShape
                    ? $"fromWire<{TypeOf(a.Payload)}>({a.Identifier}, {argumentShape})"
                    : $"{a.Identifier} as {TypeOf(a.Payload)}"));
                sb.Append("  connection.on(").Append(Literal(ev.Name)).Append(", (").Append(received).AppendLine(") => {");
                sb.Append("    callbacks().").Append(ev.Identifier).Append("?.(").Append(passed).AppendLine(");");
                sb.AppendLine("  });");
                continue;
            }

            var payload = ev.Arguments.Count == 0 ? "unknown" : TypeOf(ev.Arguments[0].Payload) ?? "unknown";
            if (ev.Arguments.Count == 1 && ShapeOf(ev.Arguments[0].Payload) is { } shape)
            {
                sb.Append("  connection.on(").Append(Literal(ev.Name)).AppendLine(", (event: unknown) => {");
                sb.Append("    callbacks().").Append(ev.Identifier).Append("?.(fromWire<").Append(payload).Append(">(event, ").Append(shape).AppendLine("));");
            }
            else
            {
                sb.Append("  connection.on(").Append(Literal(ev.Name)).Append(", (event: ").Append(payload).AppendLine(") => {");
                sb.Append("    callbacks().").Append(ev.Identifier).AppendLine("?.(event);");
            }

            sb.AppendLine("  });");
        }

        sb.AppendLine();
        sb.AppendLine("  return connection;");
        sb.AppendLine("}");

        if (hub.Methods.Count == 0)
        {
            return sb.ToString();
        }

        sb.AppendLine();
        sb.Append("export interface ").Append(hubPascal).AppendLine("HubMethods {");
        foreach (var method in hub.Methods)
        {
            sb.Append("  ").Append(method.Identifier).Append('(').Append(ParameterList(method))
                .Append("): Promise<").Append(TypeOf(method.Returns) ?? "void").AppendLine(">;");
        }

        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/**");
        sb.Append(" * The ").Append(hub.Key).AppendLine(" hub's methods over a started connection: each invokes the method under its name on");
        sb.AppendLine(" * the wire and resolves with its answer.");
        sb.AppendLine(" */");
        sb.Append("export function ").Append(hubPascal.EnsureFirstCharacterToLower()).Append("HubMethods(connection: Pick<HubConnection, 'invoke'>): ").Append(hubPascal).AppendLine("HubMethods {");
        sb.AppendLine("  return {");
        foreach (var method in hub.Methods)
        {
            var returns = TypeOf(method.Returns) ?? "void";
            var argument = string.Concat(method.Arguments.Select(a => ", " + (ShapeOf(a.Payload) is { } inShape ? $"toWire({a.Identifier}, {inShape})" : a.Identifier)));
            var call = $"connection.invoke<{(ShapeOf(method.Returns) is null ? returns : "unknown")}>({Literal(method.Name)}{argument})";
            if (ShapeOf(method.Returns) is { } outShape)
            {
                call += $".then((answer) => fromWire<{returns}>(answer, {outShape}))";
            }

            sb.Append("    ").Append(method.Identifier).Append(": (").Append(ParameterList(method))
                .Append(") => ").Append(call).AppendLine(",");
        }

        sb.AppendLine("  };");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string ParameterList(HubMember member)
        => string.Join(", ", member.Arguments.Select(a => a.Identifier + ": " + TypeOf(a.Payload)));

    // The TypeScript type of a payload: the model, a primitive, or an array of one; null for none
    private static string? TypeOf(SignalRHubPayload? payload)
        => payload switch
        {
            null => null,
            { Items: { } items } => TypeOf(items) + "[]",
            { SchemaName: { } schema } => schema,
            { Type: "integer" or "number" } => "number",
            { Type: "boolean" } => "boolean",
            _ => "string",
        };

    // The schema of a payload, for the wire shape of what it carries
    private static IOpenApiSchema SchemaOf(
        OpenApiDocument openApiDoc,
        SignalRHubPayload payload)
        => payload switch
        {
            { Items: { } items } => new OpenApiSchema { Type = JsonSchemaType.Array, Items = SchemaOf(openApiDoc, items) },
            { SchemaName: { } schema } => new OpenApiSchemaReference(schema, openApiDoc),
            _ => new OpenApiSchema { Type = JsonSchemaType.String },
        };

    private static string Literal(string name)
        => "'" + name.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal) + "'";

    /// <summary>
    /// The React hook over the neutral hub: it owns the connection lifecycle (StrictMode-safe) and reads the base URL
    /// from the generated <c>ApiProvider</c>.
    /// </summary>
    private static string GenerateHookFile(
        HubDefinition hub,
        string hubPascal,
        string? headerContent)
    {
        var hasMethods = hub.Methods.Count > 0;
        var sb = new StringBuilder();
        sb.Append(headerContent);

        var hubModule = hubPascal.EnsureFirstCharacterToLower() + "Hub";
        sb.AppendLine(hasMethods
            ? "import { useCallback, useEffect, useMemo, useRef, useState } from 'react';"
            : "import { useCallback, useEffect, useRef, useState } from 'react';");
        sb.AppendLine("import type { HubConnection } from '@microsoft/signalr';");
        sb.AppendLine("import { useApiService } from '../hooks/useApiService';");
        sb.Append("import { create").Append(hubPascal).Append("HubConnection, ");
        if (hasMethods)
        {
            sb.Append(hubModule).Append("Methods, ");
        }

        sb.Append("type ").Append(hubPascal).Append("HubConnectionState, type ").Append(hubPascal).Append("HubCallbacks } from './").Append(hubModule).AppendLine("';");
        sb.AppendLine();

        // React's StrictMode unmounts a component right after mounting it, and stopping a connection during its
        // negotiation fails the start: a stop waits for the start to settle
        sb.AppendLine("// Stops a connection once its start has settled (stopping during the negotiation fails the start)");
        sb.AppendLine("async function stopWhenStarted(connection: HubConnection, start: Promise<void> | null): Promise<void> {");
        sb.AppendLine("  await start?.catch(() => undefined);");
        sb.AppendLine("  await connection.stop();");
        sb.AppendLine("}");
        sb.AppendLine();

        // The ApiProvider's baseUrl is the API's, with its base path ("https://host/api/v1", "/api/v1"); a hub is
        // mapped on the server, so its path goes on the origin - or on the page's, for a relative base URL
        sb.AppendLine("// The server a base URL is on: its origin, or '' (the page's) for a relative one - a hub's path is the server's,");
        sb.AppendLine("// not the API's");
        sb.AppendLine("function serverUrlOf(baseUrl: string): string {");
        sb.AppendLine("""  return /^[a-z][a-z\d+.-]*:\/\/[^/?#]+/i.exec(baseUrl)?.[0] ?? '';""");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.Append("export function use").Append(hubPascal).Append("Hub(callbacks: ").Append(hubPascal).AppendLine("HubCallbacks) {");
        sb.AppendLine("  const api = useApiService();");
        sb.Append("  const [connectionState, setConnectionState] = useState<").Append(hubPascal).AppendLine("HubConnectionState>('Disconnected');");
        sb.AppendLine("  const connectionRef = useRef<HubConnection | null>(null);");
        sb.AppendLine("  const startRef = useRef<Promise<void> | null>(null);");
        sb.AppendLine("  const callbacksRef = useRef(callbacks);");
        sb.AppendLine("  callbacksRef.current = callbacks;");
        sb.AppendLine();
        sb.AppendLine("  const isConnected = connectionState === 'Connected';");
        sb.AppendLine();
        sb.AppendLine("  const connect = useCallback(async () => {");
        sb.AppendLine("    if (connectionRef.current) return;");
        sb.AppendLine();
        sb.AppendLine("    setConnectionState('Connecting');");
        sb.Append("    const connection = create").Append(hubPascal).AppendLine("HubConnection(serverUrlOf(api.baseUrl), () => callbacksRef.current);");
        sb.AppendLine();
        sb.AppendLine("    // Only the current connection updates the state: one that was replaced or stopped is history, and a");
        sb.AppendLine("    // closed one is let go of so connect() starts a new one");
        sb.AppendLine("    const isCurrent = () => connectionRef.current === connection;");
        sb.AppendLine("    connection.onreconnecting(() => {");
        sb.AppendLine("      if (isCurrent()) setConnectionState('Reconnecting');");
        sb.AppendLine("    });");
        sb.AppendLine("    connection.onreconnected(() => {");
        sb.AppendLine("      if (isCurrent()) setConnectionState('Connected');");
        sb.AppendLine("    });");
        sb.AppendLine("    connection.onclose(() => {");
        sb.AppendLine("      if (!isCurrent()) return;");
        sb.AppendLine("      connectionRef.current = null;");
        sb.AppendLine("      setConnectionState('Disconnected');");
        sb.AppendLine("    });");
        sb.AppendLine();
        sb.AppendLine("    connectionRef.current = connection;");
        sb.AppendLine("    const start = connection.start();");
        sb.AppendLine("    startRef.current = start;");
        sb.AppendLine("    try {");
        sb.AppendLine("      await start;");
        sb.AppendLine("      if (isCurrent()) setConnectionState('Connected');");
        sb.AppendLine("    } catch {");
        sb.AppendLine("      if (isCurrent()) {");
        sb.AppendLine("        connectionRef.current = null;");
        sb.AppendLine("        setConnectionState('Disconnected');");
        sb.AppendLine("      }");
        sb.AppendLine("    }");
        sb.AppendLine("  }, [api]);");
        sb.AppendLine();
        sb.AppendLine("  const disconnect = useCallback(async () => {");
        sb.AppendLine("    const connection = connectionRef.current;");
        sb.AppendLine("    if (!connection) return;");
        sb.AppendLine();
        sb.AppendLine("    connectionRef.current = null;");
        sb.AppendLine("    try {");
        sb.AppendLine("      await stopWhenStarted(connection, startRef.current);");
        sb.AppendLine("    } finally {");
        sb.AppendLine("      setConnectionState('Disconnected');");
        sb.AppendLine("    }");
        sb.AppendLine("  }, []);");
        sb.AppendLine();
        sb.AppendLine("  useEffect(() => {");
        sb.AppendLine("    return () => {");
        sb.AppendLine("      const connection = connectionRef.current;");
        sb.AppendLine("      connectionRef.current = null;");
        sb.AppendLine("      if (connection) {");
        sb.AppendLine("        void stopWhenStarted(connection, startRef.current);");
        sb.AppendLine("      }");
        sb.AppendLine("    };");
        sb.AppendLine("  }, []);");
        sb.AppendLine();
        if (!hasMethods)
        {
            sb.AppendLine("  return { connectionState, isConnected, connect, disconnect };");
            sb.AppendLine("}");
            return sb.ToString();
        }

        // The methods go to the connection of the moment, and fail while there is none
        sb.AppendLine("  const methods = useMemo(");
        sb.Append("    () => ").Append(hubModule).AppendLine("Methods({");
        sb.AppendLine("      invoke: <T>(methodName: string, ...args: unknown[]): Promise<T> => {");
        sb.AppendLine("        const connection = connectionRef.current;");
        sb.Append("        return connection ? connection.invoke<T>(methodName, ...args) : Promise.reject(new Error('The ").Append(hub.Key.Replace("'", "\\'", StringComparison.Ordinal)).AppendLine(" hub is not connected'));");
        sb.AppendLine("      },");
        sb.AppendLine("    }),");
        sb.AppendLine("    [],");
        sb.AppendLine("  );");
        sb.AppendLine();
        sb.AppendLine("  return { connectionState, isConnected, connect, disconnect, methods };");
        sb.AppendLine("}");

        return sb.ToString();
    }

    // An event name as a PascalCase identifier part: "device-updated" is "DeviceUpdated", "2fa" is "_2fa"
    private static string ToIdentifier(string name)
    {
        var sb = new StringBuilder();
        var upperNext = true;
        foreach (var c in name)
        {
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                upperNext = true;
                continue;
            }

            sb.Append(upperNext ? char.ToUpperInvariant(c) : c);
            upperNext = false;
        }

        if (sb.Length == 0 || char.IsDigit(sb[0]))
        {
            sb.Insert(0, '_');
        }

        return sb.ToString();
    }

    private sealed record HubDefinition(
        string Key,
        string Url,
        List<HubMember> Events,
        List<HubMember> Methods);

    private sealed record HubMember(
        string Name,
        string Identifier,
        List<HubArgument> Arguments,
        SignalRHubPayload? Returns);

    private sealed record HubArgument(
        string Name,
        string Identifier,
        SignalRHubPayload Payload);
}