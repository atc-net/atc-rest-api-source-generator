global using System;
global using System.Collections.Immutable;
global using System.Diagnostics.CodeAnalysis;
global using System.Globalization;
global using System.IO;
global using System.Linq;
global using System.Text;
global using System.Threading;

global using Atc.OpenApi.Helpers;
global using Atc.Rest.Api.Generator.Configurations;
global using Atc.Rest.Api.Generator.Extractors;
global using Atc.Rest.Api.Generator.Helpers;
global using Atc.Rest.Api.Generator.Validators;
global using Atc.Rest.Api.SourceGenerator.Helpers;

global using Microsoft.CodeAnalysis;
global using Microsoft.CodeAnalysis.CSharp;
global using Microsoft.CodeAnalysis.Text;
global using Microsoft.OpenApi;

global using Xunit;