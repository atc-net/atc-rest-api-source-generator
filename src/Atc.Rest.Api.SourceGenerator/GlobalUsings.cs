global using System.Collections.Immutable;
global using System.Diagnostics.CodeAnalysis;
global using System.Text;
global using System.Text.Json;
global using System.Text.RegularExpressions;

global using Atc.CodeGeneration.CSharp.CodeDocumentation.CodeComment;
global using Atc.CodeGeneration.CSharp.Content;
global using Atc.CodeGeneration.CSharp.Content.Generators;
global using Atc.CodeGeneration.CSharp.Helpers;
global using Atc.OpenApi.Extensions;
global using Atc.OpenApi.Helpers;
global using Atc.OpenApi.Models;
global using Atc.Rest.Api.Generator;
global using Atc.Rest.Api.Generator.Configurations;
global using Atc.Rest.Api.Generator.Extractors;
global using Atc.Rest.Api.Generator.Helpers;
global using Atc.Rest.Api.Generator.Services;
global using Atc.Rest.Api.Generator.Validators;
global using Atc.Rest.Api.SourceGenerator.Extensions;
global using Atc.Rest.Api.SourceGenerator.Extractors;
global using Atc.Rest.Api.SourceGenerator.Helpers;

global using Microsoft.CodeAnalysis;
global using Microsoft.CodeAnalysis.CSharp;
global using Microsoft.CodeAnalysis.CSharp.Syntax;
global using Microsoft.CodeAnalysis.Text;
global using Microsoft.OpenApi;

// Type aliases for Atc.Rest.Api.Generator.Models to avoid DiagnosticSeverity ambiguity
global using EndpointInfo = Atc.Rest.Api.Generator.Models.EndpointInfo;
global using GeneratorDiagnosticMessage = Atc.Rest.Api.Generator.Models.DiagnosticMessage;
global using GeneratorDiagnosticSeverity = Atc.Rest.Api.Generator.Models.DiagnosticSeverity;
global using MultiPartConfiguration = Atc.Rest.Api.Generator.Models.MultiPartConfiguration;