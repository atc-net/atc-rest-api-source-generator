global using System.ComponentModel;
global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;
global using System.Globalization;
global using System.Text;
global using System.Text.Json;

global using Atc.Console.Spectre;
global using Atc.Console.Spectre.Factories;
global using Atc.Console.Spectre.Helpers;
global using Atc.Console.Spectre.Logging;

global using Atc.OpenApi.Helpers;
global using Atc.Rest.Api.CliGenerator.Commands;
global using Atc.Rest.Api.CliGenerator.Commands.Settings;
global using Atc.Rest.Api.CliGenerator.Enums;
global using Atc.Rest.Api.CliGenerator.Extensions;
global using Atc.Rest.Api.CliGenerator.Options;
global using Atc.Rest.Api.CliGenerator.Services;
global using Atc.Rest.Api.Generator.Configurations;
global using Atc.Rest.Api.Generator.Helpers;
global using Atc.Rest.Api.Generator.Models;
global using Atc.Rest.Api.Generator.Services;
global using Atc.Rest.Api.Generator.Validators;
global using Atc.Serialization;

global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.OpenApi;

global using Spectre.Console;
global using Spectre.Console.Cli;