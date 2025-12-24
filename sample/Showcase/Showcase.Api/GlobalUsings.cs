global using System.Collections.Concurrent;
global using System.Diagnostics;
global using System.Runtime.CompilerServices;
global using System.Text.Json.Serialization;

global using Microsoft.AspNetCore.SignalR;

global using Scalar.AspNetCore;

global using Showcase;
global using Showcase.Api;
global using Showcase.Api.Contracts.Services;
global using Showcase.Api.Domain.Repositories;
global using Showcase.Api.Domain.WebhookHandlers;
global using Showcase.Api.Hubs;
global using Showcase.Api.Services;
global using Showcase.Generated.Extensions;
global using Showcase.Generated.Notifications.Models;
global using Showcase.Generated.Webhooks.Endpoints;