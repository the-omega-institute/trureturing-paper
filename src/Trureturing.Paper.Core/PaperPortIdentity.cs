using System.Security.Cryptography;
using System.Text;
using StrataLint.Scribe;

namespace Trureturing.Paper.Core;

public static class PaperPortIdentity
{
    public static string StatementId(Formula statement)
    {
        ArgumentNullException.ThrowIfNull(statement);
        return "sha256:" + Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(LatexWriter.WriteStatement(statement))))
            .ToLowerInvariant();
    }
}
