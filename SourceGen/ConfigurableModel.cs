using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Tyr.SourceGen;

/// <summary>
/// A diagnostic captured during the incremental transform step, reported later during source output.
/// </summary>
internal sealed class DiagnosticInfo(DiagnosticDescriptor descriptor, Location? location, params object[] args)
    : IEquatable<DiagnosticInfo>
{
    private readonly DiagnosticDescriptor _descriptor = descriptor;
    private readonly Location? _location = location;
    private readonly object[] _args = args;

    public Diagnostic Create() => Diagnostic.Create(_descriptor, _location, _args);

    public bool Equals(DiagnosticInfo? other) =>
        other is not null &&
        _descriptor.Id == other._descriptor.Id &&
        Equals(_location, other._location) &&
        string.Join("", _args) == string.Join("", other._args);

    public override bool Equals(object? obj) => Equals(obj as DiagnosticInfo);
    public override int GetHashCode() => _descriptor.Id.GetHashCode();
}

/// <summary>
/// The generated output for one <c>[Configurable]</c> type. Only strings take part in equality so the
/// incremental pipeline can cache it.
/// </summary>
internal sealed class ConfigurableOutput(
    string hintName,
    string source,
    string fullName,
    IReadOnlyList<DiagnosticInfo> diagnostics) : IEquatable<ConfigurableOutput>
{
    public string HintName { get; } = hintName;
    public string Source { get; } = source;

    /// <summary>Fully qualified type name, used by the module initializer to register the type.</summary>
    public string FullName { get; } = fullName;

    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; } = diagnostics;

    public bool Equals(ConfigurableOutput? other)
    {
        if (other is null) return false;
        if (HintName != other.HintName || Source != other.Source || FullName != other.FullName) return false;
        if (Diagnostics.Count != other.Diagnostics.Count) return false;
        for (var i = 0; i < Diagnostics.Count; i++)
        {
            if (!Diagnostics[i].Equals(other.Diagnostics[i])) return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as ConfigurableOutput);
    public override int GetHashCode() => Source.GetHashCode();
}

/// <summary>
/// Reads a <c>[Configurable]</c> type and its <c>[ConfigEntry]</c> partial properties from the semantic
/// model and produces the implementing partial: property setters with change detection, and a
/// <c>Configurable</c> handle describing every entry without reflection.
/// </summary>
internal static class ConfigurableModel
{
    public const string ConfigurableAttributeName = "Tyr.Common.Config.ConfigurableAttribute";
    public const string ConfigEntryAttributeName = "Tyr.Common.Config.ConfigEntryAttribute";

