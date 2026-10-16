namespace Atc.Rest.Api.Generator.Cli.Tests.Extractors.TypeScript;

public class TypeScriptWireNamesTests
{
    private const string SpecYaml = """
        openapi: 3.0.3
        info: { title: WireNames, version: 1.0.0 }
        paths:
          /people:
            get:
              operationId: listPeople
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema:
                        type: array
                        items: { $ref: '#/components/schemas/Person' }
          /people/{person_id}:
            get:
              operationId: getPerson
              parameters:
                - name: person_id
                  in: path
                  required: true
                  schema: { type: string }
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema: { $ref: '#/components/schemas/Person' }
                '404':
                  description: Not Found
                  content:
                    application/json:
                      schema: { $ref: '#/components/schemas/ApiProblem' }
            put:
              operationId: updatePerson
              parameters:
                - name: person_id
                  in: path
                  required: true
                  schema: { type: string }
              requestBody:
                content:
                  application/json:
                    schema: { $ref: '#/components/schemas/Person' }
              responses:
                '204':
                  description: Updated
          /plain:
            get:
              operationId: getPlain
              responses:
                '200':
                  description: OK
                  content:
                    application/json:
                      schema: { $ref: '#/components/schemas/Plain' }
        components:
          schemas:
            Person:
              type: object
              properties:
                user_name: { type: string }
                displayName: { type: string }
                home_address: { $ref: '#/components/schemas/Address' }
                tags:
                  type: array
                  items: { $ref: '#/components/schemas/Tag' }
                labels:
                  type: object
                  additionalProperties: { $ref: '#/components/schemas/Tag' }
            Address:
              type: object
              properties:
                street_name: { type: string }
            Tag:
              type: object
              properties:
                tag_name: { type: string }
            ApiProblem:
              type: object
              properties:
                error_code: { type: string }
            Plain:
              type: object
              properties:
                already: { type: string }
        """;

    [Fact]
    public void Constructor_NullDocument_Throws()
        => Assert.Throws<ArgumentNullException>(() => new TypeScriptWireNames(null!, TypeScriptNamingStrategy.CamelCase));

    [Fact]
    public void Constructor_OriginalStrategy_RenamesNothing()
    {
        var sut = new TypeScriptWireNames(OpenApiDocumentHelper.ParseYaml(SpecYaml), TypeScriptNamingStrategy.Original);

        Assert.False(sut.HasRenames);
        Assert.Null(sut.ShapeOfModel("Person"));
        Assert.Null(sut.GenerateFile(headerContent: null));
    }

    [Fact]
    public void ConvertDates_DeclaredDates_GetADateShape_AndAPlainStringNone()
    {
        const string yaml = """
            openapi: 3.0.1
            info:
              title: Dates
              version: 1.0.0
            paths: {}
            components:
              schemas:
                Event:
                  type: object
                  properties:
                    day:
                      type: string
                      format: date
                    note:
                      type: string
                    history:
                      type: array
                      items:
                        type: string
                        format: date-time
            """;
        var document = OpenApiDocumentHelper.ParseYaml(yaml);

        var sut = new TypeScriptWireNames(document, TypeScriptNamingStrategy.Original, convertDates: true);

        Assert.True(sut.HasRenames);
        Assert.Equal("'Event'", sut.ShapeOfModel("Event"));
        var file = sut.GenerateFile(headerContent: null)!;
        Assert.Contains("day: ['day', { date: 'date' }],", file, StringComparison.Ordinal);
        Assert.Contains("history: ['history', { array: { date: 'date-time' } }],", file, StringComparison.Ordinal);
        Assert.DoesNotContain("note:", file, StringComparison.Ordinal);
    }

    [Fact]
    public void ConvertDates_FormatDate_IsALocalCalendarDay()
    {
        // A format: date is a day, not an instant: 2026-10-06 is read as local October 6 (not UTC midnight, which
        // is October 5 west of UTC) and a Date is written from its local year, month and day (not its UTC day,
        // which is the day before east of UTC).
        var sut = new TypeScriptWireNames(OpenApiDocumentHelper.ParseYaml(SpecYaml), TypeScriptNamingStrategy.CamelCase, convertDates: true);

        var file = sut.GenerateFile(headerContent: null)!;

        Assert.Contains("const DATE_ONLY = /^(\\d{4})-(\\d{2})-(\\d{2})$/;", file, StringComparison.Ordinal);
        Assert.Contains("? new Date(Number(dateOnly[1]), Number(dateOnly[2]) - 1, Number(dateOnly[3]))", file, StringComparison.Ordinal);
        Assert.Contains("return `${value.getFullYear()}-${month}-${day}`;", file, StringComparison.Ordinal);
        Assert.DoesNotContain("toISOString().slice(0, 10)", file, StringComparison.Ordinal);
    }

