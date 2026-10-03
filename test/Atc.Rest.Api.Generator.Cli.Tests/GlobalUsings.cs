global using System.ComponentModel;
global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;
global using System.Linq;
global using System.Text;
global using System.Text.RegularExpressions;

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
global using Atc.Rest.Api.Generator.Models;

global using Microsoft.OpenApi;

global using Spectre.Console;
global using Spectre.Console.Cli;

global using Xunit;