    private const string ConfigNs = "global::Tyr.Common.Config";

    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public static readonly DiagnosticDescriptor TypeMustBePartial = new(
        "TYR001",
        "Configurable type must be partial",
        "Type '{0}' is marked [Configurable] and must be declared partial",
        "Tyr.Config",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EntryMustBeStaticPartialProperty = new(
        "TYR002",
        "Config entry must be a static partial property with get and set",
        "Property '{0}' is marked [ConfigEntry] and must be declared 'static partial' with both get and set accessors",
        "Tyr.Config",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TypeNotSupported = new(
        "TYR003",
        "Configurable type shape not supported",
        "Type '{0}' cannot be [Configurable]: {1}",
        "Tyr.Config",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static string FullName(INamedTypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    public static ConfigurableOutput Parse(GeneratorAttributeSyntaxContext context, System.Threading.CancellationToken ct)
    {
        var type = (INamedTypeSymbol)context.TargetSymbol;
        var syntax = (TypeDeclarationSyntax)context.TargetNode;
        var diagnostics = new List<DiagnosticInfo>();
        var fullName = FullName(type);
        var hintName = $"{fullName.Replace("global::", "")}.Configurable.g.cs";

        if (!syntax.Modifiers.Any(SyntaxKind.PartialKeyword))
            diagnostics.Add(new DiagnosticInfo(TypeMustBePartial, syntax.Identifier.GetLocation(), type.Name));

        if (type.ContainingType is not null)
            diagnostics.Add(new DiagnosticInfo(TypeNotSupported, syntax.Identifier.GetLocation(), type.Name, "nested types are not supported"));

        if (type.IsGenericType)
            diagnostics.Add(new DiagnosticInfo(TypeNotSupported, syntax.Identifier.GetLocation(), type.Name, "generic types are not supported"));

        var description = context.Attributes.Length > 0 && context.Attributes[0].ConstructorArguments.Length > 0
            ? context.Attributes[0].ConstructorArguments[0].Value as string
            : null;

        var typeKeyword = type.IsRecord
            ? (type.TypeKind == TypeKind.Struct ? "record struct" : "record")
            : (type.TypeKind == TypeKind.Struct ? "struct" : "class");

        var ns = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();

        var properties = new StringBuilder();
        var entries = new StringBuilder();

        foreach (var member in type.GetMembers())
        {
            ct.ThrowIfCancellationRequested();

            if (member is not IPropertySymbol property) continue;

            AttributeData? entryAttribute = null;
            foreach (var attribute in property.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() == ConfigEntryAttributeName)
                {
                    entryAttribute = attribute;
                    break;
                }
            }

            if (entryAttribute is null) continue;

            var isValid = property.IsStatic &&
                          property.IsPartialDefinition &&
                          property.GetMethod is not null &&
                          property.SetMethod is { IsInitOnly: false };

            if (!isValid)
            {
                diagnostics.Add(new DiagnosticInfo(EntryMustBeStaticPartialProperty,
                    property.Locations.Length > 0 ? property.Locations[0] : null, property.Name));
                continue;
            }

            ReadEntryAttribute(entryAttribute, out var entryDescription, out var storageType, out var editable);

            var typeName = property.Type.ToDisplayString(TypeFormat);
            var accessibility = AccessibilityKeyword(property.DeclaredAccessibility);
            var getAccessibility = AccessorPrefix(property.GetMethod!, property);
            var setAccessibility = AccessorPrefix(property.SetMethod!, property);
            var storageTypeExpr = $"({ConfigNs}.StorageType){storageType}";

            properties.AppendLine();
            properties.AppendLine($"    {accessibility} static partial {typeName} {property.Name}");
            properties.AppendLine("    {");
            properties.AppendLine($"        {getAccessibility}get => field;");
            properties.AppendLine($"        {setAccessibility}set");
            properties.AppendLine("        {");
            properties.AppendLine($"            if (global::System.Collections.Generic.EqualityComparer<{typeName}>.Default.Equals(field, value)) return;");
            properties.AppendLine("            field = value;");
            properties.AppendLine($"            __configurable?.MarkChanged({storageTypeExpr});");
            properties.AppendLine("        }");
            properties.AppendLine("    }");

            entries.AppendLine($"            new {ConfigNs}.ConfigEntry<{typeName}>(");
            entries.AppendLine($"                {Literal(property.Name)},");
            entries.AppendLine($"                {storageTypeExpr},");
            entries.AppendLine($"                editable: {(editable ? "true" : "false")},");
            entries.AppendLine($"                description: {Literal(entryDescription)},");
            entries.AppendLine($"                defaultValue: {property.Name},");
            entries.AppendLine($"                getter: static () => {property.Name},");
            entries.AppendLine($"                setter: static value => {property.Name} = value),");
        }

        var body = new StringBuilder();
        body.AppendLine($"partial {typeKeyword} {type.Name}");
        body.AppendLine("{");
        body.Append(properties);
        body.AppendLine();
        body.AppendLine($"    private static {ConfigNs}.Configurable? __configurable;");
        body.AppendLine();
        body.AppendLine("    /// <summary>Runtime handle for this type's config entries: change events, version counter and TOML conversion.</summary>");
        body.AppendLine($"    internal static {ConfigNs}.Configurable Configurable => __configurable ??= new {ConfigNs}.Configurable(");
        body.AppendLine($"        typeof({type.Name}),");
        body.AppendLine($"        {Literal(description)},");
        body.AppendLine($"        new {ConfigNs}.ConfigEntry[]");
        body.AppendLine("        {");
        body.Append(entries);
        body.AppendLine("        });");
        body.Append("}");

        var source = new StringBuilder();
        source.AppendLine("// <auto-generated/>");
        source.AppendLine("#nullable enable");
        source.AppendLine("#pragma warning disable");
        source.AppendLine();

        if (ns is null)
        {
            source.Append(body);
        }
        else
        {
            source.AppendLine($"namespace {ns}");
            source.AppendLine("{");
            source.Append(Indent(body.ToString(), "    "));
            source.AppendLine();
            source.AppendLine("}");
        }

        return new ConfigurableOutput(hintName, source.ToString(), fullName, diagnostics);
    }

    private static void ReadEntryAttribute(AttributeData attribute, out string? description, out int storageType, out bool editable)
    {
        description = null;
        storageType = 0;
        editable = true;

        var parameters = attribute.AttributeConstructor?.Parameters;
        if (parameters is not null)
        {
            for (var i = 0; i < attribute.ConstructorArguments.Length && i < parameters.Value.Length; i++)
            {
                var argument = attribute.ConstructorArguments[i];
                if (argument.Kind == TypedConstantKind.Error) continue;

                switch (parameters.Value[i].Name)
                {
                    case "description":
                        description = argument.Value as string;
                        break;
                    case "storageType":
                        storageType = Convert.ToInt32(argument.Value);
                        break;
                    case "editable":
                        editable = argument.Value is true;
                        break;
                }
            }
        }

        foreach (var named in attribute.NamedArguments)
        {
            if (named.Key == "Editable" && named.Value.Value is bool b)
                editable = b;
        }
    }

    private static string AccessorPrefix(IMethodSymbol accessor, IPropertySymbol property) =>
        accessor.DeclaredAccessibility == property.DeclaredAccessibility
            ? ""
            : AccessibilityKeyword(accessor.DeclaredAccessibility) + " ";

    private static string AccessibilityKeyword(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public => "public",
        Accessibility.Internal => "internal",
        Accessibility.Protected => "protected",
        Accessibility.ProtectedOrInternal => "protected internal",
        Accessibility.ProtectedAndInternal => "private protected",
        _ => "private",
    };

    private static string Literal(string? value) => value is null ? "null" : SymbolDisplay.FormatLiteral(value, quote: true);

    private static string Indent(string text, string indent)
    {
        var lines = text.Split('\n');
        var sb = new StringBuilder();
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (line.Length > 0) sb.Append(indent);
            sb.Append(line);
            if (i < lines.Length - 1) sb.Append('\n');
        }

        return sb.ToString();
    }
}