    [Fact]
    public void ConvertDatesOff_OriginalStrategy_HasNoShapes()
    {
        var sut = new TypeScriptWireNames(OpenApiDocumentHelper.ParseYaml(SpecYaml), TypeScriptNamingStrategy.Original, convertDates: false);

        Assert.False(sut.HasRenames);
    }

    [Fact]
    public void ShapeOfModel_ModelWithRenamedProperties_IsTheModelName()
    {
        var sut = new TypeScriptWireNames(OpenApiDocumentHelper.ParseYaml(SpecYaml), TypeScriptNamingStrategy.CamelCase);

        Assert.True(sut.HasRenames);
        Assert.Equal("'Person'", sut.ShapeOfModel("Person"));
        Assert.Equal("'Address'", sut.ShapeOfModel("Address"));
    }

    [Fact]
    public void ShapeOfModel_ModelWhosePropertiesAlreadyFollowTheStrategy_IsNull()
    {
        var sut = new TypeScriptWireNames(OpenApiDocumentHelper.ParseYaml(SpecYaml), TypeScriptNamingStrategy.CamelCase);

        Assert.Null(sut.ShapeOfModel("Plain"));
        Assert.Null(sut.ShapeOfModel("DoesNotExist"));
    }

    [Fact]
    public void ShapeOf_ArrayAndDictionaryOfRenamedModel_WrapTheModelShape()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(SpecYaml);
        var sut = new TypeScriptWireNames(doc, TypeScriptNamingStrategy.CamelCase);
        var person = (OpenApiSchema)doc.Components!.Schemas!["Person"];

