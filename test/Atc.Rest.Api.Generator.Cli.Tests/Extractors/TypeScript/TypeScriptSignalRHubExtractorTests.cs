namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

public class TypeScriptSignalRHubExtractorTests
{
    [Fact]
    public void Extract_DocumentWithSignalRHubExtension_EmitsHookPerHub()
    {
        // x-signalr-hubs at the document level declares one or more push channels.
        // Each hub key becomes a use<HubKey>Hub file with callbacks per event.
        const string yaml = """
                            openapi: 3.0.3
                            info: { title: T, version: '1' }
                            x-signalr-hubs:
                              notifications:
                                url: /hubs/notifications
                                events:
                                  - name: SystemNotification
                                    payload: '#/components/schemas/SystemNotification'
                                  - name: UserActivity
                                    payload: '#/components/schemas/UserActivityEvent'
                            paths: {}
                            components:
                              schemas:
                                SystemNotification: { type: object }
                                UserActivityEvent: { type: object }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var results = TypeScriptSignalRHubExtractor.Extract(doc, headerContent: null);
        var (fileName, content) = Assert.Single(results);

        Assert.Equal("useNotificationsHub", fileName);

        // Imports cover React primitives, SignalR runtime, model payloads.
        Assert.Contains("import { useCallback, useEffect, useRef, useState } from 'react';", content, StringComparison.Ordinal);
        Assert.Contains("import { HubConnectionBuilder, HubConnection } from '@microsoft/signalr';", content, StringComparison.Ordinal);
        Assert.Contains("import type { SystemNotification, UserActivityEvent } from '../models';", content, StringComparison.Ordinal);

        // Callbacks interface — one optional per event, prefix `on` + the event's wire name.
        Assert.Contains("export interface NotificationsHubCallbacks {", content, StringComparison.Ordinal);
        Assert.Contains("onSystemNotification?: (event: SystemNotification) => void;", content, StringComparison.Ordinal);
        Assert.Contains("onUserActivity?: (event: UserActivityEvent) => void;", content, StringComparison.Ordinal);

        // Hook signature exposes the callbacks bag and returns the lifecycle API.
        Assert.Contains("export function useNotificationsHub(callbacks: NotificationsHubCallbacks)", content, StringComparison.Ordinal);
        Assert.Contains("return { connectionState, isConnected, connect, disconnect };", content, StringComparison.Ordinal);

        // Connection wiring — withUrl interpolates the document-declared path under the
        // ApiClient's base URL, and each event registers a forwarding handler.
        Assert.Contains(".withUrl(`${api.baseUrl}/hubs/notifications`)", content, StringComparison.Ordinal);
        Assert.Contains("connection.on('SystemNotification', (event: SystemNotification)", content, StringComparison.Ordinal);
        Assert.Contains("connection.on('UserActivity', (event: UserActivityEvent)", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_NoSignalRExtension_ReturnsEmpty()
    {
        // Specs without the vendor extension emit nothing — no surprise hub files.
        const string yaml = """
                            openapi: 3.0.3
                            info: { title: T, version: '1' }
                            paths: {}
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var results = TypeScriptSignalRHubExtractor.Extract(doc, headerContent: null);

        Assert.Empty(results);
    }

    [Fact]
    public void Extract_MultipleHubs_OneFilePerHub()
    {
        // Two hubs → two files, each with its own callbacks interface and lifecycle API.
        const string yaml = """
                            openapi: 3.0.3
                            info: { title: T, version: '1' }
                            x-signalr-hubs:
                              chat:
                                url: /hubs/chat
                                events:
                                  - name: Message
                                    payload: '#/components/schemas/ChatMessage'
                              presence:
                                url: /hubs/presence
                                events:
                                  - name: UserOnline
                                    payload: '#/components/schemas/User'
                            paths: {}
                            components:
                              schemas:
                                ChatMessage: { type: object }
                                User: { type: object }
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var results = TypeScriptSignalRHubExtractor.Extract(doc, headerContent: null);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.FileName == "useChatHub");
        Assert.Contains(results, r => r.FileName == "usePresenceHub");
    }

    [Fact]
    public void Extract_EventWithoutPayload_TypesAsUnknown()
    {
        // Hubs sometimes carry parameterless events (e.g. ConnectionLost ping). When the
        // spec omits payload, the callback typing defaults to `unknown` so the consumer
        // explicitly casts if they want to inspect the payload.
        const string yaml = """
                            openapi: 3.0.3
                            info: { title: T, version: '1' }
                            x-signalr-hubs:
                              health:
                                url: /hubs/health
                                events:
                                  - name: Ping
                            paths: {}
                            """;
        var doc = ParseYaml(yaml);
        Assert.NotNull(doc);

        var results = TypeScriptSignalRHubExtractor.Extract(doc, headerContent: null);
        var (_, content) = Assert.Single(results);

        Assert.Contains("onPing?: (event: unknown) => void;", content, StringComparison.Ordinal);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}