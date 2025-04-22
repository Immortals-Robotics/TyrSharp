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
                         global using Tyr.Common.Extensions;
                         global using Timestamp = Tyr.Common.Time.Timestamp;
                         using System.Runtime.CompilerServices;
                         using System.Reflection;
                         using Microsoft.Extensions.Logging;

                         namespace {{ns}}
                         {
                             internal static class Globals
                             {
                                 internal static string ModuleName => "{{moduleName}}";
                             
                                 internal static ILogger Logger { get; private set; }
                                 internal static Common.Debug.Assertion.Assert Assert { get; private set; }
                                 internal static Common.Debug.Drawing.Drawer Drawer { get; private set; }
                                 internal static Random Rand { get; private set; }

                                 [ModuleInitializer]
                                 internal static void Init()
                                 {
                                     Logger = Common.Debug.Log.GetLogger("{{moduleName}}");
                                     Assert = new Common.Debug.Assertion.Assert(Logger);
                                     Drawer = new Common.Debug.Drawing.Drawer("{{moduleName}}");
                                     Rand = new Random();
                                     
                                     Common.Config.Registry.RegisterAssembly(Assembly.GetExecutingAssembly());
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