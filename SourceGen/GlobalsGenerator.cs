using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Tyr.SourceGen;

[Generator]
public class GlobalsGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxReceiver is not SyntaxReceiver receiver)
            return;

        foreach (var classDecl in receiver.CandidateClasses)
        {
            var model = context.Compilation.GetSemanticModel(classDecl.SyntaxTree);
            var symbol = model.GetDeclaredSymbol(classDecl);
            if (symbol is null) continue;

            var attr = symbol.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.ToDisplayString() == typeof(GenerateGlobalsAttribute).FullName);

            if (attr == null) continue;

            var ns = symbol.ContainingNamespace.ToDisplayString();

            var nsParts = symbol.ContainingNamespace.ToDisplayString().Split('.');
            var moduleName = nsParts.LastOrDefault() ?? "Unknown";

            var code = $$"""
                         global using static Tyr.{{moduleName}}.Globals;
                         global using ZLogger;
                         using Microsoft.Extensions.Logging;
                         using Tyr.Common.Debug;
                         using Tyr.Common.Debug.Assertion;
                         using Tyr.Common.Debug.Drawing;

                         namespace {{ns}}
                         {
                             public static class Globals
                             {
                                 public static readonly ILogger Logger;
                                 public static readonly Assert Assert;
                                 public static readonly Drawer Drawer;
                         
                                 static Globals()
                                 {
                                     Logger = Log.GetLogger("{{moduleName}}");
                                     Assert = new Assert(Logger);
                                     Drawer = new Drawer("{{moduleName}}");
                                 }
                             }
                         }
                         """;
            context.AddSource($"Globals.g.cs", SourceText.From(code, Encoding.UTF8));
        }
    }

    private class SyntaxReceiver : ISyntaxReceiver
    {
        public List<ClassDeclarationSyntax> CandidateClasses { get; } = [];

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is ClassDeclarationSyntax { AttributeLists.Count: > 0 } cls)
            {
                CandidateClasses.Add(cls);
            }
        }
    }
}