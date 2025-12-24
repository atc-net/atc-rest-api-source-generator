namespace Atc.Rest.Api.Generator.Helpers;

/// <summary>
/// Helper class for building common method parameter patterns.
/// </summary>
public static class MethodParameterBuilder
{
    /// <summary>
    /// Creates the standard parameters for a DI extension method (IServiceCollection services).
    /// </summary>
    /// <returns>A tuple containing the parameter list and documentation dictionary.</returns>
    public static (List<ParameterBaseParameters> Parameters, Dictionary<string, string> Documentation)
        BuildServiceCollectionExtensionParameters()
    {
        var parameters = new List<ParameterBaseParameters>
        {
            new(
                Attributes: null,
                GenericTypeName: null,
                IsGenericListType: false,
                TypeName: "this IServiceCollection",
                IsNullableType: false,
                IsReferenceType: true,
                Name: "services",
                DefaultValue: null),
        };

        var documentation = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "services", "The service collection." },
        };

        return (parameters, documentation);
    }

    /// <summary>
    /// Creates parameters for a WebApplication extension method.
    /// </summary>
    /// <returns>A tuple containing the parameter list and documentation dictionary.</returns>
    public static (List<ParameterBaseParameters> Parameters, Dictionary<string, string> Documentation)
        BuildWebApplicationExtensionParameters()
    {
        var parameters = new List<ParameterBaseParameters>
        {
            new(
                Attributes: null,
                GenericTypeName: null,
                IsGenericListType: false,
                TypeName: "this WebApplication",
                IsNullableType: false,
                IsReferenceType: true,
                Name: "app",
                DefaultValue: null),
        };

        var documentation = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "app", "The web application." },
        };

        return (parameters, documentation);
    }

    /// <summary>
    /// Creates parameters for an IEndpointRouteBuilder extension method.
    /// </summary>
    /// <returns>A tuple containing the parameter list and documentation dictionary.</returns>
    public static (List<ParameterBaseParameters> Parameters, Dictionary<string, string> Documentation)
        BuildEndpointRouteBuilderExtensionParameters()
    {
        var parameters = new List<ParameterBaseParameters>
        {
            new(
                Attributes: null,
                GenericTypeName: null,
                IsGenericListType: false,
                TypeName: "this IEndpointRouteBuilder",
                IsNullableType: false,
                IsReferenceType: true,
                Name: "endpoints",
                DefaultValue: null),
        };

        var documentation = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "endpoints", "The endpoint route builder." },
        };

        return (parameters, documentation);
    }

    /// <summary>
    /// Creates a single parameter definition.
    /// </summary>
    /// <param name="typeName">The C# type name.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="isNullable">Whether the parameter is nullable.</param>
    /// <param name="defaultValue">The default value, if any.</param>
    /// <param name="isExtensionParameter">Whether this is an extension method parameter (adds 'this').</param>
    /// <returns>The parameter definition.</returns>
    public static ParameterBaseParameters CreateParameter(
        string typeName,
        string name,
        bool isNullable = false,
        string? defaultValue = null,
        bool isExtensionParameter = false)
    {
        var actualTypeName = isExtensionParameter ? $"this {typeName}" : typeName;

        return new ParameterBaseParameters(
            Attributes: null,
            GenericTypeName: null,
            IsGenericListType: false,
            TypeName: actualTypeName,
            IsNullableType: isNullable,
            IsReferenceType: !CSharpTypeHelper.IsBasicValueType(typeName),
            Name: name,
            DefaultValue: defaultValue);
    }
}