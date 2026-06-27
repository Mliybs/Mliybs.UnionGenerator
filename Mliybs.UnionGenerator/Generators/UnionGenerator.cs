using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Mliybs.UnionGenerator.Generators
{
    [Generator]
    public class UnionGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            //Debugger.Launch();
            context.RegisterPostInitializationOutput(static x =>
            {
                x.AddSource("Mliybs.UnionGenerator.UnionGenerator.g.cs", """
                    using System;

                    namespace Mliybs.UnionGenerator
                    {
                        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
                        public class UnionAttribute : Attribute
                        {
                        }

                        [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
                        public class UnionTemplateAttribute : Attribute
                        {
                        }
                    }
                    """);
            });

            var unionProvider = context.SyntaxProvider.ForAttributeWithMetadataName("Mliybs.UnionGenerator.UnionAttribute", (x, token) =>
            x is StructDeclarationSyntax { Keyword.Text: not "union" } or ClassDeclarationSyntax
                , (x, token) =>
                (x.SemanticModel, x.Attributes.Single(x => x.AttributeClass?.GetFullyQualifiedName() == "global::Mliybs.UnionGenerator.UnionAttribute"), (TypeDeclarationSyntax)x.TargetNode, (INamedTypeSymbol)x.TargetSymbol));

            context.RegisterSourceOutput(unionProvider.Combine(context.CompilationProvider), static async (context, tuple) =>
            {
                var stringWriter = new StringWriter();
                var writer = new IndentedTextWriter(stringWriter);
                var ((semanticModel, unionAttribute, unionDeclaration, unionSymbol), compilation) = tuple;

                try
                {
                    if (unionSymbol.ContainingType is not null)
                    {
                        writer.WriteErrorMessage("Nested union is not supported.");
                    }

                    INamedTypeSymbol? template = null;

                    {
                        var members = unionSymbol.GetTypeMembers();

                        if (members.Length == 0)
                        {
                            writer.WriteErrorMessage("Cannot find UnionTemplate.");
                            return;
                        }

                        foreach (var member in members)
                        {
                            if (member.GetAttributes().Any(x => x.AttributeClass?.GetFullMetadataName() == "Mliybs.UnionGenerator.UnionTemplateAttribute"))
                            {
                                if (template is not null)
                                {
                                    writer.WriteErrorMessage("There are more than one UnionTemplate.");
                                    return;
                                }

                                else template = member;
                            }
                        }
                    }

                    if (template is null)
                    {
                        writer.WriteErrorMessage("Cannot find UnionTemplate.");
                        return;
                    }

                    foreach (var reference in template.DeclaringSyntaxReferences)
                    {
                        var node = await reference.GetSyntaxAsync();

                        if (node is StructDeclarationSyntax { Keyword.Text: "union", ParameterList.Parameters: { } @params })
                        {
                            var namespaces = new Stack<string>();
                            var @namespace = unionSymbol.ContainingNamespace;
                            while (@namespace is { IsGlobalNamespace: false })
                            {
                                namespaces.Push(@namespace.Name);
                                @namespace = @namespace.ContainingNamespace;
                            }

                            writer.WriteLine("using System;");

                            writer.WriteLine("#nullable enable");

                            if (namespaces.Count > 0)
                            {
                                writer.Write("namespace ");
                                writer.Write(string.Join(".", namespaces));
                                writer.WriteLine(';');
                            }

                            writer.WriteLine("[global::System.Runtime.CompilerServices.Union]");
                            writer.Write("partial ");

                            switch (unionDeclaration)
                            {
                                case StructDeclarationSyntax:
                                    writer.Write("struct ");
                                    writer.Write(unionSymbol.GetFullMetadataName());
                                    break;

                                case ClassDeclarationSyntax:
                                    writer.Write("class ");
                                    writer.Write(unionSymbol.GetFullMetadataName());
                                    break;
                            }

                            writer.WriteLine(" : global::System.Runtime.CompilerServices.IUnion");

                            writer.WriteLine('{');
                            writer.Indent++;

                            var hasReferenceType = false;

                            writer.WriteLine("private byte _tag;");

                            writer.WriteLine();

                            writer.WriteLine("public bool HasValue => _tag != 0;");

                            writer.WriteLine();

                            byte tag = 1;

                            foreach (var param in @params)
                            {
                                var paramType = semanticModel.GetTypeInfo(param.Type!).Type;

                                var paramName = "";

                                if (paramType is null)
                                {
                                    writer.WriteErrorMessage($"Cannot find the type of param {param}.");
                                    return;
                                }

                                var paramTypeNameNullable = paramType.WithNullableAnnotation(NullableAnnotation.Annotated).GetFullyQualifiedName();
                                if (paramType.IsValueType) paramTypeNameNullable += '?';

                                var paramTypeNameNotNullable = paramType.WithNullableAnnotation(NullableAnnotation.NotAnnotated).GetFullyQualifiedName();

                                if (paramType.IsValueType)
                                {
                                    writer.Write("private ");
                                    writer.Write(paramTypeNameNotNullable);
                                    writer.Write(' ');
                                    paramName = $"_{paramType.Name}Value";
                                    writer.Write(paramName);
                                    writer.WriteLine(" = default!;");
                                }

                                else if (paramType.IsReferenceType)
                                {
                                    paramName = "_referenceValue";

                                    if (!hasReferenceType)
                                    {
                                        writer.WriteLine("private object _referenceValue = default!;");
                                        hasReferenceType = true;
                                    }
                                }

                                else if (paramType.TypeKind == TypeKind.TypeParameter)
                                {
                                    writer.Write("private ");
                                    writer.Write(paramTypeNameNotNullable);
                                    writer.Write(' ');
                                    paramName = $"_{paramType.Name}Value";
                                    writer.Write(paramName);
                                    writer.WriteLine(" = default!;");
                                }

                                writer.WriteLine();

                                writer.Write("public ");
                                writer.Write(unionSymbol.Name);
                                writer.Write('(');
                                writer.Write(paramTypeNameNullable);
                                writer.WriteLine(" value)");
                                writer.WriteLine('{');
                                writer.Indent++;

                                if (paramType.IsValueType)
                                {
                                    writer.WriteLine("if (value.HasValue)");
                                    writer.WriteLine('{');
                                    writer.Indent++;
                                    writer.WriteLine($"{paramName} = value.Value;");
                                    writer.Write("_tag = ");
                                    writer.Write(tag);
                                    writer.WriteLine(';');
                                    writer.Indent--;
                                    writer.WriteLine('}');
                                }

                                else if (paramType.IsReferenceType)
                                {
                                    writer.WriteLine("if (value is not null)");
                                    writer.WriteLine('{');
                                    writer.Indent++;
                                    writer.WriteLine($"{paramName} = value;");
                                    writer.Write("_tag = "); // 其实这一段没啥用
                                    writer.Write(tag);
                                    writer.WriteLine(';');
                                    writer.Indent--;
                                    writer.WriteLine('}');
                                }

                                else if (paramType.TypeKind == TypeKind.TypeParameter)
                                {
                                    writer.WriteLine($"if (value is {paramTypeNameNotNullable} _value)");
                                    writer.WriteLine('{');
                                    writer.Indent++;
                                    writer.WriteLine($"{paramName} = _value;");
                                    writer.Write("_tag = ");
                                    writer.Write(tag);
                                    writer.WriteLine(';');
                                    writer.Indent--;
                                    writer.WriteLine('}');
                                }

                                writer.Indent--;
                                writer.WriteLine('}');

                                writer.WriteLine();

                                writer.Write("public bool TryGetValue(out ");
                                writer.Write(paramTypeNameNotNullable);
                                writer.WriteLine(" value)");

                                writer.WriteLine('{');
                                writer.Indent++;

                                if (paramType.IsValueType)
                                {
                                    writer.Write("value = ");
                                    writer.Write(paramName);
                                    writer.WriteLine(';');

                                    writer.Write("return _tag == ");
                                    writer.Write(tag);
                                    writer.WriteLine(';');
                                }

                                else if (paramType.IsReferenceType)
                                {
                                    writer.Write("if (");
                                    writer.Write(paramName);
                                    writer.Write(" is ");
                                    writer.Write(paramTypeNameNotNullable);
                                    writer.WriteLine(" _value)");

                                    writer.WriteLine('{');
                                    writer.Indent++;

                                    writer.WriteLine("value = _value;");
                                    writer.WriteLine("return true;");

                                    writer.Indent--;
                                    writer.WriteLine('}');

                                    writer.WriteLine();

                                    writer.WriteLine("else");
                                    writer.WriteLine('{');
                                    writer.Indent++;
                                    writer.WriteLine("value = default!;");
                                    writer.WriteLine("return false;");
                                    writer.Indent--;
                                    writer.WriteLine('}');
                                }

                                else if (paramType.TypeKind == TypeKind.TypeParameter)
                                {
                                    writer.Write("value = ");
                                    writer.Write(paramName);
                                    writer.WriteLine(';');

                                    writer.Write("return _tag == ");
                                    writer.Write(tag);
                                    writer.WriteLine(';');
                                }

                                writer.Indent--;
                                writer.WriteLine('}');

                                writer.WriteLine();

                                tag++;
                            }

                            writer.WriteLine("public object? Value => _tag switch");
                            writer.WriteLine('{');
                            writer.Indent++;

                            tag = 1;

                            foreach (var param in @params)
                            {
                                writer.Write(tag);
                                writer.Write(" => ");

                                var type = semanticModel.GetTypeInfo(param.Type!).Type!;

                                if (type.IsValueType)
                                {
                                    writer.Write('_');
                                    writer.Write(type.Name);
                                    writer.WriteLine("Value,");
                                }

                                else if (type.IsReferenceType)
                                    writer.WriteLine("_referenceValue,");

                                else if (type.TypeKind == TypeKind.TypeParameter)
                                {
                                    writer.Write('_');
                                    writer.Write(type.Name);
                                    writer.WriteLine("Value,");
                                }

                                tag++;
                            }

                            writer.WriteLine("_ => null");

                            writer.Indent--;
                            writer.Write('}');
                            writer.WriteLine(';');
                        }
                    }

                    writer.Indent--;
                    writer.WriteLine('}');
                }
                finally
                {
                    writer.Flush();
                    context.AddSource($"{unionSymbol.GetFullMetadataName().Replace('<', '_').Replace('>', '_')}.g.cs", stringWriter.ToString());
                }
            });
        }
    }
}
