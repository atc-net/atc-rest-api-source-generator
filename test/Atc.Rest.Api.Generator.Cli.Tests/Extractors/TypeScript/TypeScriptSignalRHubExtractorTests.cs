namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

public class TypeScriptSignalRHubExtractorTests
{
    private const string NotificationsYaml = """
                                             openapi: 3.0.3
                                             info: { title: T, version: '1' }
                                             x-signalr-hubs:
                                               notifications:
                                                 url: /hubs/notifications
                                                 events:
                                                   - name: SystemNotification
                                                     payload: '#/components/schemas/SystemNotification'
                                                   - name: user-activity
                                                     payload: '#/components/schemas/UserActivityEvent'
                                                   - name: SystemNotification
                                                     payload: '#/components/schemas/SystemNotification'
                                             paths: {}
                                             components:
                                               schemas:
                                                 SystemNotification: { type: object }
                                                 UserActivityEvent: { type: object }
                                             """;

    [Fact]
    public void Extract_Hub_IsAFrameworkNeutralConnection_WithItsCallbacks()
    {
        // x-signalr-hubs at the document level declares one or more push channels. Each hub key becomes a
        // notificationsHub file: its callbacks (one optional per event) and a connection that forwards each event.
        var doc = ParseYaml(NotificationsYaml);
        Assert.NotNull(doc);

        var results = TypeScriptSignalRHubExtractor.Extract(doc, headerContent: null);
        var content = Assert.Single(results, r => r.FileName == "notificationsHub").Content;

        Assert.Contains("import { HubConnectionBuilder, HubConnection } from '@microsoft/signalr';", content, StringComparison.Ordinal);
        Assert.Contains("import type { SystemNotification, UserActivityEvent } from '../models';", content, StringComparison.Ordinal);
        Assert.DoesNotContain("react", content, StringComparison.Ordinal);

        Assert.Contains("export type NotificationsHubConnectionState = 'Disconnected' | 'Connecting' | 'Connected' | 'Reconnecting';", content, StringComparison.Ordinal);
        Assert.Contains("export interface NotificationsHubCallbacks {", content, StringComparison.Ordinal);
        Assert.Contains("onSystemNotification?: (event: SystemNotification) => void;", content, StringComparison.Ordinal);
        Assert.Contains("export function createNotificationsHubConnection(baseUrl: string, callbacks: () => NotificationsHubCallbacks): HubConnection {", content, StringComparison.Ordinal);
        Assert.Contains(".withUrl(`${baseUrl}/hubs/notifications`)", content, StringComparison.Ordinal);
        Assert.Contains("connection.on('SystemNotification', (event: SystemNotification) => {", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_EventNames_AreIdentifiersForTheirCallbacks_AndADuplicateIsGeneratedOnce()
    {
        // "user-activity" is no identifier: its callback is onUserActivity, the wire name stays as declared. The
        // second SystemNotification is one callback already (it used to throw on the duplicate key).
        var doc = ParseYaml(NotificationsYaml);
        Assert.NotNull(doc);

        var content = Assert.Single(TypeScriptSignalRHubExtractor.Extract(doc, headerContent: null), r => r.FileName == "notificationsHub").Content;

        Assert.Contains("onUserActivity?: (event: UserActivityEvent) => void;", content, StringComparison.Ordinal);
        Assert.Contains("connection.on('user-activity', (event: UserActivityEvent) => {", content, StringComparison.Ordinal);
        Assert.Contains("callbacks().onUserActivity?.(event);", content, StringComparison.Ordinal);
        Assert.Equal(1, CountOf(content, "onSystemNotification?:"));
        Assert.Equal(1, CountOf(content, "connection.on('SystemNotification'"));
    }

    [Fact]
    public void Extract_WithReactHooks_AddsAHookOverTheConnection()
    {
        // The hook owns the lifecycle and reads the base URL from the generated ApiProvider (hooks/useApiService).
        var doc = ParseYaml(NotificationsYaml);
        Assert.NotNull(doc);

        var results = TypeScriptSignalRHubExtractor.Extract(doc, headerContent: null, includeReactHooks: true);
        var hook = Assert.Single(results, r => r.FileName == "useNotificationsHub").Content;

        Assert.Contains("import { useCallback, useEffect, useRef, useState } from 'react';", hook, StringComparison.Ordinal);
        Assert.Contains("import { useApiService } from '../hooks/useApiService';", hook, StringComparison.Ordinal);
        Assert.Contains("from './notificationsHub';", hook, StringComparison.Ordinal);
        Assert.Contains("export function useNotificationsHub(callbacks: NotificationsHubCallbacks)", hook, StringComparison.Ordinal);
        Assert.Contains("createNotificationsHubConnection(api.baseUrl, () => callbacksRef.current)", hook, StringComparison.Ordinal);
        Assert.Contains("return { connectionState, isConnected, connect, disconnect };", hook, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_WithoutReactHooks_IsTheConnectionOnly()
    {
        // --hooks None: no React, no hooks folder to import from
        var doc = ParseYaml(NotificationsYaml);
        Assert.NotNull(doc);

        var results = TypeScriptSignalRHubExtractor.Extract(doc, headerContent: null, includeReactHooks: false);

        Assert.Equal(["notificationsHub"], results.Select(r => r.FileName), StringComparer.Ordinal);
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
    public void Extract_MultipleHubs_FilesPerHub()
    {
        // Two hubs → a connection and a hook each, with their own callbacks and state types (one barrel exports all).
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

        Assert.Equal(["chatHub", "useChatHub", "presenceHub", "usePresenceHub"], results.Select(r => r.FileName), StringComparer.Ordinal);
        Assert.Contains("ChatHubConnectionState", results[0].Content, StringComparison.Ordinal);
        Assert.Contains("PresenceHubConnectionState", results[2].Content, StringComparison.Ordinal);
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
        var content = Assert.Single(results, r => r.FileName == "healthHub").Content;

        Assert.Contains("onPing?: (event: unknown) => void;", content, StringComparison.Ordinal);
    }

    private static int CountOf(
        string text,
        string value)
        => (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}