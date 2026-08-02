using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace BetterWinUI.DependencyInjection.PageActivation.Generator;

/// <summary>
/// Describes the referenced WASDK XAML interfaces without retaining Roslyn symbols.
/// </summary>
internal readonly struct XamlContractModel : IEquatable<XamlContractModel>
{
    /// <summary>Initializes a XAML interface contract model.</summary>
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

    /// <summary>Gets the fully qualified metadata provider interface name.</summary>
    public string MetadataProviderInterfaceName { get; }

    /// <summary>Gets the fully qualified XAML type interface name.</summary>
    public string XamlTypeInterfaceName { get; }

    /// <summary>Gets the metadata provider interface members.</summary>
    public ImmutableArray<InterfaceMemberModel> MetadataProviderMembers { get; }

    /// <summary>Gets the XAML type interface members.</summary>
    public ImmutableArray<InterfaceMemberModel> XamlTypeMembers { get; }

    /// <summary>Gets a value indicating whether both XAML interfaces are available.</summary>
    public bool IsAvailable =>
        !string.IsNullOrEmpty(MetadataProviderInterfaceName) &&
        !string.IsNullOrEmpty(XamlTypeInterfaceName);

    /// <summary>Gets a value indicating whether all required interception methods are unique.</summary>
    public bool HasRequiredInterceptors =>
        MetadataProviderMembers.Count(static member => member.SpecialKind == XamlMemberSpecialKind.GetXamlTypeByType) ==
        1 &&
        MetadataProviderMembers.Count(static member => member.SpecialKind == XamlMemberSpecialKind.GetXamlTypeByName) ==
        1 &&
        XamlTypeMembers.Count(static member => member.SpecialKind == XamlMemberSpecialKind.ActivateInstance) == 1 &&
        XamlTypeMembers.Count(static member => member.IsUnderlyingType) == 1;

    /// <summary>
    /// Creates a value-only contract from the current compilation references.
    /// </summary>
    public static XamlContractModel Create(
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var metadataProvider =
            compilation.GetTypeByMetadataName(MetadataNames.XamlMetadataProvider);
        var xamlType =
            compilation.GetTypeByMetadataName(MetadataNames.XamlType);
        if (metadataProvider is null || xamlType is null) return default;

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
            var hashCode = MetadataProviderInterfaceName is null
                ? 0
                : StringComparer.Ordinal.GetHashCode(MetadataProviderInterfaceName);
            hashCode = (hashCode * HashCodeValues.Multiplier) ^
                       (XamlTypeInterfaceName is null
                           ? 0
                           : StringComparer.Ordinal.GetHashCode(XamlTypeInterfaceName));
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
    private static readonly PropertyInfo? AllowsRefLikeTypeProperty =
        typeof(ITypeParameterSymbol).GetProperty("AllowsRefLikeType");

    /// <summary>Initializes an interface member model.</summary>
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

    /// <summary>Gets Method, Property, or Event.</summary>
    public string Kind { get; }

    /// <summary>Gets a value indicating whether this member is a method.</summary>
    public bool IsMethod => Kind == "Method";

    /// <summary>Gets a value indicating whether this member is a property or indexer.</summary>
    public bool IsProperty => Kind == "Property";

    /// <summary>Gets a value indicating whether this member is an event.</summary>
    public bool IsEvent => Kind == "Event";

    /// <summary>Gets the declaration member name and any type or index parameters.</summary>
    public string DeclarationName { get; }

    /// <summary>Gets the invocation member name and any type or index arguments.</summary>
    public string InvocationName { get; }

    /// <summary>Gets the fully qualified return, property, or event type.</summary>
    public string TypeName { get; }

    /// <summary>Gets a ref return declaration prefix.</summary>
    public string DeclarationPrefix { get; }

    /// <summary>Gets a ref return expression prefix.</summary>
    public string ExpressionPrefix { get; }

    /// <summary>Gets the comma-separated method parameter declarations.</summary>
    public string Parameters { get; }

    /// <summary>Gets the comma-separated forwarding arguments.</summary>
    public string Arguments { get; }

    /// <summary>Gets generic constraint clauses required by implicit implementations.</summary>
    public string Constraints { get; }

    /// <summary>Gets a value indicating whether a property can be read.</summary>
    public bool CanRead { get; }

    /// <summary>Gets a value indicating whether a property can be written.</summary>
    public bool CanWrite { get; }

    /// <summary>Gets the interception semantics for this member.</summary>
    public XamlMemberSpecialKind SpecialKind { get; }

    /// <summary>Gets a value indicating whether this member intercepts Type lookup.</summary>
    public bool IsGetXamlTypeByType =>
        SpecialKind == XamlMemberSpecialKind.GetXamlTypeByType;

    /// <summary>Gets a value indicating whether this member intercepts name lookup.</summary>
    public bool IsGetXamlTypeByName =>
        SpecialKind == XamlMemberSpecialKind.GetXamlTypeByName;

    /// <summary>Gets a value indicating whether this member intercepts activation.</summary>
    public bool IsActivateInstance =>
        SpecialKind == XamlMemberSpecialKind.ActivateInstance;

    /// <summary>Gets a value indicating whether this is the required runtime type property.</summary>
    public bool IsUnderlyingType =>
        IsProperty &&
        DeclarationName == "UnderlyingType" &&
        TypeName == "global::System.Type" &&
        CanRead;

    /// <summary>Creates a value-only model from a referenced interface member.</summary>
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
        if (method.Name == "ActivateInstance" &&
            method.Parameters.IsDefaultOrEmpty &&
            method.RefKind == RefKind.None &&
            method.ReturnType.SpecialType == SpecialType.System_Object)
            return XamlMemberSpecialKind.ActivateInstance;

        if (method.Name != "GetXamlType" ||
            method.Parameters.Length != 1 ||
            method.Parameters[0].RefKind != RefKind.None ||
            method.RefKind != RefKind.None ||
            method.ReturnType.ToGlobalDisplayString() !=
            "global::Microsoft.UI.Xaml.Markup.IXamlType")
            return XamlMemberSpecialKind.None;

        var parameterType = method.Parameters[0].Type;
        if (parameterType.SpecialType == SpecialType.System_String) return XamlMemberSpecialKind.GetXamlTypeByName;

        return parameterType.ToGlobalDisplayString() == "global::System.Type"
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