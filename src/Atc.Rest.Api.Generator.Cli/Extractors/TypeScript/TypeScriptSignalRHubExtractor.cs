namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Emits clients for SignalR-style server-push hubs declared via the <c>x-signalr-hubs</c> document-level vendor
/// extension. Each hub becomes a framework-neutral <c>{hub}Hub.ts</c> (<c>create&lt;HubKey&gt;HubConnection</c>: a
/// connection whose inbound events go to optional caller-supplied callbacks) and, when the client has React hooks, a
/// <c>use&lt;HubKey&gt;Hub</c> hook that owns the connection lifecycle. Distinctive surface — no other OpenAPI TS
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
/// event declared twice is generated once. The hook returns <c>{ connectionState, isConnected, connect, disconnect }</c>.
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
            results.Add((ToCamelCase(hubPascal) + "Hub", GenerateConnectionFile(hub, hubPascal, headerContent, wireNames)));
            if (includeReactHooks)
            {
                results.Add(("use" + hubPascal + "Hub", GenerateHookFile(hubPascal, headerContent)));
            }
        }

        return results;
    }

    private static List<HubDefinition> ParseHubs(OpenApiDocument openApiDoc)
    {
        var hubs = new List<HubDefinition>();
        if (openApiDoc.Extensions is null ||
            !openApiDoc.Extensions.TryGetValue("x-signalr-hubs", out var extension) ||
            extension is not JsonNodeExtension jsonNodeExt ||
            jsonNodeExt.Node is not JsonObject hubsObject)
        {
            return hubs;
        }

        foreach (var (hubKey, hubValue) in hubsObject)
        {
            if (hubValue is not JsonObject hubObj)
            {
                continue;
            }

            var url = hubObj["url"] is JsonValue urlValue && urlValue.TryGetValue<string>(out var u)
                ? u
                : "/hubs/" + hubKey;

            var events = new List<HubEvent>();
            var callbackNames = new HashSet<string>(StringComparer.Ordinal);
            if (hubObj["events"] is JsonArray eventsArr)
            {
                foreach (var ev in eventsArr)
                {
                    if (ev is not JsonObject evObj ||
                        evObj["name"] is not JsonValue nameValue ||
                        !nameValue.TryGetValue<string>(out var name) ||
                        string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    // One callback per event: an event declared twice (or two names that give one callback) once
                    var callbackName = "on" + ToIdentifier(name);
                    if (!callbackNames.Add(callbackName))
                    {
                        continue;
                    }

                    string? payloadType = null;
                    if (evObj["payload"] is JsonValue payloadValue && payloadValue.TryGetValue<string>(out var payloadRef))
                    {
                        // Extract the schema name from a "#/components/schemas/X" ref string.
                        var lastSlash = payloadRef.LastIndexOf('/');
                        payloadType = lastSlash >= 0 && lastSlash < payloadRef.Length - 1
                            ? payloadRef[(lastSlash + 1)..]
                            : payloadRef;
                    }

                    events.Add(new HubEvent(name, callbackName, payloadType));
                }
            }

            hubs.Add(new HubDefinition(hubKey, url, events));
        }

        return hubs;
    }

    /// <summary>
    /// The framework-neutral hub: its callbacks, its connection states and <c>create{Hub}HubConnection</c>.
    /// </summary>
    private static string GenerateConnectionFile(
        HubDefinition hub,
        string hubPascal,
        string? headerContent,
        TypeScriptWireNames? wireNames)
    {
        var sb = new StringBuilder();
        if (headerContent is not null)
        {
            sb.Append(headerContent);
        }

        // An event payload whose model renames keys arrives with the API's property names: rename it first.
        var payloadShapes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var ev in hub.Events)
        {
            if (ev.PayloadType is not null && wireNames?.ShapeOfModel(ev.PayloadType) is { } shape)
            {
                payloadShapes[ev.Name] = shape;
            }
        }

        sb.AppendLine("import { HubConnectionBuilder, HubConnection } from '@microsoft/signalr';");
        if (payloadShapes.Count > 0)
        {
            sb.AppendLine("import { fromWire } from '../helpers/wireNames';");
        }

        var payloadImports = new SortedSet<string>(hub.Events.Where(e => e.PayloadType is not null).Select(e => e.PayloadType!), StringComparer.Ordinal);
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
            sb.Append("  ").Append(ev.CallbackName).Append("?: (event: ").Append(ev.PayloadType ?? "unknown").AppendLine(") => void;");
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
            var wireName = ev.Name.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal);
            var payload = ev.PayloadType ?? "unknown";
            if (payloadShapes.TryGetValue(ev.Name, out var shape))
            {
                sb.Append("  connection.on('").Append(wireName).AppendLine("', (event: unknown) => {");
                sb.Append("    callbacks().").Append(ev.CallbackName).Append("?.(fromWire<").Append(payload).Append(">(event, ").Append(shape).AppendLine("));");
            }
            else
            {
                sb.Append("  connection.on('").Append(wireName).Append("', (event: ").Append(payload).AppendLine(") => {");
                sb.Append("    callbacks().").Append(ev.CallbackName).AppendLine("?.(event);");
            }

            sb.AppendLine("  });");
        }

        sb.AppendLine();
        sb.AppendLine("  return connection;");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// The React hook over the neutral hub: it owns the connection lifecycle (StrictMode-safe) and reads the base URL
    /// from the generated <c>ApiProvider</c>.
    /// </summary>
    private static string GenerateHookFile(
        string hubPascal,
        string? headerContent)
    {
        var sb = new StringBuilder();
        if (headerContent is not null)
        {
            sb.Append(headerContent);
        }

        var hubModule = ToCamelCase(hubPascal) + "Hub";
        sb.AppendLine("import { useCallback, useEffect, useRef, useState } from 'react';");
        sb.AppendLine("import type { HubConnection } from '@microsoft/signalr';");
        sb.AppendLine("import { useApiService } from '../hooks/useApiService';");
        sb.Append("import { create").Append(hubPascal).Append("HubConnection, type ").Append(hubPascal).Append("HubConnectionState, type ").Append(hubPascal).Append("HubCallbacks } from './").Append(hubModule).AppendLine("';");
        sb.AppendLine();

        sb.Append("export function use").Append(hubPascal).Append("Hub(callbacks: ").Append(hubPascal).AppendLine("HubCallbacks) {");
        sb.AppendLine("  const api = useApiService();");
        sb.Append("  const [connectionState, setConnectionState] = useState<").Append(hubPascal).AppendLine("HubConnectionState>('Disconnected');");
        sb.AppendLine("  const connectionRef = useRef<HubConnection | null>(null);");
        sb.AppendLine("  const callbacksRef = useRef(callbacks);");
        sb.AppendLine("  callbacksRef.current = callbacks;");
        sb.AppendLine();
        sb.AppendLine("  const isConnected = connectionState === 'Connected';");
        sb.AppendLine();
        sb.AppendLine("  const connect = useCallback(async () => {");
        sb.AppendLine("    if (connectionRef.current) return;");
        sb.AppendLine();
        sb.AppendLine("    setConnectionState('Connecting');");
        sb.Append("    const connection = create").Append(hubPascal).AppendLine("HubConnection(api.baseUrl, () => callbacksRef.current);");
        sb.AppendLine("    connection.onreconnecting(() => setConnectionState('Reconnecting'));");
        sb.AppendLine("    connection.onreconnected(() => setConnectionState('Connected'));");
        sb.AppendLine("    connection.onclose(() => setConnectionState('Disconnected'));");
        sb.AppendLine();
        sb.AppendLine("    connectionRef.current = connection;");
        sb.AppendLine();
        sb.AppendLine("    try {");
        sb.AppendLine("      await connection.start();");

        // StrictMode-safe: only mutate state if our connection ref is still the active one.
        sb.AppendLine("      if (connectionRef.current === connection) {");
        sb.AppendLine("        setConnectionState('Connected');");
        sb.AppendLine("      }");
        sb.AppendLine("    } catch {");
        sb.AppendLine("      if (connectionRef.current === connection) {");
        sb.AppendLine("        setConnectionState('Disconnected');");
        sb.AppendLine("        connectionRef.current = null;");
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
        sb.AppendLine("      await connection.stop();");
        sb.AppendLine("    } finally {");
        sb.AppendLine("      setConnectionState('Disconnected');");
        sb.AppendLine("    }");
        sb.AppendLine("  }, []);");
        sb.AppendLine();
        sb.AppendLine("  useEffect(() => {");
        sb.AppendLine("    return () => {");
        sb.AppendLine("      connectionRef.current?.stop();");
        sb.AppendLine("      connectionRef.current = null;");
        sb.AppendLine("    };");
        sb.AppendLine("  }, []);");
        sb.AppendLine();
        sb.AppendLine("  return { connectionState, isConnected, connect, disconnect };");
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

    private static string ToCamelCase(string pascal)
        => pascal.Length == 0 ? pascal : char.ToLowerInvariant(pascal[0]) + pascal[1..];

    private sealed record HubDefinition(
        string Key,
        string Url,
        List<HubEvent> Events);

    private sealed record HubEvent(
        string Name,
        string CallbackName,
        string? PayloadType);
}