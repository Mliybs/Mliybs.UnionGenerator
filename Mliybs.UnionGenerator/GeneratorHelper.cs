global using static Mliybs.UnionGenerator.GeneratorHelper;

using Microsoft.CodeAnalysis;
using System;
using System.CodeDom.Compiler;
using System.Runtime.CompilerServices;

namespace Mliybs.UnionGenerator;

public static class GeneratorHelper
{
    private static readonly SymbolDisplayFormat formatGlobal =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private static readonly SymbolDisplayFormat formatMetadata =
        SymbolDisplayFormat.FullyQualifiedFormat
        .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted);

    /// <summary>
    /// 返回带global::前缀的全名
    /// </summary>
    /// <param name="symbol"></param>
    /// <returns></returns>
    public static string GetFullyQualifiedName(this ISymbol symbol) => symbol.ToDisplayString(formatGlobal);

    public static string GetFullMetadataName(this ISymbol symbol) => symbol.ToDisplayString(formatMetadata);

    public static void WriteErrorMessage(this IndentedTextWriter writer, string message = "", [CallerFilePath] string filePath = "", [CallerMemberName] string memberName = "", [CallerLineNumber] int lineNumber = 0)
    {
        writer.WriteLine($"#error Error in file {filePath} member {memberName} line {lineNumber} with message {message}");
    }
}
