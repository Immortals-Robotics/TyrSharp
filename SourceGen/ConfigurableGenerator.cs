using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Tyr.SourceGen;

/// <summary>
/// For every <c>[Configurable]</c> type, implements its <c>[ConfigEntry]</c> partial properties with
/// change detection and emits a <c>Configurable</c> handle that describes the entries without reflection.
/// Registration with the config registry is emitted by <see cref="GlobalsGenerator"/> into the module initializer.
/// </summary>
[Generator]
public sealed class ConfigurableGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var configurables = context.SyntaxProvider.ForAttributeWithMetadataName(
            ConfigurableModel.ConfigurableAttributeName,
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (ctx, ct) => ConfigurableModel.Parse(ctx, ct));

        context.RegisterSourceOutput(configurables, static (spc, output) =>
        {
            foreach (var diagnostic in output.Diagnostics)
                spc.ReportDiagnostic(diagnostic.Create());

            spc.AddSource(output.HintName, SourceText.From(output.Source, Encoding.UTF8));
        });
    }
}
