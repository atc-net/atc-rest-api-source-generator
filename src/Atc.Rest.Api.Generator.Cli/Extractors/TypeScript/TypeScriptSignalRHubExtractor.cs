namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Emits React hooks for SignalR-style server-push hubs declared via the
/// <c>x-signalr-hubs</c> document-level vendor extension. Each hub becomes a
/// <c>use&lt;HubKey&gt;Hub</c> hook that owns the connection lifecycle and forwards
/// inbound events to optional caller-supplied callbacks. The shape mirrors the
/// hand-written Showcase hook so consumers migrating from hand-written to generated
/// keep the same call-site API. Distinctive surface — no other OpenAPI TS generator
/// emits this.
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
///       - name: UserActivity
///         payload: '#/components/schemas/UserActivityEvent'
/// </code>
/// Each hub key produces a hook file <c>useNotificationsHub.ts</c>. The hook accepts
/// <c>callbacks: { onSystemNotification?: (n: SystemNotification) =&gt; void; ... }</c>
/// and returns <c>{ connectionState, isConnected, connect, disconnect }</c>.
/// </remarks>
public static class TypeScriptSignalRHubExtractor
{
    /// <summary>
    /// Parses the <c>x-signalr-hubs</c> document extension and returns one
    /// (FileName, Content) tuple per hub. Returns empty when the extension is
    /// missing, malformed, or declares no hubs.
    /// </summary>
    public static List<(string FileName, string Content)> Extract(
        OpenApiDocument openApiDoc,
        string? headerContent)
    {
        ArgumentNullException.ThrowIfNull(openApiDoc);

        var results = new List<(string FileName, string Content)>();
        var hubs = ParseHubs(openApiDoc);
        foreach (var hub in hubs)
        {
            var fileName = "use" + hub.Key.ToPascalCase() + "Hub";
            var content = GenerateHookFile(hub, headerContent);
            results.Add((fileName, content));
        }

        return results;
    }

    private static List<HubDefinition> ParseHubs(OpenApiDocument openApiDoc)
    {
        var hubs = new List<HubDefinition>();
        if (openApiDoc.Extensions == null ||
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
            if (hubObj["events"] is JsonArray eventsArr)
            {
                foreach (var ev in eventsArr)
                {
                    if (ev is not JsonObject evObj)
                    {
                        continue;
                    }

                    if (evObj["name"] is not JsonValue nameValue || !nameValue.TryGetValue<string>(out var name))
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

                    events.Add(new HubEvent(name, payloadType));
                }
            }

            hubs.Add(new HubDefinition(hubKey, url, events));
        }

        return hubs;
    }

    private static string GenerateHookFile(
        HubDefinition hub,
        string? headerContent)
    {
        var sb = new StringBuilder();
        if (headerContent != null)
        {
            sb.Append(headerContent);
        }

        sb.AppendLine("import { useCallback, useEffect, useRef, useState } from 'react';");
        sb.AppendLine("import { HubConnectionBuilder, HubConnection } from '@microsoft/signalr';");
        sb.AppendLine("import { useApiService } from './useApiService';");

        var payloadImports = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var ev in hub.Events)
        {
            if (ev.PayloadType != null)
            {
                payloadImports.Add(ev.PayloadType);
            }
        }

        if (payloadImports.Count > 0)
        {
            sb.Append("import type { ").Append(string.Join(", ", payloadImports)).AppendLine(" } from '../models';");
        }

        sb.AppendLine();
        sb.AppendLine("export type HubConnectionState = 'Disconnected' | 'Connecting' | 'Connected' | 'Reconnecting';");
        sb.AppendLine();

        // Callbacks shape: one optional per event, with the prefix `on` + the event name
        // verbatim (preserves the wire-name casing so consumers can grep the hub side).
        var hubPascal = hub.Key.ToPascalCase();
        sb.Append("export interface ").Append(hubPascal).AppendLine("HubCallbacks {");
        foreach (var ev in hub.Events)
        {
            var callbackName = "on" + ev.Name;
            var payload = ev.PayloadType ?? "unknown";
            sb.Append("  ").Append(callbackName).Append("?: (event: ").Append(payload).AppendLine(") => void;");
        }

        sb.AppendLine("}");
        sb.AppendLine();

        var hookName = "use" + hubPascal + "Hub";
        sb.Append("export function ").Append(hookName).Append("(callbacks: ").Append(hubPascal).AppendLine("HubCallbacks) {");
        sb.AppendLine("  const api = useApiService();");
        sb.AppendLine("  const [connectionState, setConnectionState] = useState<HubConnectionState>('Disconnected');");
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
        sb.AppendLine("    const connection = new HubConnectionBuilder()");
        sb.Append("      .withUrl(`${api.baseUrl}").Append(hub.Url).AppendLine("`)");
        sb.AppendLine("      .withAutomaticReconnect()");
        sb.AppendLine("      .build();");
        sb.AppendLine();
        sb.AppendLine("    connection.onreconnecting(() => setConnectionState('Reconnecting'));");
        sb.AppendLine("    connection.onreconnected(() => setConnectionState('Connected'));");
        sb.AppendLine("    connection.onclose(() => setConnectionState('Disconnected'));");
        sb.AppendLine();
        foreach (var ev in hub.Events)
        {
            var payload = ev.PayloadType ?? "unknown";
            sb.Append("    connection.on('").Append(ev.Name).Append("', (event: ").Append(payload).AppendLine(") => {");
            sb.Append("      callbacksRef.current.on").Append(ev.Name).AppendLine("?.(event);");
            sb.AppendLine("    });");
            sb.AppendLine();
        }

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

    private sealed record HubDefinition(
        string Key,
        string Url,
        List<HubEvent> Events);

    private sealed record HubEvent(
        string Name,
        string? PayloadType);
}