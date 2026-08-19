using System.Text;
using Trureturing.Paper.Core;
using Xunit;

namespace Trureturing.Paper.Tests;

public sealed class SharedTruthExportReaderTests
{
    [Fact]
    public void Reads_the_base_wire_without_importing_base_engine_types()
    {
        var export = SharedTruthExportReader.Read(Encoding.UTF8.GetBytes(ValidJson));

        Assert.Equal(new string('1', 40), export.SourceCommit);
        Assert.Equal(new string('2', 40), export.SourceTree);
        var node = Assert.Single(export.Nodes);
        Assert.Equal("D5/S0/Carrier/TraceConjugation.lean", node.RepoPath);
        Assert.Equal("sha256:" + new string('a', 64), node.FrozenNodeId);
        Assert.Equal(["propext"], node.NodeAxiomClosure);
        var declaration = Assert.Single(node.Declarations);
        Assert.Equal("D5.S0.Carrier.trace_conj", declaration.DeclarationNameKey);
        Assert.Equal("theorem", declaration.Kind);
        Assert.Equal("sha256:" + new string('b', 64), declaration.StatementId);
    }

    [Theory]
    [InlineData("\"schema\":\"stratalint.truth-export\"", "\"schema\":\"wrong\"")]
    [InlineData("\"schema_version\":1", "\"schema_version\":2")]
    [InlineData("\"dialect\":\"stratalint.truth-export.v1\"", "\"dialect\":\"stratalint.truth-export.v2\"")]
    [InlineData("\"producer\":\"TruthExportCommand\"", "\"producer\":\"Impostor\"")]
    public void Rejects_unsupported_contract_identity(string from, string to)
    {
        Assert.Throws<ClaimGateException>(() =>
            SharedTruthExportReader.Read(Encoding.UTF8.GetBytes(
                ValidJson.Replace(from, to, StringComparison.Ordinal))));
    }

    [Fact]
    public void Rejects_unknown_or_duplicate_members()
    {
        var unknown = ValidJson.Replace(
            "\"source_tree\":\"" + new string('2', 40) + "\"",
            "\"source_tree\":\"" + new string('2', 40) + "\",\"extra\":true",
            StringComparison.Ordinal);
        var duplicate = ValidJson.Replace(
            "\"producer\":\"TruthExportCommand\"",
            "\"producer\":\"TruthExportCommand\",\"producer\":\"TruthExportCommand\"",
            StringComparison.Ordinal);

        Assert.Throws<ClaimGateException>(() =>
            SharedTruthExportReader.Read(Encoding.UTF8.GetBytes(unknown)));
        Assert.Throws<ClaimGateException>(() =>
            SharedTruthExportReader.Read(Encoding.UTF8.GetBytes(duplicate)));
    }

    [Fact]
    public void Rejects_noncanonical_source_and_content_identities()
    {
        foreach (var invalid in new[]
        {
            ValidJson.Replace(new string('1', 40), new string('A', 40), StringComparison.Ordinal),
            ValidJson.Replace(new string('2', 40), "abc", StringComparison.Ordinal),
            ValidJson.Replace("sha256:" + new string('a', 64), new string('a', 64), StringComparison.Ordinal),
            ValidJson.Replace("sha256:" + new string('b', 64), "sha256:" + new string('G', 64), StringComparison.Ordinal),
        })
        {
            Assert.Throws<ClaimGateException>(() =>
                SharedTruthExportReader.Read(Encoding.UTF8.GetBytes(invalid)));
        }
    }

    [Fact]
    public void Rejects_nonlocal_or_nonlean_repository_paths()
    {
        foreach (var path in new[]
        {
            "../outside.lean",
            "/absolute.lean",
            "D5\\S0\\Bad.lean",
            "D5/S0/NotLean.txt",
            "D5/S0/./Alias.lean",
        })
        {
            var invalid = ValidJson.Replace(
                "D5/S0/Carrier/TraceConjugation.lean",
                path,
                StringComparison.Ordinal);
            Assert.Throws<ClaimGateException>(() =>
                SharedTruthExportReader.Read(Encoding.UTF8.GetBytes(invalid)));
        }
    }

    [Fact]
    public void Rejects_unsorted_or_duplicate_axioms()
    {
        var unsorted = ValidJson.Replace(
            "\"node_axiom_closure\":[\"propext\"]",
            "\"node_axiom_closure\":[\"propext\",\"Classical.choice\"]",
            StringComparison.Ordinal);
        var duplicate = ValidJson.Replace(
            "\"node_axiom_closure\":[\"propext\"]",
            "\"node_axiom_closure\":[\"propext\",\"propext\"]",
            StringComparison.Ordinal);

        Assert.Throws<ClaimGateException>(() =>
            SharedTruthExportReader.Read(Encoding.UTF8.GetBytes(unsorted)));
        Assert.Throws<ClaimGateException>(() =>
            SharedTruthExportReader.Read(Encoding.UTF8.GetBytes(duplicate)));
    }

