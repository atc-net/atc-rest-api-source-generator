global using System.Collections.Concurrent;
global using System.ComponentModel;
global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;
global using System.Globalization;
global using System.Linq;
global using System.Net;
global using System.Reflection;
global using System.Text;
global using System.Text.Json;
global using System.Text.Json.Nodes;
global using System.Text.Json.Serialization;
global using System.Text.RegularExpressions;
global using System.Xml.Linq;

global using Atc.Helpers;
global using Atc.OpenApi.Helpers;

global using Atc.Rest.Api.Generator.Cli.Commands;
global using Atc.Rest.Api.Generator.Cli.Commands.Settings;
global using Atc.Rest.Api.Generator.Cli.Enums;
global using Atc.Rest.Api.Generator.Cli.Extensions;
global using Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;
global using Atc.Rest.Api.Generator.Cli.Helpers;
global using Atc.Rest.Api.Generator.Cli.Models.Migration;
global using Atc.Rest.Api.Generator.Cli.Options;
global using Atc.Rest.Api.Generator.Cli.Services;
global using Atc.Rest.Api.Generator.Cli.Services.Migration;
global using Atc.Rest.Api.Generator.Cli.Tests.Helpers;
global using Atc.Rest.Api.Generator.Configurations;
global using Atc.Rest.Api.Generator.Helpers;
global using Atc.Rest.Api.Generator.JsonConverters;
global using Atc.Rest.Api.Generator.Models;
global using Atc.Serialization;

global using Json.Schema;

global using Microsoft.OpenApi;

global using Spectre.Console;
global using Spectre.Console.Cli;

global using Xunit;global using Atc.Rest.Api.Tests.Node;
