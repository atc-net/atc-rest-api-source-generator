namespace Atc.Rest.Api.SourceGenerator.Tests.Generators;

/// <summary>
/// A length limit (<c>minLength</c>/<c>maxLength</c>, <c>minItems</c>/<c>maxItems</c>) is trim- and AOT-safe: the
/// constructors of <see cref="MinLengthAttribute"/> and <see cref="MaxLengthAttribute"/> require unreferenced code
/// (they count an unknown type through reflection), so the generated code uses attributes derived from them. A
/// derived attribute validates the same, with the same message, and ASP.NET Core's OpenAPI document still reads it
/// as the length limit it is.
/// </summary>
public sealed class LengthLimitAotTests
{
    private const string Yaml = """
        openapi: 3.0.3
        info:
          title: ErrorBodies
          version: 1.0.0
        paths:
          /notes:
            post:
              operationId: createNote
              parameters:
                - name: tag
                  in: query
                  schema: { type: string, minLength: 2, maxLength: 3 }
              requestBody:
                required: true
                content:
                  application/json:
                    schema:
                      $ref: '#/components/schemas/Note'
              responses:
                '201':
                  description: Created
                  content:
                    application/json:
                      schema:
                        $ref: '#/components/schemas/Note'
        components:
          schemas:
            Note:
              type: object
              required: [title, labels]
              properties:
                title: { type: string, minLength: 2, maxLength: 5 }
                labels: { type: array, minItems: 1, maxItems: 2, items: { type: string } }
        """;

    public static TheoryData<string> Generators
        => ["server", "typed-client", "operation-client"];

    [Theory]
    [MemberData(nameof(Generators))]
    public void ALengthLimit_IsAnAttributeDerivedFromMinLengthOrMaxLength_NotTheReflectionOne(
        string generator)
    {
        var assembly = Load(generator);

        var lengthAttributes = assembly
            .GetTypes()
            .SelectMany(t => t.GetProperties().Cast<MemberInfo>().Concat(t.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.Member)))
            .Distinct()
            .SelectMany(m => m.GetCustomAttributes(inherit: false))
            .Concat(assembly.GetTypes().SelectMany(t => t.GetConstructors()).SelectMany(c => c.GetParameters()).SelectMany(p => p.GetCustomAttributes(inherit: false)))
            .Where(a => a is MinLengthAttribute or MaxLengthAttribute)
            .ToList();

        Assert.NotEmpty(lengthAttributes);
        Assert.All(lengthAttributes, a => Assert.True(
            a.GetType() != typeof(MinLengthAttribute) && a.GetType() != typeof(MaxLengthAttribute),
            $"{generator}: {a.GetType().Name} is the attribute whose constructor requires unreferenced code"));
    }

    [Theory]
    [MemberData(nameof(Generators))]
    public void ALengthLimit_ValidatesWithTheMessageOfMinLengthAndMaxLength(
        string generator)
    {
        var note = AssemblyNote(Load(generator));

        Assert.Equal(
            [
                new MinLengthAttribute(2).FormatErrorMessage("Title"),
                new MinLengthAttribute(1).FormatErrorMessage("Labels"),
            ],
            Validate(note.Create("a", []), note.Type));
        Assert.Equal(
            [
                new MaxLengthAttribute(5).FormatErrorMessage("Title"),
                new MaxLengthAttribute(2).FormatErrorMessage("Labels"),
            ],
            Validate(note.Create("abcdef", ["x", "y", "z"]), note.Type));
        Assert.Empty(Validate(note.Create("abc", ["x"]), note.Type));

        static (Type Type, Func<string, List<string>, object> Create) AssemblyNote(
            Assembly assembly)
        {
            var type = assembly.GetTypes().Single(t => t.Name == "Note");
            return (type, (title, labels) =>
            {
                var constructor = type.GetConstructors().FirstOrDefault(c => c.GetParameters().Length == 2);
                if (constructor is not null)
                {
                    return constructor.Invoke([title, labels]);
                }

                var instance = Activator.CreateInstance(type)!;
                type.GetProperty("Title")!.SetValue(instance, title);
                type.GetProperty("Labels")!.SetValue(instance, labels);
                return instance;
            });
        }
    }

    private static Assembly Load(string generator)
        => CompilationVerificationHarness.EmitAndLoad(generator switch
        {
            "server" => ErrorResponseBodyRuntimeTests.Generate(new ApiServerGenerator(), ".atc-rest-api-server", "{}", includeMinimalApi: false, Yaml),
            "typed-client" => ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", """{ "generationMode": "TypedClient" }""", includeMinimalApi: false, Yaml),
            _ => ErrorResponseBodyRuntimeTests.Generate(new ApiClientGenerator(), ".atc-rest-api-client", ErrorResponseBodyRuntimeTests.ClientOperationMarker("ProblemDetails"), includeMinimalApi: false, Yaml),
        });

    // The messages DataAnnotations gives for the instance, in the order of the properties
    private static List<string> Validate(
        object instance,
        Type type)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);

        var order = type.GetProperties().Select(p => p.Name).ToList();
        return [.. results
            .OrderBy(r => order.IndexOf(r.MemberNames.First()))
            .Select(r => r.ErrorMessage!)];
    }
}