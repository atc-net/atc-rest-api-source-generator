namespace Atc.Rest.Api.Generator.Cli.Extractors.TypeScript;

/// <summary>
/// Generates the React context provider (ApiProvider.ts) and
/// the consumer hook (useApiService.ts) for TanStack Query integration.
/// </summary>
public static class TypeScriptApiProviderExtractor
{
    /// <summary>
    /// Generates the content for hooks/ApiProvider.ts.
    /// Uses React.createElement instead of JSX so no build step is required.
    /// </summary>
    public static string GenerateApiProvider(string? headerContent)
    {
        var sb = new StringBuilder();

        if (headerContent != null)
        {
            sb.Append(headerContent);
        }

        sb.AppendLine("import { createContext, useMemo, createElement } from 'react';");
        sb.AppendLine("import type { ReactNode } from 'react';");
        sb.AppendLine("import { ApiService } from '../client/ApiService';");
        sb.AppendLine("import type { ApiClientOptions } from '../client/ApiClient';");
        sb.AppendLine();

        // ApiProviderProps interface
        sb.AppendLine("export interface ApiProviderProps {");
        sb.AppendLine("  baseUrl: string;");
        sb.AppendLine("  options?: ApiClientOptions;");
        sb.AppendLine("  children: ReactNode;");
        sb.AppendLine("}");
        sb.AppendLine();

        // Context
        sb.AppendLine("export const ApiServiceContext = createContext<ApiService | null>(null);");
        sb.AppendLine();

        // Provider component
        sb.AppendLine("export function ApiProvider({ baseUrl, options, children }: ApiProviderProps) {");
        sb.AppendLine("  const service = useMemo(() => new ApiService(baseUrl, options), [baseUrl, options]);");
        sb.AppendLine("  return createElement(ApiServiceContext.Provider, { value: service }, children);");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates the content for hooks/useApiService.ts.
    /// </summary>
    public static string GenerateUseApiService(string? headerContent)
    {
        var sb = new StringBuilder();

        if (headerContent != null)
        {
            sb.Append(headerContent);
        }

        sb.AppendLine("import { useContext } from 'react';");
        sb.AppendLine("import { ApiServiceContext } from './ApiProvider';");
        sb.AppendLine("import type { ApiService } from '../client/ApiService';");
        sb.AppendLine();

        sb.AppendLine("export function useApiService(): ApiService {");
        sb.AppendLine("  const service = useContext(ApiServiceContext);");
        sb.AppendLine("  if (!service) {");
        sb.AppendLine("    throw new Error('useApiService must be used within an <ApiProvider>');");
        sb.AppendLine("  }");
        sb.AppendLine("  return service;");
        sb.AppendLine("}");

        return sb.ToString();
    }
}