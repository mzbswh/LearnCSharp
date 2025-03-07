using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

[Generator]
public class CustomGenerator : IIncrementalGenerator
{
    private record Model(string Namespace, string ClassName, string MethodName);
    private record FieldModel(string Namespace, string ClassName, string FieldName, string FieldType, string PropertyName);

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
            fullyQualifiedMetadataName: "AutoNotify.AutoNotifyAttribute",
            predicate: static (syntaxNode, cancellationToken) => true,
            transform: static (context, cancellationToken) =>
            {
                var variableDeclarator = (VariableDeclaratorSyntax)context.TargetNode;
                var fieldName = variableDeclarator.Identifier.Text;
                var attribute = context.Attributes[0];
                var pName = attribute.NamedArguments.FirstOrDefault(a => a.Key == "PropertyName").Value.Value?.ToString();
                if (string.IsNullOrEmpty(pName))
                {
                    pName = fieldName;
                    // 将_varName/varName 转换为 VarName
                    // 去掉下划线
                    if (fieldName.StartsWith("_"))
                    {
                        pName = pName.Substring(1);
                    }
                    // 首字母大写
                    if (fieldName.Length > 0)
                    {
                        pName = char.ToUpper(pName[0]) + pName.Substring(1);
                    }
                }
                var containingClass = context.TargetSymbol.ContainingType;
                var declaration = variableDeclarator.Parent as VariableDeclarationSyntax;
                var fieldType = declaration?.Type.ToString();
                return new FieldModel(
                    Namespace: containingClass.ContainingNamespace?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)),
                    ClassName: containingClass.Name,
                    FieldName: fieldName,
                    FieldType: fieldType,
                        PropertyName: pName);
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
                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(
                    id: "GEN001",
                    title: "Generator Information",
                    messageFormat: $"Processing AutoNotify fields {groupedFields.Count}",
                    category: "Generator",
                    DiagnosticSeverity.Warning,
                    isEnabledByDefault: true), Location.None));

                foreach (var group in groupedFields)
                {
                    var namespaceName = group.Key.Namespace;
                    var className = group.Key.ClassName;
                    var fields = group.Value;

                    var propertyDef = string.Join("\n\n", fields.Select(f => $$"""
                            // fieldName= {{f.FieldName}}
                            // fieldType {{f.FieldType}}
                            // PropertyName= {{f.PropertyName}}
                            public {{f.FieldType}} {{f.PropertyName}}
                            {
                                get => {{f.FieldName}};
                                set
                                {
                                    {{f.FieldName}} = value;
                                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("{{f.PropertyName}}"));
                                }
                            }
                    """));

                    var sourceText = SourceText.From($$"""
                    using System.ComponentModel;

                    namespace {{namespaceName}}
                    {
                        public partial class {{className}} : INotifyPropertyChanged
                        {
                            public event PropertyChangedEventHandler PropertyChanged;

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

        // custom property and metadata
        var emitLoggingPipeline = context.AnalyzerConfigOptionsProvider.Select(static (options, cancellationToken) =>
            options.GlobalOptions.TryGetValue("emit_log", out var emitLoggingSwitch)
                ? emitLoggingSwitch.Equals("true", StringComparison.InvariantCultureIgnoreCase)
                : false); // Default

        context.RegisterSourceOutput(emitLoggingPipeline, static (context, emitLogging) =>
        {
            context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(
                id: "GEN001",
                title: "Generator Information",
                messageFormat: $"EmitLogging: {emitLogging}",
                category: "Generator",
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true), Location.None));
        });

        var property = context.AnalyzerConfigOptionsProvider.Select((provider, ct) =>
            provider.GlobalOptions.TryGetValue("build_property.TestVisibleProperty", out var emitLoggingSwitch)
                ? emitLoggingSwitch.Equals("true", StringComparison.InvariantCultureIgnoreCase) : false);
        context.RegisterSourceOutput(property, static (context, visibleProperty) =>
        {
            context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(
                id: "GEN001",
                title: "Generator Information",
                messageFormat: $"VisibleProperty: {visibleProperty}",
                category: "Generator",
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true), Location.None));
        });

        var metadata = context.AdditionalTextsProvider
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select((pair, ctx) =>
                pair.Right.GetOptions(pair.Left).TryGetValue("build_metadata.AdditionalFiles.TestVisibleItemMetadata", out var perFileLoggingSwitch)
                ? perFileLoggingSwitch.Equals("true", StringComparison.OrdinalIgnoreCase) : false);

        context.RegisterSourceOutput(metadata, static (context, visibleMetadata) =>
        {
            context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(
                id: "GEN001",
                title: "Generator Information",
                messageFormat: $"VisibleMetadata: {visibleMetadata}",
                category: "Generator",
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true), Location.None));
        });
    }
}