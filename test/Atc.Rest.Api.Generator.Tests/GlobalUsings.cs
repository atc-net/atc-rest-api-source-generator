global using System;
global using System.Collections.Generic;
global using System.Diagnostics.CodeAnalysis;
global using System.IO;
global using System.Text;
global using System.Text.Json;

global using Atc.CodeGeneration.CSharp.Content;
global using Atc.CodeGeneration.CSharp.Helpers;
global using Atc.OpenApi.Extensions;
global using Atc.OpenApi.Helpers;
global using Atc.OpenApi.Models;
global using Atc.Rest.Api.Generator.Configurations;
global using Atc.Rest.Api.Generator.Extensions;
global using Atc.Rest.Api.Generator.Extractors;
global using Atc.Rest.Api.Generator.Helpers;
global using Atc.Rest.Api.Generator.Models;
global using Atc.Rest.Api.Generator.Services;
global using Atc.Rest.Api.Generator.Validators;

global using Microsoft.OpenApi;

global using Xunit;