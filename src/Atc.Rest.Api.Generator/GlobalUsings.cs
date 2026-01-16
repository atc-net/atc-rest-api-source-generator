global using System;
global using System.Collections.Generic;
global using System.Diagnostics.CodeAnalysis;
global using System.Globalization;
global using System.IO;
global using System.Linq;
global using System.Text;
global using System.Text.Json;
global using System.Text.Json.Nodes;
global using System.Text.Json.Serialization;

global using Atc.CodeGeneration.CSharp.CodeDocumentation.CodeComment;
global using Atc.CodeGeneration.CSharp.Content;
global using Atc.CodeGeneration.CSharp.Content.Generators;
global using Atc.CodeGeneration.CSharp.Helpers;
global using Atc.Helpers;
global using Atc.OpenApi.Extensions;
global using Atc.OpenApi.Helpers;
global using Atc.OpenApi.Models;
global using Atc.Rest.Api.Generator.Abstractions;
global using Atc.Rest.Api.Generator.Configurations;
global using Atc.Rest.Api.Generator.Extensions;
global using Atc.Rest.Api.Generator.Extractors;
global using Atc.Rest.Api.Generator.Helpers;
global using Atc.Rest.Api.Generator.JsonConverters;
global using Atc.Rest.Api.Generator.Models;

global using Microsoft.OpenApi;