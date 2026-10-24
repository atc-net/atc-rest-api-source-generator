namespace Atc.Rest.Api.SourceGenerator;

/// <summary>
/// Reports a comparison of a generated extensible enum with one listed value (<c>ATC_API_CLT004</c>, off by default):
/// <c>team.Kind != TeamKind.Professional</c> compiles unchanged from a C# enum, and a value the specification adds
/// later is "not Professional" there for good - often the opposite of what the code means. The note asks for the
/// decision the compiler cannot show: what an unknown value means. A comparison in a condition that also reads the
/// value's <c>IsKnown</c> is decided, and not reported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExtensibleEnumComparisonAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        RuleIdentifiers.ExtensibleEnumComparedWithOneValue,
        "An extensible enum compared with one listed value",
        "'{0}' compares '{1}' with one listed value: a value the specification does not list yet is {2} here. Decide what an unknown value means - check IsKnown first, or switch on the value with a default.",
        RuleIdentifiers.Category,
        DiagnosticSeverity.Info,
        isEnabledByDefault: false,
        helpLinkUri: "https://github.com/atc-net/atc-rest-api-source-generator/wiki/Analyzer-Rules#atc_api_clt004");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeComparison, OperationKind.Binary);
    }

    private static void AnalyzeComparison(OperationAnalysisContext context)
    {
        var comparison = (IBinaryOperation)context.Operation;
        if (comparison.OperatorKind is not (BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals))
        {
            return;
        }

        // One side is a value of an extensible enum, the other one of its listed values (a static member of the type)
        var type = ComparedType(Unwrap(comparison.LeftOperand), Unwrap(comparison.RightOperand));
        if (type is null || IsDecided(comparison, type))
        {
            return;
        }

        var outcome = comparison.OperatorKind == BinaryOperatorKind.NotEquals
            ? "not equal to it (the condition is true)"
            : "equal to none of them (the condition is false)";
        context.ReportDiagnostic(Diagnostic.Create(Rule, comparison.Syntax.GetLocation(), comparison.Syntax.ToString(), type.Name, outcome));
    }

    // The extensible enum compared with one of its listed values, or null (two values, two listed values, no such type)
    private static ITypeSymbol? ComparedType(
        IOperation left,
        IOperation right)
    {
        if (KnownValueType(right) is { } rightType && !IsKnownValue(left, rightType))
        {
            return rightType;
        }

        if (KnownValueType(left) is { } leftType && !IsKnownValue(right, leftType))
        {
            return leftType;
        }

        return null;
    }

    // The extensible enum whose listed value the operand is (DeviceStatus.Online), or null
    private static ITypeSymbol? KnownValueType(IOperation operand)
        => operand is IMemberReferenceOperation { Member: { IsStatic: true } member } &&
           member.ContainingType is { } type &&
           MemberType(member) is { } memberType &&
           SymbolEqualityComparer.Default.Equals(memberType, type) &&
           ExtensibleEnumUsageAnalyzer.IsExtensibleEnum(type)
            ? type
            : null;

    private static bool IsKnownValue(
        IOperation operand,
        ITypeSymbol type)
        => KnownValueType(operand) is { } other && SymbolEqualityComparer.Default.Equals(other, type);

    private static ITypeSymbol? MemberType(ISymbol member)
        => member switch
        {
            IPropertySymbol property => property.Type,
            IFieldSymbol field => field.Type,
            _ => null,
        };

    // A comparison of a nullable value (status == DeviceStatus.Online with a DeviceStatus?) is lifted: look through
    // the conversion
    private static IOperation Unwrap(IOperation operand)
        => operand is IConversionOperation { IsImplicit: true } conversion
            ? Unwrap(conversion.Operand)
            : operand;

    // In a condition of && and || that reads IsKnown of the type, what an unknown value means is decided
    private static bool IsDecided(
        IOperation comparison,
        ITypeSymbol type)
    {
        var condition = comparison;
        while (condition.Parent is IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalAnd or BinaryOperatorKind.ConditionalOr } or IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } or IParenthesizedOperation)
        {
            condition = condition.Parent;
        }

        return condition.DescendantsAndSelf()
            .OfType<IPropertyReferenceOperation>()
            .Any(p => p.Property.Name == "IsKnown" && SymbolEqualityComparer.Default.Equals(p.Property.ContainingType, type));
    }
}