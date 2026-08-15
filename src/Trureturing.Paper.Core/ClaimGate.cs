namespace Trureturing.Paper.Core;

public static class ClaimGate
{
    public static PaperDocument Resolve(
        PaperRecipe recipe,
        FrozenInputs inputs,
        SourceSnapshot snapshot,
        FrozenTruthGraph? truthGraph = null)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!string.Equals(recipe.Schema, "recipe.v1", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(recipe.PaperId)
            || string.IsNullOrWhiteSpace(recipe.Title)
            || recipe.Claims is null
            || recipe.Claims.Count == 0)
        {
            throw new ClaimGateException("Recipe is not a nonempty recipe.v1 document.");
        }

        var theoremBlocks = new List<TheoremBlock>(recipe.Claims.Count);
        var requestedGids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var claim in recipe.Claims)
        {
            if (claim is null
                || string.IsNullOrWhiteSpace(claim.DeclarationGid)
                || string.IsNullOrWhiteSpace(claim.DescribeAnchor)
                || !claim.DescribeAnchor.StartsWith("describe:", StringComparison.Ordinal)
                || !requestedGids.Add(claim.DeclarationGid))
            {
                throw new ClaimGateException("Recipe claim identity or describe anchor is invalid or duplicate.");
            }

            var declarations = inputs.Declarations
                .Where(value => string.Equals(value.DeclarationGid, claim.DeclarationGid, StringComparison.Ordinal))
                .ToArray();
            if (declarations.Length != 1)
            {
                throw new ClaimGateException(
                    $"Declaration '{claim.DeclarationGid}' is absent or ambiguous in the frozen ledger.");
            }
            var declaration = declarations[0];
            if (!string.Equals(declaration.Status, "frozen", StringComparison.Ordinal))
            {
                throw new ClaimGateException($"Declaration '{claim.DeclarationGid}' is not frozen.");
            }
            if (truthGraph is not null)
            {
                var binding = TruthGraphReader.RequireClosedTheorem(
                    truthGraph,
                    declaration.DeclarationGid,
                    claim.DescribeAnchor);
                if (!string.Equals(
                        declaration.TruthAnchor,
                        binding.DocumentGid,
                        StringComparison.Ordinal))
                {
                    throw new ClaimGateException(
                        $"Declaration '{claim.DeclarationGid}' is not bound to its closed truth-graph node.");
                }
            }
            if (!string.Equals(declaration.LeanReportSha256, snapshot.LeanReportSha256, StringComparison.Ordinal))
            {
                throw new ClaimGateException(
                    $"Declaration '{claim.DeclarationGid}' is not bound to the blessed Lean report.");
            }
            if (declaration.DeclaredAxioms is null
                || declaration.AllowedAxioms is null
                || declaration.DeclaredAxioms.Any(axiom =>
                    !declaration.AllowedAxioms.Contains(axiom, StringComparer.Ordinal)))
            {
                throw new ClaimGateException(
                    $"Declaration '{claim.DeclarationGid}' exceeds its frozen axiom whitelist.");
            }

            var blueprints = inputs.BlueprintBlocks
                .Where(value => string.Equals(value.DescribeAnchor, claim.DescribeAnchor, StringComparison.Ordinal))
                .ToArray();
            if (blueprints.Length != 1)
            {
                throw new ClaimGateException(
                    $"Describe anchor '{claim.DescribeAnchor}' is absent or ambiguous.");
            }
            var blueprint = blueprints[0];
            if (!string.Equals(blueprint.DeclarationGid, declaration.DeclarationGid, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(declaration.TruthAnchor)
                || !string.Equals(blueprint.TruthAnchor, declaration.TruthAnchor, StringComparison.Ordinal))
            {
                throw new ClaimGateException(
                    $"Describe anchor '{claim.DescribeAnchor}' is not bound to the frozen truth anchor.");
            }

            theoremBlocks.Add(new TheoremBlock(
                declaration.DeclarationGid,
                blueprint.DescribeAnchor,
                blueprint.Narrative,
                declaration.Statement));
        }

        return new PaperDocument(recipe.PaperId, recipe.Title, theoremBlocks);
    }
}
