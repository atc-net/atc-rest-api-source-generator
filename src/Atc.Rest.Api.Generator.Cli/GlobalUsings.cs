global using System;
global using System.Collections.Concurrent;
global using System.Collections.Generic;
global using System.ComponentModel;
global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;
global using System.Globalization;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Text;
global using System.Text.Json;
global using System.Text.RegularExpressions;
global using System.Threading.Tasks;

global using Atc.Console.Spectre;
global using Atc.Console.Spectre.Factories;
global using Atc.Console.Spectre.Helpers;
global using Atc.Console.Spectre.Logging;
global using Atc.DotNet;
global using Atc.Helpers;

global using Atc.OpenApi.Helpers;
global using Atc.Rest.Api.Generator.Cli.Commands;
global using Atc.Rest.Api.Generator.Cli.Commands.Settings;
global using Atc.Rest.Api.Generator.Cli.Enums;
global using Atc.Rest.Api.Generator.Cli.Extensions;
global using Atc.Rest.Api.Generator.Cli.Helpers;
global using Atc.Rest.Api.Generator.Cli.Models.Migration;
global using Atc.Rest.Api.Generator.Cli.Options;
global using Atc.Rest.Api.Generator.Cli.Services;
global using Atc.Rest.Api.Generator.Cli.Services.Migration;
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