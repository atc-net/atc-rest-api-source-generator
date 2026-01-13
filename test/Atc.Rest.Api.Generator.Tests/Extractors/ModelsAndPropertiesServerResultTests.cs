namespace Atc.Rest.Api.Generator.Tests.Extractors;

[SuppressMessage("", "SA1512:Single-line comments should not be followed by blank line", Justification = "OK")]
public class ModelsAndPropertiesServerResultTests
{
    // ========== List Operations (200 with Array Response) ==========

    [Fact]
    public void Extract_ListModels_GeneratesOkWithListOfComprehensiveModel()
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
        var resultClasses = ResultClassExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var listModelsResult = resultClasses.FirstOrDefault(r => r.ClassTypeName == "ListModelsResult");
        Assert.NotNull(listModelsResult);

        var okMethod = listModelsResult.Methods?.FirstOrDefault(m => m.Name == "Ok");
        Assert.NotNull(okMethod);
        Assert.NotNull(okMethod.Parameters);
        Assert.Single(okMethod.Parameters);
        Assert.Equal("List<ComprehensiveModel>", okMethod.Parameters[0].TypeName);
        Assert.Equal("response", okMethod.Parameters[0].Name);
    }

    [Fact]
    public void Extract_ListAddresses_GeneratesOkWithListOfAddress()
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
        var resultClasses = ResultClassExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var result = resultClasses.FirstOrDefault(r => r.ClassTypeName == "ListAddressesResult");
        Assert.NotNull(result);

        var okMethod = result.Methods?.FirstOrDefault(m => m.Name == "Ok");
        Assert.NotNull(okMethod);
        Assert.NotNull(okMethod.Parameters);
        Assert.Single(okMethod.Parameters);
        Assert.Equal("List<Address>", okMethod.Parameters[0].TypeName);
        Assert.Equal("response", okMethod.Parameters[0].Name);
    }

    [Fact]
    public void Extract_ListCountries_GeneratesOkWithListOfCountry()
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
        var resultClasses = ResultClassExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var result = resultClasses.FirstOrDefault(r => r.ClassTypeName == "ListCountriesResult");
        Assert.NotNull(result);

        var okMethod = result.Methods?.FirstOrDefault(m => m.Name == "Ok");
        Assert.NotNull(okMethod);
        Assert.NotNull(okMethod.Parameters);
        Assert.Single(okMethod.Parameters);
        Assert.Equal("List<Country>", okMethod.Parameters[0].TypeName);
        Assert.Equal("response", okMethod.Parameters[0].Name);
    }

    [Fact]
    public void Extract_ListPersons_GeneratesOkWithListOfPerson()
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
        var resultClasses = ResultClassExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var result = resultClasses.FirstOrDefault(r => r.ClassTypeName == "ListPersonsResult");
        Assert.NotNull(result);

        var okMethod = result.Methods?.FirstOrDefault(m => m.Name == "Ok");
        Assert.NotNull(okMethod);
        Assert.NotNull(okMethod.Parameters);
        Assert.Single(okMethod.Parameters);
        Assert.Equal("List<Person>", okMethod.Parameters[0].TypeName);
        Assert.Equal("response", okMethod.Parameters[0].Name);
    }

    // ========== Create Operations (201 Created, 400 BadRequest) ==========

    [Fact]
    public void Extract_CreateModel_GeneratesCreatedAndBadRequest()
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
                    '400':
                      description: Validation error
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
        var resultClasses = ResultClassExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var result = resultClasses.FirstOrDefault(r => r.ClassTypeName == "CreateModelResult");
        Assert.NotNull(result);

        // Verify Created method exists
        var createdMethod = result.Methods?.FirstOrDefault(m => m.Name == "Created");
        Assert.NotNull(createdMethod);

        // Verify BadRequest method exists
        var badRequestMethod = result.Methods?.FirstOrDefault(m => m.Name == "BadRequest");
        Assert.NotNull(badRequestMethod);
    }

    [Fact]
    public void Extract_CreateAddress_GeneratesCreated()
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
        var resultClasses = ResultClassExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var result = resultClasses.FirstOrDefault(r => r.ClassTypeName == "CreateAddressResult");
        Assert.NotNull(result);

        // Verify Created method exists
        var createdMethod = result.Methods?.FirstOrDefault(m => m.Name == "Created");
        Assert.NotNull(createdMethod);
    }

    // ========== Get By Id Operations (200 Ok, 404 NotFound) ==========

    [Fact]
    public void Extract_GetModelById_GeneratesOkAndNotFound()
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
                    '404':
                      description: Model not found
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
        var resultClasses = ResultClassExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var result = resultClasses.FirstOrDefault(r => r.ClassTypeName == "GetModelByIdResult");
        Assert.NotNull(result);

        // Verify Ok method with ComprehensiveModel response
        var okMethod = result.Methods?.FirstOrDefault(m => m.Name == "Ok");
        Assert.NotNull(okMethod);
        Assert.NotNull(okMethod.Parameters);
        Assert.Single(okMethod.Parameters);
        Assert.Equal("ComprehensiveModel", okMethod.Parameters[0].TypeName);
        Assert.Equal("response", okMethod.Parameters[0].Name);

        // Verify NotFound method with string? message = null
        var notFoundMethod = result.Methods?.FirstOrDefault(m => m.Name == "NotFound");
        Assert.NotNull(notFoundMethod);
        Assert.NotNull(notFoundMethod.Parameters);
        Assert.Single(notFoundMethod.Parameters);
        Assert.Equal("string", notFoundMethod.Parameters[0].TypeName);
        Assert.Equal("message", notFoundMethod.Parameters[0].Name);
        Assert.True(notFoundMethod.Parameters[0].IsNullableType);
        Assert.Equal("null", notFoundMethod.Parameters[0].DefaultValue);
    }

    // ========== Single Object Response Tests ==========

    [Fact]
    public void Extract_GetPrimitiveTypes_GeneratesOkWithPrimitiveTypes()
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
        var resultClasses = ResultClassExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var result = resultClasses.FirstOrDefault(r => r.ClassTypeName == "GetPrimitiveTypesResult");
        Assert.NotNull(result);

        var okMethod = result.Methods?.FirstOrDefault(m => m.Name == "Ok");
        Assert.NotNull(okMethod);
        Assert.NotNull(okMethod.Parameters);
        Assert.Single(okMethod.Parameters);
        Assert.Equal("PrimitiveTypes", okMethod.Parameters[0].TypeName);
        Assert.Equal("response", okMethod.Parameters[0].Name);
    }

    [Fact]
    public void Extract_GetStringFormats_GeneratesOkWithStringFormats()
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
        var resultClasses = ResultClassExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var result = resultClasses.FirstOrDefault(r => r.ClassTypeName == "GetStringFormatsResult");
        Assert.NotNull(result);

        var okMethod = result.Methods?.FirstOrDefault(m => m.Name == "Ok");
        Assert.NotNull(okMethod);
        Assert.NotNull(okMethod.Parameters);
        Assert.Single(okMethod.Parameters);
        Assert.Equal("StringFormats", okMethod.Parameters[0].TypeName);
        Assert.Equal("response", okMethod.Parameters[0].Name);
    }

    [Fact]
    public void Extract_GetValidations_GeneratesOkWithValidationConstraints()
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
        var resultClasses = ResultClassExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var result = resultClasses.FirstOrDefault(r => r.ClassTypeName == "GetValidationsResult");
        Assert.NotNull(result);

        var okMethod = result.Methods?.FirstOrDefault(m => m.Name == "Ok");
        Assert.NotNull(okMethod);
        Assert.NotNull(okMethod.Parameters);
        Assert.Single(okMethod.Parameters);
        Assert.Equal("ValidationConstraints", okMethod.Parameters[0].TypeName);
        Assert.Equal("response", okMethod.Parameters[0].Name);
    }

    [Fact]
    public void Extract_GetDefaults_GeneratesOkWithDefaultValues()
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
        var resultClasses = ResultClassExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var result = resultClasses.FirstOrDefault(r => r.ClassTypeName == "GetDefaultsResult");
        Assert.NotNull(result);

        var okMethod = result.Methods?.FirstOrDefault(m => m.Name == "Ok");
        Assert.NotNull(okMethod);
        Assert.NotNull(okMethod.Parameters);
        Assert.Single(okMethod.Parameters);
        Assert.Equal("DefaultValues", okMethod.Parameters[0].TypeName);
        Assert.Equal("response", okMethod.Parameters[0].Name);
    }

    [Fact]
    public void Extract_GetNullables_GeneratesOkWithNullableTypes()
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
        var resultClasses = ResultClassExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var result = resultClasses.FirstOrDefault(r => r.ClassTypeName == "GetNullablesResult");
        Assert.NotNull(result);

        var okMethod = result.Methods?.FirstOrDefault(m => m.Name == "Ok");
        Assert.NotNull(okMethod);
        Assert.NotNull(okMethod.Parameters);
        Assert.Single(okMethod.Parameters);
        Assert.Equal("NullableTypes", okMethod.Parameters[0].TypeName);
        Assert.Equal("response", okMethod.Parameters[0].Name);
    }

    [Fact]
    public void Extract_GetArrayTypes_GeneratesOkWithArrayTypes()
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
        var resultClasses = ResultClassExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var result = resultClasses.FirstOrDefault(r => r.ClassTypeName == "GetArrayTypesResult");
        Assert.NotNull(result);

        var okMethod = result.Methods?.FirstOrDefault(m => m.Name == "Ok");
        Assert.NotNull(okMethod);
        Assert.NotNull(okMethod.Parameters);
        Assert.Single(okMethod.Parameters);
        Assert.Equal("ArrayTypes", okMethod.Parameters[0].TypeName);
        Assert.Equal("response", okMethod.Parameters[0].Name);
    }

    [Fact]
    public void Extract_GetDictionaryTypes_GeneratesOkWithDictionaryTypes()
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
        var resultClasses = ResultClassExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var result = resultClasses.FirstOrDefault(r => r.ClassTypeName == "GetDictionaryTypesResult");
        Assert.NotNull(result);

        var okMethod = result.Methods?.FirstOrDefault(m => m.Name == "Ok");
        Assert.NotNull(okMethod);
        Assert.NotNull(okMethod.Parameters);
        Assert.Single(okMethod.Parameters);
        Assert.Equal("DictionaryTypes", okMethod.Parameters[0].TypeName);
        Assert.Equal("response", okMethod.Parameters[0].Name);
    }

    [Fact]
    public void Extract_GetEnumTypes_GeneratesOkWithEnumTypes()
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
        var resultClasses = ResultClassExtractor.Extract(
            document!,
            "TestApi",
            registry: null,
            systemTypeResolver: new SystemTypeConflictResolver([]),
            includeDeprecated: false);

        // Assert
        Assert.NotNull(resultClasses);
        var result = resultClasses.FirstOrDefault(r => r.ClassTypeName == "GetEnumTypesResult");
        Assert.NotNull(result);

        var okMethod = result.Methods?.FirstOrDefault(m => m.Name == "Ok");
        Assert.NotNull(okMethod);
        Assert.NotNull(okMethod.Parameters);
        Assert.Single(okMethod.Parameters);
        Assert.Equal("EnumTypes", okMethod.Parameters[0].TypeName);
        Assert.Equal("response", okMethod.Parameters[0].Name);
    }

    private static OpenApiDocument? ParseYaml(string yaml)
        => OpenApiDocumentHelper.TryParseYaml(yaml, "test.yaml", out var document)
            ? document
            : null;
}