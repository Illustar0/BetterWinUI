using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace BetterWinUI.PageActivation.Generator;

/// <summary>
/// Describes the referenced WASDK XAML interfaces without retaining Roslyn symbols.
/// </summary>
/// <remarks>
/// The generated adapter implements every member of these interfaces by forwarding to the native provider. Reading
/// the members from the referenced Windows App SDK, rather than hard-coding them, keeps the adapter compiling when a
/// later SDK version adds members.
/// </remarks>
internal readonly struct XamlContractModel : IEquatable<XamlContractModel>
{
    public XamlContractModel(
        string metadataProviderInterfaceName,
        string xamlTypeInterfaceName,
        ImmutableArray<InterfaceMemberModel> metadataProviderMembers,
        ImmutableArray<InterfaceMemberModel> xamlTypeMembers)
    {
        MetadataProviderInterfaceName = metadataProviderInterfaceName;
        XamlTypeInterfaceName = xamlTypeInterfaceName;
        MetadataProviderMembers = metadataProviderMembers;
        XamlTypeMembers = xamlTypeMembers;
    }

    public string MetadataProviderInterfaceName { get; }

    public string XamlTypeInterfaceName { get; }

    public ImmutableArray<InterfaceMemberModel> MetadataProviderMembers { get; }

    public ImmutableArray<InterfaceMemberModel> XamlTypeMembers { get; }

    /// <summary>Gets the contract of a compilation that does not reference both XAML interfaces.</summary>
    public static XamlContractModel Unavailable { get; } = new(
        string.Empty,
        string.Empty,
        ImmutableArray<InterfaceMemberModel>.Empty,
        ImmutableArray<InterfaceMemberModel>.Empty);

    public bool IsAvailable => MetadataProviderInterfaceName.Length > 0;

    /// <summary>
    /// Gets a value that indicates whether each member the adapter intercepts (both GetXamlType overloads,
    /// UnderlyingType, and ActivateInstance) occurs exactly once.
    /// </summary>
    public bool HasRequiredInterceptors =>
        MetadataProviderMembers.Count(static member => member.SpecialKind == XamlMemberSpecialKind.GetXamlTypeByType) ==
        1 &&
        MetadataProviderMembers.Count(static member => member.SpecialKind == XamlMemberSpecialKind.GetXamlTypeByName) ==
        1 &&
        XamlTypeMembers.Count(static member => member.SpecialKind == XamlMemberSpecialKind.ActivateInstance) == 1 &&
        XamlTypeMembers.Count(static member => member.IsUnderlyingType) == 1;

    public static XamlContractModel Create(
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var metadataProvider =
            compilation.GetTypeByMetadataName(MetadataNames.XamlMetadataProvider);
        var xamlType =
            compilation.GetTypeByMetadataName(MetadataNames.XamlType);
        if (metadataProvider is null || xamlType is null) return Unavailable;

        return new XamlContractModel(
            metadataProvider.ToGlobalDisplayString(),
            xamlType.ToGlobalDisplayString(),
            ReadMembers(metadataProvider, cancellationToken),
            ReadMembers(xamlType, cancellationToken));
    }

    /// <inheritdoc />
    public bool Equals(XamlContractModel other)
    {
        return string.Equals(
                   MetadataProviderInterfaceName,
                   other.MetadataProviderInterfaceName,
                   StringComparison.Ordinal) &&
               string.Equals(
                   XamlTypeInterfaceName,
                   other.XamlTypeInterfaceName,
                   StringComparison.Ordinal) &&
               MetadataProviderMembers.SequenceEqual(other.MetadataProviderMembers) &&
               XamlTypeMembers.SequenceEqual(other.XamlTypeMembers);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is XamlContractModel other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = StringComparer.Ordinal.GetHashCode(MetadataProviderInterfaceName);
            hashCode = (hashCode * HashCodeValues.Multiplier) ^
                       StringComparer.Ordinal.GetHashCode(XamlTypeInterfaceName);
            hashCode = (hashCode * HashCodeValues.Multiplier) ^
                       MetadataProviderMembers.Length;
            return (hashCode * HashCodeValues.Multiplier) ^ XamlTypeMembers.Length;
        }
    }

    private static ImmutableArray<InterfaceMemberModel> ReadMembers(
        INamedTypeSymbol interfaceSymbol,
        CancellationToken cancellationToken)
    {
        IEnumerable<ISymbol> symbols = interfaceSymbol.AllInterfaces
            .Reverse()
            .SelectMany(static inherited => inherited.GetMembers())
            .Concat(interfaceSymbol.GetMembers())
            .Where(static member =>
                !member.IsStatic &&
                member.DeclaredAccessibility == Accessibility.Public &&
                (member is IPropertySymbol or IEventSymbol ||
                 member is IMethodSymbol { MethodKind: MethodKind.Ordinary }))
            // A stable order keeps the generated source and the cached model identical across compilations.
            .OrderBy(static member => member.Kind)
            .ThenBy(static member => member.Name, StringComparer.Ordinal)
            .ThenBy(
                static member =>
                    member.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var members = ImmutableArray.CreateBuilder<InterfaceMemberModel>();

        foreach (var symbol in symbols)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // A member redeclared by several interfaces in the hierarchy is implemented once.
            var key = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
            if (seen.Add(key)) members.Add(InterfaceMemberModel.Create(symbol));
        }

        return members.ToImmutable();
    }
}

