namespace Atc.Rest.Api.Generator.Tests.Extractors;

[SuppressMessage("", "SA1512:Single-line comments should not be followed by blank line", Justification = "OK")]
public class ModelsAndPropertiesClientTests
{
    // ========== List Operations (GET with Array Response) ==========

    [Fact]
    public void Extract_ListModels_GeneratesAsyncMethodWithListReturn()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /models:
                get:
                  operationId: listModels
                  responses:
                    '200':
                      description: Array of models
                      content:
                        application/json:
                          schema:
                            type: array
                            items:
                              $ref: '#/components/schemas/ComprehensiveModel'
            components:
              schemas:
                ComprehensiveModel:
                  type: object
                  properties:
                    id:
                      type: string
                      format: uuid
                    name:
                      type: string
            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var clientClass = HttpClientExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        Assert.NotNull(clientClass.Methods);

        var listModelsMethod = clientClass.Methods.FirstOrDefault(m => m.Name == "ListModelsAsync");
        Assert.NotNull(listModelsMethod);
        Assert.Contains("List<ComprehensiveModel>", listModelsMethod.ReturnTypeName, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_ListAddresses_GeneratesAsyncMethodWithListReturn()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /addresses:
                get:
                  operationId: listAddresses
                  responses:
                    '200':
                      description: Array of addresses
                      content:
                        application/json:
                          schema:
                            type: array
                            items:
                              $ref: '#/components/schemas/Address'
            components:
              schemas:
                Address:
                  type: object
                  properties:
                    streetName:
                      type: string
                    postalCode:
                      type: string
                    cityName:
                      type: string
            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var clientClass = HttpClientExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        Assert.NotNull(clientClass.Methods);

        var method = clientClass.Methods.FirstOrDefault(m => m.Name == "ListAddressesAsync");
        Assert.NotNull(method);
        Assert.Contains("List<Address>", method.ReturnTypeName, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_ListCountries_GeneratesAsyncMethodWithListReturn()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /countries:
                get:
                  operationId: listCountries
                  responses:
                    '200':
                      description: Array of countries
                      content:
                        application/json:
                          schema:
                            type: array
                            items:
                              $ref: '#/components/schemas/Country'
            components:
              schemas:
                Country:
                  type: object
                  properties:
                    name:
                      type: string
                    alpha2Code:
                      type: string
                    alpha3Code:
                      type: string
            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var clientClass = HttpClientExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        Assert.NotNull(clientClass.Methods);

        var method = clientClass.Methods.FirstOrDefault(m => m.Name == "ListCountriesAsync");
        Assert.NotNull(method);
        Assert.Contains("List<Country>", method.ReturnTypeName, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_ListPersons_GeneratesAsyncMethodWithQueryParameter()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /persons:
                get:
                  operationId: listPersons
                  parameters:
                    - name: gender
                      in: query
                      required: false
                      schema:
                        $ref: '#/components/schemas/GenderType'
                  responses:
                    '200':
                      description: Array of persons
                      content:
                        application/json:
                          schema:
                            type: array
                            items:
                              $ref: '#/components/schemas/Person'
            components:
              schemas:
                Person:
                  type: object
                  properties:
                    id:
                      type: string
                      format: uuid
                    firstName:
                      type: string
                    lastName:
                      type: string
                    email:
                      type: string
                      format: email
                GenderType:
                  type: string
                  enum:
                    - None
                    - NonBinary
                    - Male
                    - Female
            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var clientClass = HttpClientExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        Assert.NotNull(clientClass.Methods);

        var method = clientClass.Methods.FirstOrDefault(m => m.Name == "ListPersonsAsync");
        Assert.NotNull(method);
        Assert.Contains("List<Person>", method.ReturnTypeName, StringComparison.Ordinal);

        // Verify query parameter (wrapped in parameters class)
        Assert.NotNull(method.Parameters);
        var parametersParam = method.Parameters.FirstOrDefault(p => p.TypeName == "ListPersonsParameters");
        Assert.NotNull(parametersParam);
    }

    // ========== Create Operations (POST with Body) ==========

    [Fact]
    public void Extract_CreateModel_GeneratesAsyncMethodWithModelReturn()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /models:
                post:
                  operationId: createModel
                  requestBody:
                    required: true
                    content:
                      application/json:
                        schema:
                          $ref: '#/components/schemas/ComprehensiveModel'
                  responses:
                    '201':
                      description: Model created
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/ComprehensiveModel'
            components:
              schemas:
                ComprehensiveModel:
                  type: object
                  properties:
                    id:
                      type: string
                      format: uuid
                    name:
                      type: string
            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var clientClass = HttpClientExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        Assert.NotNull(clientClass.Methods);

        var method = clientClass.Methods.FirstOrDefault(m => m.Name == "CreateModelAsync");
        Assert.NotNull(method);
        Assert.Contains("ComprehensiveModel", method.ReturnTypeName, StringComparison.Ordinal);

        // Verify request body parameter (wrapped in parameters class)
        Assert.NotNull(method.Parameters);
        var parametersParam = method.Parameters.FirstOrDefault(p => p.TypeName == "CreateModelParameters");
        Assert.NotNull(parametersParam);
    }

    [Fact]
    public void Extract_CreateAddress_GeneratesAsyncMethodWithAddressReturn()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /addresses:
                post:
                  operationId: createAddress
                  requestBody:
                    required: true
                    content:
                      application/json:
                        schema:
                          $ref: '#/components/schemas/Address'
                  responses:
                    '201':
                      description: Address created
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/Address'
            components:
              schemas:
                Address:
                  type: object
                  properties:
                    streetName:
                      type: string
                    postalCode:
                      type: string
                    cityName:
                      type: string
            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var clientClass = HttpClientExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        Assert.NotNull(clientClass.Methods);

        var method = clientClass.Methods.FirstOrDefault(m => m.Name == "CreateAddressAsync");
        Assert.NotNull(method);
        Assert.Contains("Address", method.ReturnTypeName, StringComparison.Ordinal);

        // Verify request body parameter (wrapped in parameters class)
        Assert.NotNull(method.Parameters);
        var parametersParam = method.Parameters.FirstOrDefault(p => p.TypeName == "CreateAddressParameters");
        Assert.NotNull(parametersParam);
    }

