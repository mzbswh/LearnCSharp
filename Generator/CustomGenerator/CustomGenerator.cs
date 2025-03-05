using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

[Generator]
public class CustomGenerator : IIncrementalGenerator
{
    private record Model(string Namespace, string ClassName, string MethodName);
    private record FieldModel(string Namespace, string ClassName, string FieldName, string FieldType);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static postInitializationContext =>
        {
            // postInitializationContext.AddEmbeddedAttributeDefinition();
            postInitializationContext.AddSource("myGeneratedFile.cs", SourceText.From("""
                using System;
                using Microsoft.CodeAnalysis;

                namespace GeneratedNamespace
                {
                    // [Embedded]
                    [AttributeUsage(AttributeTargets.Method)]
                    internal sealed class GeneratedAttribute : Attribute
                    {
                    }
                }
                """, Encoding.UTF8));

            // postInitializationContext.AddSource("TestClass.Generated.cs", SourceText.From("""
            //     using System;

            //     public partial class UserClass
            //     {
            //         public partial void UserMethod()
            //         {
            //             Console.WriteLine("GeneratedMethod");
            //         }
            //     }
            //     """, Encoding.UTF8));
        });

        // generate class
        var classPipeline = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "GeneratedNamespace.GeneratedAttribute",
            predicate: static (syntaxNode, cancellationToken) => syntaxNode is BaseMethodDeclarationSyntax,
            transform: static (context, cancellationToken) =>
            {
                var containingClass = context.TargetSymbol.ContainingType;
                return new Model(
                    // Note: this is a simplified example. You will also need to handle the case where the type is in a global namespace, nested, etc.
                    Namespace: containingClass.ContainingNamespace?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)),
                    ClassName: containingClass.Name,
                    MethodName: context.TargetSymbol.Name);
            }
        );

        context.RegisterSourceOutput(classPipeline, static (context, model) =>
        {
            var sourceText = SourceText.From($$"""
                namespace {{model.Namespace}};
                public partial class {{model.ClassName}}
                {
                    public partial void {{model.MethodName}}()
                    {
                        Console.WriteLine("{{model.ClassName}}.{{model.MethodName}} Generated");
                    }
                }
                """, Encoding.UTF8);

            context.AddSource($"{model.ClassName}_{model.MethodName}.g.cs", sourceText);
        });

        // Generate Property
        var pp1 = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "GeneratedNamespace.AutoNotifyAttribute",
            predicate: static (syntaxNode, cancellationToken) => syntaxNode is FieldDeclarationSyntax,
            transform: static (context, cancellationToken) =>
            {
                var fieldDeclaration = (FieldDeclarationSyntax)context.TargetNode;
                var containingClass = context.TargetSymbol.ContainingType;
                var fieldName = fieldDeclaration.Declaration.Variables[0].Identifier.Text;
                return new FieldModel(
                    // Note: this is a simplified example. You will also need to handle the case where the type is in a global namespace, nested, etc.
                    Namespace: containingClass.ContainingNamespace?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)),
                    ClassName: containingClass.Name,
                    FieldName: fieldName,
                    FieldType: fieldDeclaration.Declaration.Type.ToString());
            }
        );

        var pp2 = pp1.Collect().Select(static (fields, _) =>
        {
            var grouped = fields.GroupBy(f => new { f.Namespace, f.ClassName })
                                .ToImmutableDictionary(g => g.Key, g => g.ToImmutableArray());
            return grouped;
        });

        context.RegisterSourceOutput(pp2, static (context, groupedFields) =>
        {
            foreach (var group in groupedFields)
            {
                var namespaceName = group.Key.Namespace;
                var className = group.Key.ClassName;
                var fields = group.Value;

                var propertyDef = string.Join("\n", fields.Select(f => $"public bool {f.FieldName} {{ get; set; }}"));

                var sourceText = SourceText.From($$"""
                    namespace {{namespaceName}}
                    {
                        public partial class {{className}}
                        {
                            {{propertyDef}}
                        }
                    }
                    """, Encoding.UTF8);

                context.AddSource($"{className}_AutoNotify.g.cs", sourceText);
            }
        });


        // generate text
        var pipeline = context.AdditionalTextsProvider
            .Where(static (text) => text.Path.EndsWith(".txt"))
            .Select(static (text, cancellationToken) =>
            {
                var name = Path.GetFileNameWithoutExtension(text.Path);
                var code = text.GetText(cancellationToken);
                return (name, code);
            });

        context.RegisterSourceOutput(pipeline,
            static (context, pair) =>
            {
                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(
                    id: "GEN001",
                    title: "Generator Information",
                    messageFormat: $"Processing file: {pair.name}",
                    category: "Generator",
                    DiagnosticSeverity.Info,
                    isEnabledByDefault: true), Location.None));

                if (pair.code != null)
                {
                    context.AddSource($"{pair.name}.g.cs", SourceText.From(pair.code.ToString(), Encoding.UTF8));
                }
            });

        // generate code

    }
}