/// <summary>
/// Identifies members with generated interception semantics.
/// </summary>
internal enum XamlMemberSpecialKind : byte
{
    /// <summary>The member is forwarded normally.</summary>
    None,

    /// <summary>The metadata lookup accepts a runtime Type.</summary>
    GetXamlTypeByType,

    /// <summary>The metadata lookup accepts a full type name.</summary>
    GetXamlTypeByName,

    /// <summary>The XAML type member activates an instance.</summary>
    ActivateInstance
}

/// <summary>
/// Describes one interface member for the Scriban forwarding template.
/// </summary>
internal readonly struct InterfaceMemberModel : IEquatable<InterfaceMemberModel>
{
    // ITypeParameterSymbol.AllowsRefLikeType was added after Roslyn 4.8, which the generator compiles against so that
    // the .NET 8 SDK can load it. Newer host compilers expose the property and it is read through reflection.
    private static readonly PropertyInfo? AllowsRefLikeTypeProperty =
        typeof(ITypeParameterSymbol).GetProperty("AllowsRefLikeType");

    public InterfaceMemberModel(
        string kind,
        string declarationName,
        string invocationName,
        string typeName,
        string declarationPrefix,
        string expressionPrefix,
        string parameters,
        string arguments,
        string constraints,
        bool canRead,
        bool canWrite,
        XamlMemberSpecialKind specialKind)
    {
        Kind = kind;
        DeclarationName = declarationName;
        InvocationName = invocationName;
        TypeName = typeName;
        DeclarationPrefix = declarationPrefix;
        ExpressionPrefix = expressionPrefix;
        Parameters = parameters;
        Arguments = arguments;
        Constraints = constraints;
        CanRead = canRead;
        CanWrite = canWrite;
        SpecialKind = specialKind;
    }

    /// <summary>Gets <c>Method</c>, <c>Property</c>, or <c>Event</c>.</summary>
    public string Kind { get; }

    public bool IsMethod => string.Equals(Kind, "Method", StringComparison.Ordinal);

    /// <summary>Gets a value that indicates whether this member is a property or an indexer.</summary>
    public bool IsProperty => string.Equals(Kind, "Property", StringComparison.Ordinal);

    public bool IsEvent => string.Equals(Kind, "Event", StringComparison.Ordinal);

    /// <summary>Gets the declaration member name and any type or index parameters.</summary>
    public string DeclarationName { get; }

    /// <summary>Gets the invocation member name and any type or index arguments.</summary>
    public string InvocationName { get; }

    /// <summary>Gets the fully qualified return, property, or event type.</summary>
    public string TypeName { get; }

    /// <summary>Gets <c>ref </c> or <c>ref readonly </c> for a member that returns by reference; otherwise, an empty string.</summary>
    public string DeclarationPrefix { get; }

    /// <summary>Gets <c>ref </c> for a forwarding expression that returns by reference; otherwise, an empty string.</summary>
    public string ExpressionPrefix { get; }

    /// <summary>Gets the comma-separated parameter declarations.</summary>
    public string Parameters { get; }

    /// <summary>Gets the comma-separated arguments that forward the parameters.</summary>
    public string Arguments { get; }

    /// <summary>Gets the generic constraint clauses that an implicit implementation must repeat.</summary>
    public string Constraints { get; }

    public bool CanRead { get; }

    public bool CanWrite { get; }

    public XamlMemberSpecialKind SpecialKind { get; }

    public bool IsGetXamlTypeByType =>
        SpecialKind == XamlMemberSpecialKind.GetXamlTypeByType;

    public bool IsGetXamlTypeByName =>
        SpecialKind == XamlMemberSpecialKind.GetXamlTypeByName;

    public bool IsActivateInstance =>
        SpecialKind == XamlMemberSpecialKind.ActivateInstance;

    /// <summary>Gets a value that indicates whether this is the readable UnderlyingType property the adapter reads.</summary>
    public bool IsUnderlyingType =>
        IsProperty &&
        string.Equals(DeclarationName, "UnderlyingType", StringComparison.Ordinal) &&
        string.Equals(TypeName, "global::System.Type", StringComparison.Ordinal) &&
        CanRead;

    public static InterfaceMemberModel Create(ISymbol symbol)
    {
        return symbol switch
        {
            IMethodSymbol method => CreateMethod(method),
            IPropertySymbol property => CreateProperty(property),
            IEventSymbol @event => CreateEvent(@event),
            _ => throw new ArgumentOutOfRangeException(nameof(symbol))
        };
    }

    /// <inheritdoc />
    public bool Equals(InterfaceMemberModel other)
    {
        return string.Equals(Kind, other.Kind, StringComparison.Ordinal) &&
               string.Equals(DeclarationName, other.DeclarationName, StringComparison.Ordinal) &&
               string.Equals(InvocationName, other.InvocationName, StringComparison.Ordinal) &&
               string.Equals(TypeName, other.TypeName, StringComparison.Ordinal) &&
               string.Equals(DeclarationPrefix, other.DeclarationPrefix, StringComparison.Ordinal) &&
               string.Equals(ExpressionPrefix, other.ExpressionPrefix, StringComparison.Ordinal) &&
               string.Equals(Parameters, other.Parameters, StringComparison.Ordinal) &&
               string.Equals(Arguments, other.Arguments, StringComparison.Ordinal) &&
               string.Equals(Constraints, other.Constraints, StringComparison.Ordinal) &&
               CanRead == other.CanRead &&
               CanWrite == other.CanWrite &&
               SpecialKind == other.SpecialKind;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is InterfaceMemberModel other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = StringComparer.Ordinal.GetHashCode(Kind);
            hashCode = (hashCode * HashCodeValues.Multiplier) ^
                       StringComparer.Ordinal.GetHashCode(DeclarationName);
            hashCode = (hashCode * HashCodeValues.Multiplier) ^
                       StringComparer.Ordinal.GetHashCode(TypeName);
            hashCode = (hashCode * HashCodeValues.Multiplier) ^
                       StringComparer.Ordinal.GetHashCode(Parameters);
            return (hashCode * HashCodeValues.Multiplier) ^ (int)SpecialKind;
        }
    }

    private static InterfaceMemberModel CreateMethod(IMethodSymbol method)
    {
        var name = EscapeIdentifier(method.Name);
        var typeParameters = method.TypeParameters.IsDefaultOrEmpty
            ? string.Empty
            : $"<{string.Join(", ", method.TypeParameters.Select(
                static parameter => EscapeIdentifier(parameter.Name)))}>";
        return new InterfaceMemberModel(
            "Method",
            name + typeParameters,
            "." + name + typeParameters,
            method.ReturnsVoid ? "void" : method.ReturnType.ToGlobalDisplayString(),
            GetDeclarationPrefix(method.RefKind),
            GetExpressionPrefix(method.RefKind),
            string.Join(", ", method.Parameters.Select(FormatParameter)),
            string.Join(", ", method.Parameters.Select(FormatArgument)),
            FormatConstraints(method),
            false,
            false,
            GetSpecialKind(method));
    }

    private static InterfaceMemberModel CreateProperty(IPropertySymbol property)
    {
        var name = EscapeIdentifier(property.Name);
        var parameters = string.Join(", ", property.Parameters.Select(FormatParameter));
        var arguments = string.Join(", ", property.Parameters.Select(FormatArgument));
        return new InterfaceMemberModel(
            "Property",
            property.IsIndexer ? $"this[{parameters}]" : name,
            property.IsIndexer ? $"[{arguments}]" : "." + name,
            property.Type.ToGlobalDisplayString(),
            GetDeclarationPrefix(property.RefKind),
            GetExpressionPrefix(property.RefKind),
            parameters,
            arguments,
            string.Empty,
            property.GetMethod is not null,
            property.SetMethod is not null,
            XamlMemberSpecialKind.None);
    }

    private static InterfaceMemberModel CreateEvent(IEventSymbol @event)
    {
        var name = EscapeIdentifier(@event.Name);
        return new InterfaceMemberModel(
            "Event",
            name,
            "." + name,
            @event.Type.ToGlobalDisplayString(),
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            false,
            false,
            XamlMemberSpecialKind.None);
    }

    private static XamlMemberSpecialKind GetSpecialKind(IMethodSymbol method)
    {
        if (string.Equals(method.Name, "ActivateInstance", StringComparison.Ordinal) &&
            method.Parameters.IsDefaultOrEmpty &&
            method.RefKind == RefKind.None &&
            method.ReturnType.SpecialType == SpecialType.System_Object)
            return XamlMemberSpecialKind.ActivateInstance;

        if (!string.Equals(method.Name, "GetXamlType", StringComparison.Ordinal) ||
            method.Parameters.Length != 1 ||
            method.Parameters[0].RefKind != RefKind.None ||
            method.RefKind != RefKind.None ||
            !string.Equals(
                method.ReturnType.ToGlobalDisplayString(),
                "global::Microsoft.UI.Xaml.Markup.IXamlType",
                StringComparison.Ordinal))
            return XamlMemberSpecialKind.None;

        var parameterType = method.Parameters[0].Type;
        if (parameterType.SpecialType == SpecialType.System_String) return XamlMemberSpecialKind.GetXamlTypeByName;

        return string.Equals(
            parameterType.ToGlobalDisplayString(),
            "global::System.Type",
            StringComparison.Ordinal)
            ? XamlMemberSpecialKind.GetXamlTypeByType
            : XamlMemberSpecialKind.None;
    }

    private static string FormatParameter(IParameterSymbol parameter)
    {
        var modifier = parameter.RefKind switch
        {
            RefKind.Ref => "ref ",
            RefKind.Out => "out ",
            RefKind.In => "in ",
            RefKind.RefReadOnlyParameter => "ref readonly ",
            _ => parameter.IsParams
                ? "params "
                : string.Empty
        };
        return $"{modifier}{parameter.Type.ToGlobalDisplayString()} " +
               EscapeIdentifier(parameter.Name);
    }

    private static string FormatArgument(IParameterSymbol parameter)
    {
        var modifier = parameter.RefKind switch
        {
            RefKind.Ref => "ref ",
            RefKind.Out => "out ",
            RefKind.In => "in ",
            // A ref readonly parameter accepts an in argument at the call site.
            RefKind.RefReadOnlyParameter => "in ",
            _ => string.Empty
        };
        return modifier + EscapeIdentifier(parameter.Name);
    }

    private static string FormatConstraints(IMethodSymbol method)
    {
        return string.Join(
            " ",
            method.TypeParameters
                .Select(FormatConstraint)
                .Where(static constraint => constraint.Length > 0));
    }

    private static string FormatConstraint(ITypeParameterSymbol parameter)
    {
        var constraints = new List<string>();
        if (parameter.HasUnmanagedTypeConstraint)
            constraints.Add("unmanaged");
        else if (parameter.HasValueTypeConstraint)
            constraints.Add("struct");
        else if (parameter.HasReferenceTypeConstraint)
            constraints.Add(
                parameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated
                    ? "class?"
                    : "class");
        else if (parameter.HasNotNullConstraint) constraints.Add("notnull");

        constraints.AddRange(
            parameter.ConstraintTypes.Select(static type => type.ToGlobalDisplayString()));
        if (parameter.HasConstructorConstraint) constraints.Add("new()");

        if (AllowsRefLikeType(parameter)) constraints.Add("allows ref struct");

        return constraints.Count == 0
            ? string.Empty
            : $"where {EscapeIdentifier(parameter.Name)} : " +
              string.Join(", ", constraints);
    }

    private static bool AllowsRefLikeType(ITypeParameterSymbol parameter)
    {
        return AllowsRefLikeTypeProperty?.GetValue(parameter) is true;
    }

    private static string GetDeclarationPrefix(RefKind refKind)
    {
        return refKind switch
        {
            RefKind.Ref => "ref ",
            RefKind.RefReadOnly => "ref readonly ",
            _ => string.Empty
        };
    }

    private static string GetExpressionPrefix(RefKind refKind)
    {
        return refKind is RefKind.Ref or RefKind.RefReadOnly ? "ref " : string.Empty;
    }

    private static string EscapeIdentifier(string name)
    {
        return SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None ? name : $"@{name}";
    }
}