    // ========== Get By Id (Path Parameter with UUID) ==========

    [Fact]
    public void Extract_GetModelById_GeneratesAsyncMethodWithGuidParameter()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /models/{modelId}:
                get:
                  operationId: getModelById
                  parameters:
                    - name: modelId
                      in: path
                      required: true
                      schema:
                        type: string
                        format: uuid
                  responses:
                    '200':
                      description: Model details
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/ComprehensiveModel'
            components:
              schemas:
                ComprehensiveModel:
                  type: object
                  properties:
                    id:
                      type: string
                      format: uuid
                    name:
                      type: string
            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var clientClass = HttpClientExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        Assert.NotNull(clientClass.Methods);

        var method = clientClass.Methods.FirstOrDefault(m => m.Name == "GetModelByIdAsync");
        Assert.NotNull(method);
        Assert.Contains("ComprehensiveModel", method.ReturnTypeName, StringComparison.Ordinal);

        // Verify path parameter (wrapped in parameters class)
        Assert.NotNull(method.Parameters);
        var parametersParam = method.Parameters.FirstOrDefault(p => p.TypeName == "GetModelByIdParameters");
        Assert.NotNull(parametersParam);
    }

    // ========== Single Object Response Tests ==========

    [Fact]
    public void Extract_GetPrimitiveTypes_GeneratesAsyncMethod()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /primitives:
                get:
                  operationId: getPrimitiveTypes
                  responses:
                    '200':
                      description: Primitive types
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/PrimitiveTypes'
            components:
              schemas:
                PrimitiveTypes:
                  type: object
                  properties:
                    stringValue:
                      type: string
                    integerValue:
                      type: integer
                    numberValue:
                      type: number
                    booleanValue:
                      type: boolean
            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var clientClass = HttpClientExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        Assert.NotNull(clientClass.Methods);

        var method = clientClass.Methods.FirstOrDefault(m => m.Name == "GetPrimitiveTypesAsync");
        Assert.NotNull(method);
        Assert.Contains("PrimitiveTypes", method.ReturnTypeName, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_GetStringFormats_GeneratesAsyncMethod()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /string-formats:
                get:
                  operationId: getStringFormats
                  responses:
                    '200':
                      description: String format types
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/StringFormats'
            components:
              schemas:
                StringFormats:
                  type: object
                  properties:
                    plainString:
                      type: string
                    uuidValue:
                      type: string
                      format: uuid
                    dateTimeValue:
                      type: string
                      format: date-time
            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var clientClass = HttpClientExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        Assert.NotNull(clientClass.Methods);

        var method = clientClass.Methods.FirstOrDefault(m => m.Name == "GetStringFormatsAsync");
        Assert.NotNull(method);
        Assert.Contains("StringFormats", method.ReturnTypeName, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_GetValidations_GeneratesAsyncMethod()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /validations:
                get:
                  operationId: getValidations
                  responses:
                    '200':
                      description: Validation constraints
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/ValidationConstraints'
            components:
              schemas:
                ValidationConstraints:
                  type: object
                  properties:
                    requiredString:
                      type: string
                    requiredInteger:
                      type: integer
            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var clientClass = HttpClientExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        Assert.NotNull(clientClass.Methods);

        var method = clientClass.Methods.FirstOrDefault(m => m.Name == "GetValidationsAsync");
        Assert.NotNull(method);
        Assert.Contains("ValidationConstraints", method.ReturnTypeName, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_GetDefaults_GeneratesAsyncMethod()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /defaults:
                get:
                  operationId: getDefaults
                  responses:
                    '200':
                      description: Default values
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/DefaultValues'
            components:
              schemas:
                DefaultValues:
                  type: object
                  properties:
                    stringDefault:
                      type: string
                      default: "default-value"
                    integerDefault:
                      type: integer
                      default: 42
                    booleanTrueDefault:
                      type: boolean
                      default: true
            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var clientClass = HttpClientExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        Assert.NotNull(clientClass.Methods);

        var method = clientClass.Methods.FirstOrDefault(m => m.Name == "GetDefaultsAsync");
        Assert.NotNull(method);
        Assert.Contains("DefaultValues", method.ReturnTypeName, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_GetNullables_GeneratesAsyncMethod()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /nullables:
                get:
                  operationId: getNullables
                  responses:
                    '200':
                      description: Nullable types
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/NullableTypes'
            components:
              schemas:
                NullableTypes:
                  type: object
                  properties:
                    requiredString:
                      type: string
                    nullableString:
                      type: string
                      nullable: true
                    nullableInteger:
                      type: integer
                      nullable: true
            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var clientClass = HttpClientExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        Assert.NotNull(clientClass.Methods);

        var method = clientClass.Methods.FirstOrDefault(m => m.Name == "GetNullablesAsync");
        Assert.NotNull(method);
        Assert.Contains("NullableTypes", method.ReturnTypeName, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_GetArrayTypes_GeneratesAsyncMethod()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /arrays:
                get:
                  operationId: getArrayTypes
                  responses:
                    '200':
                      description: Array types
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/ArrayTypes'
            components:
              schemas:
                ArrayTypes:
                  type: object
                  properties:
                    stringArray:
                      type: array
                      items:
                        type: string
                    integerArray:
                      type: array
                      items:
                        type: integer
            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var clientClass = HttpClientExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        Assert.NotNull(clientClass.Methods);

        var method = clientClass.Methods.FirstOrDefault(m => m.Name == "GetArrayTypesAsync");
        Assert.NotNull(method);
        Assert.Contains("ArrayTypes", method.ReturnTypeName, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_GetDictionaryTypes_GeneratesAsyncMethod()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /dictionaries:
                get:
                  operationId: getDictionaryTypes
                  responses:
                    '200':
                      description: Dictionary types
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/DictionaryTypes'
            components:
              schemas:
                DictionaryTypes:
                  type: object
                  properties:
                    stringDictionary:
                      type: object
                      additionalProperties:
                        type: string
                    integerDictionary:
                      type: object
                      additionalProperties:
                        type: integer
            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var clientClass = HttpClientExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        Assert.NotNull(clientClass.Methods);

        var method = clientClass.Methods.FirstOrDefault(m => m.Name == "GetDictionaryTypesAsync");
        Assert.NotNull(method);
        Assert.Contains("DictionaryTypes", method.ReturnTypeName, StringComparison.Ordinal);
    }

    [Fact]
    public void Extract_GetEnumTypes_GeneratesAsyncMethod()
    {
        // Arrange
        var yaml = """
            openapi: 3.0.0
            info:
              title: Test API
              version: 1.0.0
            paths:
              /enums:
                get:
                  operationId: getEnumTypes
                  responses:
                    '200':
                      description: Enum types
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/EnumTypes'
            components:
              schemas:
                EnumTypes:
                  type: object
                  properties:
                    genderType:
                      $ref: '#/components/schemas/GenderType'
                    statusType:
                      $ref: '#/components/schemas/StatusType'
                GenderType:
                  type: string
                  enum:
                    - None
                    - NonBinary
                    - Male
                    - Female
                StatusType:
                  type: string
                  enum:
                    - Draft
                    - Pending
                    - Active
                    - Inactive
                    - Archived
                    - Deleted
            """;

        var document = ParseYaml(yaml);
        Assert.NotNull(document);

        // Act
        var clientClass = HttpClientExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(clientClass);
        Assert.NotNull(clientClass.Methods);

        var method = clientClass.Methods.FirstOrDefault(m => m.Name == "GetEnumTypesAsync");
        Assert.NotNull(method);
        Assert.Contains("EnumTypes", method.ReturnTypeName, StringComparison.Ordinal);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}