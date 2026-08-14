using System.Text;
using StrataLint.Scribe;

namespace Trureturing.Paper.Core;

public static class LatexDocumentWriter
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static byte[] Write(PaperDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var builder = new StringBuilder();
        builder.Append("\\documentclass{article}\n")
            .Append("\\usepackage{amsmath,amsthm}\n")
            .Append("\\newtheorem{theorem}{Theorem}\n")
            .Append("\\title{").Append(EscapeText(document.Title)).Append("}\n")
            .Append("\\begin{document}\n")
            .Append("\\maketitle\n");
        foreach (var theorem in document.Theorems)
        {
            builder.Append("% declaration-gid: ").Append(EscapeComment(theorem.DeclarationGid)).Append('\n')
                .Append("% describe-anchor: ").Append(EscapeComment(theorem.DescribeAnchor)).Append('\n')
                .Append("\\begin{theorem}\n")
                .Append(EscapeText(theorem.Narrative)).Append("\n\n")
                .Append(LatexWriter.WriteStatement(theorem.Statement)).Append('\n')
                .Append("\\end{theorem}\n");
        }
        builder.Append("\\end{document}\n");
        return StrictUtf8.GetBytes(builder.ToString());
    }

    private static string EscapeText(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(character switch
            {
                '\\' => "\\textbackslash{}",
                '{' => "\\{",
                '}' => "\\}",
                '$' => "\\$",
                '&' => "\\&",
                '#' => "\\#",
                '_' => "\\_",
                '%' => "\\%",
                '~' => "\\textasciitilde{}",
                '^' => "\\textasciicircum{}",
                '\r' => string.Empty,
                '\n' => "\n",
                _ when char.IsControl(character) =>
                    throw new ClaimGateException("Document prose contains a forbidden control character."),
                _ => character.ToString()
            });
        }
        return builder.ToString();
    }

    private static string EscapeComment(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl))
        {
            throw new ClaimGateException("Document identity is empty or contains a control character.");
        }
        return value.Replace("%", "percent", StringComparison.Ordinal);
    }
}