        Assert.Equal("{ array: 'Tag' }", sut.ShapeOf(person.Properties!["tags"]));
        Assert.Equal("{ record: 'Tag' }", sut.ShapeOf(person.Properties["labels"]));
        Assert.Null(sut.ShapeOf(person.Properties["displayName"]));
        Assert.Null(sut.ShapeOf(schema: null));
    }

    [Fact]
    public void OperationOption_BodyResponseAndErrors_AreAllListed()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(SpecYaml);
        var sut = new TypeScriptWireNames(doc, TypeScriptNamingStrategy.CamelCase);
        var operations = doc.Paths["/people/{person_id}"].Operations!;

        var get = sut.OperationOption(operations[HttpMethod.Get], isStreaming: false, isFileUpload: false, isDownload: false);
        var put = sut.OperationOption(operations[HttpMethod.Put], isStreaming: false, isFileUpload: false, isDownload: false);

        Assert.Equal("{ response: 'Person', errors: { '404': 'ApiProblem' } }", get);
        Assert.Equal("{ body: 'Person' }", put);
    }

    [Fact]
    public void OperationOption_ListResponse_IsAnArrayShape()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(SpecYaml);
        var sut = new TypeScriptWireNames(doc, TypeScriptNamingStrategy.CamelCase);
        var list = doc.Paths["/people"].Operations![HttpMethod.Get];

        var option = sut.OperationOption(list, isStreaming: false, isFileUpload: false, isDownload: false);
        var stream = sut.OperationOption(list, isStreaming: true, isFileUpload: false, isDownload: false);

        Assert.Equal("{ response: { array: 'Person' } }", option);
        Assert.Equal("{ response: 'Person' }", stream);
    }

    [Fact]
    public void OperationOption_NothingToRename_IsNull()
    {
        var doc = OpenApiDocumentHelper.ParseYaml(SpecYaml);
        var sut = new TypeScriptWireNames(doc, TypeScriptNamingStrategy.CamelCase);
        var plain = doc.Paths["/plain"].Operations![HttpMethod.Get];
        var download = doc.Paths["/people"].Operations![HttpMethod.Get];

        Assert.Null(sut.OperationOption(plain, isStreaming: false, isFileUpload: false, isDownload: false));
        Assert.Null(sut.OperationOption(download, isStreaming: false, isFileUpload: false, isDownload: true));
    }

    [Fact]
    public void GenerateFile_ListsEveryRenamedModelWithItsWireAndTypeScriptNames()
    {
        var sut = new TypeScriptWireNames(OpenApiDocumentHelper.ParseYaml(SpecYaml), TypeScriptNamingStrategy.CamelCase);

        var file = sut.GenerateFile("// header\n");

        Assert.NotNull(file);
        Assert.StartsWith("// header\n", file, StringComparison.Ordinal);
        Assert.Contains("export type WireShape =", file, StringComparison.Ordinal);
        Assert.Contains("const wireModels: Readonly<Record<string, WireProperties>> = {", file, StringComparison.Ordinal);
        Assert.Contains("    user_name: ['userName'],", file, StringComparison.Ordinal);
        Assert.Contains("    home_address: ['homeAddress', 'Address'],", file, StringComparison.Ordinal);
        Assert.Contains("    tags: ['tags', { array: 'Tag' }],", file, StringComparison.Ordinal);
        Assert.Contains("    labels: ['labels', { record: 'Tag' }],", file, StringComparison.Ordinal);
        Assert.Contains("    error_code: ['errorCode'],", file, StringComparison.Ordinal);
        Assert.DoesNotContain("displayName: [", file, StringComparison.Ordinal);
        Assert.DoesNotContain("  Plain: {", file, StringComparison.Ordinal);
    }

    /// <summary>
    /// A union with a <c>discriminator</c> names the member to convert with by the discriminator's value - the
    /// mapping key, or the member's schema name when the mapping leaves it out - so two members that share their
    /// top-level keys (and differ inside a nested model) are told apart; "the member with the most keys" is only the
    /// fallback for a union without one. A member with nothing to rename maps to <c>null</c>: its value is left as
    /// it is, not converted as another member.
    /// </summary>
    [Fact]
    public void ShapeOf_DiscriminatedUnion_NamesTheMemberByTheDiscriminatorValue()
    {
        var sut = new TypeScriptWireNames(
            OpenApiDocumentHelper.ParseYaml("""
                openapi: 3.0.3
                info: { title: WireNames, version: 1.0.0 }
                paths:
                  /payments:
                    get:
                      operationId: listPayments
                      responses:
                        '200':
                          description: OK
                          content:
                            application/json:
                              schema:
                                type: array
                                items: { $ref: '#/components/schemas/Payment' }
                components:
                  schemas:
                    Payment:
                      oneOf:
                        - $ref: '#/components/schemas/Card'
                        - $ref: '#/components/schemas/Bank'
                        - $ref: '#/components/schemas/Cash'
                      discriminator:
                        propertyName: payment_type
                        mapping:
                          card: '#/components/schemas/Card'
                          cash: '#/components/schemas/Cash'
                    Card:
                      type: object
                      properties:
                        payment_type: { type: string }
                        holder_name: { type: string }
                        details: { $ref: '#/components/schemas/CardDetails' }
                    Bank:
                      type: object
                      properties:
                        payment_type: { type: string }
                        holder_name: { type: string }
                        details: { $ref: '#/components/schemas/BankDetails' }
                    Cash:
                      type: object
                      properties:
                        paymentType: { type: string }
                        amount: { type: number }
                    CardDetails:
                      type: object
                      properties:
                        card_number: { type: string }
                    BankDetails:
                      type: object
                      properties:
                        iban_number: { type: string }
                """),
            TypeScriptNamingStrategy.CamelCase);

        Assert.Equal(
            "{ union: ['Card', 'Bank'], discriminator: { wire: 'payment_type', ts: 'paymentType', mapping: { card: 'Card', Bank: 'Bank', cash: null } } }",
            sut.ShapeOfModel("Payment"));

        var file = sut.GenerateFile(headerContent: null);
        Assert.NotNull(file);
        Assert.Contains("export interface WireDiscriminator {", file, StringComparison.Ordinal);
        Assert.Contains("const kind = value[toWire ? discriminator.ts : discriminator.wire];", file, StringComparison.Ordinal);
    }

    [Fact]
    public void ShapeOf_UnionWithoutADiscriminator_HasNoDiscriminator()
    {
        var sut = new TypeScriptWireNames(OpenApiDocumentHelper.ParseYaml(SpecYaml), TypeScriptNamingStrategy.CamelCase);

        var file = sut.GenerateFile(headerContent: null);

        Assert.NotNull(file);
        Assert.DoesNotContain("discriminator: {", file, StringComparison.Ordinal);
    }
}