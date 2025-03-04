using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

[Generator]
public class CustomGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        if (!Debugger.IsAttached)
        {
            // how to make it break here?
            Debugger.Launch();
        }

        context.RegisterPostInitializationOutput(static postInitializationContext =>
        {
            // postInitializationContext.AddEmbeddedAttributeDefinition();
            postInitializationContext.AddSource("myGeneratedFile.cs", SourceText.From("""
                using System;
                using Microsoft.CodeAnalysis;

                namespace GeneratedNamespace
                {
                    // [Embedded]
                    internal sealed class GeneratedAttribute : Attribute
                    {
                    }
                }
                """, Encoding.UTF8));

            postInitializationContext.AddSource("TestClass.Generated.cs", SourceText.From("""
                using System;

                public partial class UserClass
                {
                    public partial void UserMethod()
                    {
                        Console.WriteLine("GeneratedMethod");
                    }
                }
                """, Encoding.UTF8));
        });

        var pipeline = context.AdditionalTextsProvider
            .Where(static (text) => text.Path.EndsWith(".txt"))
            .Select(static (text, cancellationToken) =>
            {
                var name = Path.GetFileName(text.Path);
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
                    DiagnosticSeverity.Warning,
                    isEnabledByDefault: true), Location.None));

                if (pair.code != null)
                {
                    context.AddSource($"{pair.name}generated.cs", SourceText.From(pair.code.ToString(), Encoding.UTF8));
                }
            });
    }
}