    [Fact]
    public void Rejects_duplicate_node_or_declaration_identity_even_when_tuple_order_is_strict()
    {
        var secondNode = NodeJson
            .Replace(
                "sha256:" + new string('a', 64),
                "sha256:" + new string('c', 64),
                StringComparison.Ordinal)
            .Replace(
                "D5.S0.Carrier.trace_conj",
                "D5.S0.Carrier.trace_conj_two",
                StringComparison.Ordinal)
            .Replace(
                "sha256:" + new string('b', 64),
                "sha256:" + new string('d', 64),
                StringComparison.Ordinal);
        var duplicateNodePath = ValidJson.Replace(
            "\"nodes\":[" + NodeJson + "]",
            "\"nodes\":[" + NodeJson + "," + secondNode + "]",
            StringComparison.Ordinal);
        var secondDeclaration = DeclarationJson.Replace(
            "sha256:" + new string('b', 64),
            "sha256:" + new string('c', 64),
            StringComparison.Ordinal);
        var duplicateDeclarationName = ValidJson.Replace(
            "\"declarations\":[" + DeclarationJson + "]",
            "\"declarations\":[" + DeclarationJson + "," + secondDeclaration + "]",
            StringComparison.Ordinal);

        Assert.Throws<ClaimGateException>(() =>
            SharedTruthExportReader.Read(Encoding.UTF8.GetBytes(duplicateNodePath)));
        Assert.Throws<ClaimGateException>(() =>
            SharedTruthExportReader.Read(Encoding.UTF8.GetBytes(duplicateDeclarationName)));
    }

    [Fact]
    public void Rejects_unsorted_nodes_and_declarations()
    {
        var earlierNode = NodeJson
            .Replace(
                "D5/S0/Carrier/TraceConjugation.lean",
                "D5/S0/Carrier/Alpha.lean",
                StringComparison.Ordinal)
            .Replace(
                "sha256:" + new string('a', 64),
                "sha256:" + new string('c', 64),
                StringComparison.Ordinal)
            .Replace(
                "D5.S0.Carrier.trace_conj",
                "D5.S0.Carrier.alpha",
                StringComparison.Ordinal)
            .Replace(
                "sha256:" + new string('b', 64),
                "sha256:" + new string('d', 64),
                StringComparison.Ordinal);
        var unsortedNodes = ValidJson.Replace(
            "\"nodes\":[" + NodeJson + "]",
            "\"nodes\":[" + NodeJson + "," + earlierNode + "]",
            StringComparison.Ordinal);
        var earlierDeclaration = DeclarationJson
            .Replace(
                "D5.S0.Carrier.trace_conj",
                "D5.S0.Carrier.alpha",
                StringComparison.Ordinal)
            .Replace(
                "sha256:" + new string('b', 64),
                "sha256:" + new string('c', 64),
                StringComparison.Ordinal);
        var unsortedDeclarations = ValidJson.Replace(
            "\"declarations\":[" + DeclarationJson + "]",
            "\"declarations\":[" + DeclarationJson + "," + earlierDeclaration + "]",
            StringComparison.Ordinal);

        Assert.Throws<ClaimGateException>(() =>
            SharedTruthExportReader.Read(Encoding.UTF8.GetBytes(unsortedNodes)));
        Assert.Throws<ClaimGateException>(() =>
            SharedTruthExportReader.Read(Encoding.UTF8.GetBytes(unsortedDeclarations)));
    }

    private const string DeclarationJson =
        "{\"declaration_name_key\":\"D5.S0.Carrier.trace_conj\","
        + "\"kind\":\"theorem\","
        + "\"statement_id\":\"sha256:" + B64 + "\"}";

    private const string NodeJson =
        "{\"declarations\":[" + DeclarationJson + "],"
        + "\"frozen_node_id\":\"sha256:" + A64 + "\","
        + "\"node_axiom_closure\":[\"propext\"],"
        + "\"repo_path\":\"D5/S0/Carrier/TraceConjugation.lean\"}";

    private const string ValidJson =
        "{\"dialect\":\"stratalint.truth-export.v1\","
        + "\"nodes\":[" + NodeJson + "],"
        + "\"producer\":\"TruthExportCommand\","
        + "\"schema\":\"stratalint.truth-export\","
        + "\"schema_version\":1,"
        + "\"source_commit\":\"1111111111111111111111111111111111111111\","
        + "\"source_tree\":\"2222222222222222222222222222222222222222\"}\n";

    private const string A64 =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string B64 =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
}
