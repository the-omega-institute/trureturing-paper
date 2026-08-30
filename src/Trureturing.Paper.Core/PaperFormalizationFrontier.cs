using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Trureturing.Paper.Core;

public static class PaperFormalizationFrontierSchemas
{
    public const string Frontier = "paper-formalization-frontier.v1";
    public const string FrontierEvent = "paper-formalization-frontier-event.v1";
    public const string FrontierState = "paper-formalization-frontier-state.v1";
}

public sealed record PaperFormalizationFrontierNodeSpec(
    [property: JsonRequired] string ClaimId,
    [property: JsonRequired] string FormalizationKind,
    [property: JsonRequired] int Priority,
    [property: JsonRequired] string TargetLeanPackage,
    [property: JsonRequired] string TargetLeanModule,
    [property: JsonRequired] string FormalStatement,
    [property: JsonRequired] string AcceptanceCriterion);

public sealed record PaperFormalizationFrontierNode(
    [property: JsonRequired] string NodeId,
    [property: JsonRequired] string ClaimId,
    [property: JsonRequired] string Title,
    [property: JsonRequired] string FormalizationKind,
    [property: JsonRequired] string TheoremPackageKind,
    [property: JsonRequired] string InformalStatement,
    [property: JsonRequired] string FormalStatement,
    [property: JsonRequired] IReadOnlyList<string> DependencyNodeIds,
    [property: JsonRequired] int ParallelWave,
    [property: JsonRequired] int Priority,
    [property: JsonRequired] string TargetLeanPackage,
    [property: JsonRequired] string TargetLeanModule,
    [property: JsonRequired] string AcceptanceCriterion,
    [property: JsonRequired] string InitialStatus);

public sealed record PaperFormalizationFrontierContent(
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string TheoremPackageRef,
    [property: JsonRequired] string TheoryAuditRef,
    [property: JsonRequired] string ScorecardRef,
    [property: JsonRequired] string PortfolioDecisionRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TruthReleaseDigest,
    [property: JsonRequired] string TopologyDigest,
    [property: JsonRequired] string PaperResearchInputRef,
    [property: JsonRequired] IReadOnlyList<PaperFormalizationFrontierNode> Nodes,
    [property: JsonRequired] IReadOnlyList<string> MainTheoremNodeIds,
    [property: JsonRequired] IReadOnlyList<string> SharpnessNodeIds,
    [property: JsonRequired] IReadOnlyList<string> CorollaryNodeIds,
    [property: JsonRequired] int CriticalPathDepth,
    [property: JsonRequired] int MaximumWaveWidth,
    [property: JsonRequired] string CreatedAt);

public sealed record PaperFormalizationFrontier(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string FrontierId,
    [property: JsonRequired] PaperFormalizationFrontierContent FrontierContent);

internal sealed record PaperFormalizationFrontierNodeIdentity(
    string TheoremPackageRef,
    string PaperId,
    string ClaimId);

public static class PaperFormalizationFrontierService
{
    public const string InitialNodeStatus = "selection-pending";

    private static readonly Regex DigestPattern =
        new("^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant);
    private static readonly Regex ClaimIdPattern =
        new("^[A-Za-z][A-Za-z0-9._:-]{0,255}$", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> FormalizationKinds = new(
        ["definition", "prerequisite", "structural", "main-theorem",
         "sharpness", "corollary", "counterexample", "proof-interface"],
        StringComparer.Ordinal);

    public static PaperFormalizationFrontier CreateFrontier(
        PaperTheoryProgram program,
        PaperTheoremPackage package,
        PaperTheoryAudit audit,
        PaperCandidateScorecard scorecard,
        PaperPortfolioDecision portfolioDecision,
        IReadOnlyList<PaperFormalizationFrontierNodeSpec> nodeSpecs,
        string createdAt)
    {
        ValidatePromotionInputs(
            program,
            package,
            audit,
            scorecard,
            portfolioDecision);
        ParseUtc(createdAt, nameof(createdAt));
        ArgumentNullException.ThrowIfNull(nodeSpecs);
        if (nodeSpecs.Count != package.TheoremPackageContent.Claims.Count)
        {
            throw new InvalidDataException(
                "A formalization frontier must specify every theorem-package claim exactly once.");
        }

        var specsByClaim = new Dictionary<string, PaperFormalizationFrontierNodeSpec>(
            StringComparer.Ordinal);
        foreach (PaperFormalizationFrontierNodeSpec spec in nodeSpecs)
        {
            ValidateSpec(spec);
            if (!specsByClaim.TryAdd(spec.ClaimId, spec))
            {
                throw new InvalidDataException(
                    "Formalization frontier claim specifications must be unique.");
            }
        }
        var claimsById = package.TheoremPackageContent.Claims.ToDictionary(
            claim => claim.ClaimId,
            StringComparer.Ordinal);
        if (!claimsById.Keys.OrderBy(value => value, StringComparer.Ordinal)
            .SequenceEqual(specsByClaim.Keys.OrderBy(value => value, StringComparer.Ordinal)))
        {
            throw new InvalidDataException(
                "Formalization frontier specifications changed the theorem-package claim set.");
        }

        ValidateSemanticKinds(package, specsByClaim);
        var nodeIdsByClaim = claimsById.Keys.ToDictionary(
            claimId => claimId,
            claimId => Reference(new PaperFormalizationFrontierNodeIdentity(
                package.TheoremPackageId,
                program.ProgramContent.PaperId,
                claimId)),
            StringComparer.Ordinal);
        var waveMemo = new Dictionary<string, int>(StringComparer.Ordinal);
        int Wave(string claimId)
        {
            if (waveMemo.TryGetValue(claimId, out int cached))
            {
                return cached;
            }
            PaperTheoremPackageClaim claim = claimsById[claimId];
            int wave = claim.Dependencies.Count == 0
                ? 0
                : claim.Dependencies.Max(dependency => Wave(dependency)) + 1;
            waveMemo[claimId] = wave;
            return wave;
        }

        PaperFormalizationFrontierNode[] nodes = package.TheoremPackageContent.Claims
            .Select(claim =>
            {
                PaperFormalizationFrontierNodeSpec spec = specsByClaim[claim.ClaimId];
                return new PaperFormalizationFrontierNode(
                    nodeIdsByClaim[claim.ClaimId],
                    claim.ClaimId,
                    claim.Title,
                    spec.FormalizationKind,
                    claim.Kind,
                    claim.Statement,
                    spec.FormalStatement,
                    claim.Dependencies.Select(dependency => nodeIdsByClaim[dependency])
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray(),
                    Wave(claim.ClaimId),
                    spec.Priority,
                    spec.TargetLeanPackage,
                    spec.TargetLeanModule,
                    spec.AcceptanceCriterion,
                    InitialNodeStatus);
            })
            .OrderBy(node => node.ParallelWave)
            .ThenByDescending(node => node.Priority)
            .ThenBy(node => node.ClaimId, StringComparer.Ordinal)
            .ToArray();
        int criticalPathDepth = nodes.Max(node => node.ParallelWave) + 1;
        int maximumWaveWidth = nodes
            .GroupBy(node => node.ParallelWave)
            .Max(group => group.Count());
        var content = new PaperFormalizationFrontierContent(
            program.TheoryProgramId,
            package.TheoremPackageId,
            audit.AuditId,
            scorecard.ScorecardId,
            portfolioDecision.DecisionId,
            program.ProgramContent.PaperId,
            program.ProgramContent.TruthReleaseDigest,
            program.ProgramContent.TopologyDigest,
            program.ProgramContent.PaperResearchInputRef,
            nodes,
            package.TheoremPackageContent.MainTheoremClaimIds
                .Select(claimId => nodeIdsByClaim[claimId])
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            package.TheoremPackageContent.SharpnessClaimIds
                .Select(claimId => nodeIdsByClaim[claimId])
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            package.TheoremPackageContent.CorollaryClaimIds
                .Select(claimId => nodeIdsByClaim[claimId])
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            criticalPathDepth,
            maximumWaveWidth,
            createdAt);
        ValidateContent(content);
        return new(PaperFormalizationFrontierSchemas.Frontier, Reference(content), content);
    }

    public static void Validate(PaperFormalizationFrontier frontier)
    {
        ArgumentNullException.ThrowIfNull(frontier);
        RequireExact(frontier.Schema, PaperFormalizationFrontierSchemas.Frontier, "schema");
        ValidateContent(frontier.FrontierContent);
        RequireIdentity(frontier.FrontierId, frontier.FrontierContent, nameof(frontier.FrontierId));
    }

    public static PaperFormalizationFrontierNode RequireNode(
        PaperFormalizationFrontier frontier,
        string nodeId)
    {
        Validate(frontier);
        RequireDigest(nodeId, nameof(nodeId));
        return frontier.FrontierContent.Nodes.SingleOrDefault(node =>
                string.Equals(node.NodeId, nodeId, StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                $"Formalization frontier does not contain node {nodeId}.");
    }

    private static void ValidatePromotionInputs(
        PaperTheoryProgram program,
        PaperTheoremPackage package,
        PaperTheoryAudit audit,
        PaperCandidateScorecard scorecard,
        PaperPortfolioDecision portfolioDecision)
    {
        PaperPortfolioService.Validate(program);
        PaperTheoryDeepeningService.Validate(package);
        PaperTheoryAuditService.Validate(audit);
        PaperPortfolioDecisionService.Validate(scorecard);
        PaperPortfolioDecisionService.Validate(portfolioDecision);
        if (!audit.AuditContent.Passed
            || !scorecard.ScorecardContent.PromotionEligible
            || !string.Equals(package.TheoremPackageContent.Maturity, "audit-candidate", StringComparison.Ordinal)
            || !string.Equals(package.TheoremPackageContent.TheoryProgramRef, program.TheoryProgramId, StringComparison.Ordinal)
            || !string.Equals(package.TheoremPackageContent.PaperId, program.ProgramContent.PaperId, StringComparison.Ordinal)
            || !string.Equals(audit.AuditContent.TheoremPackageRef, package.TheoremPackageId, StringComparison.Ordinal)
            || !string.Equals(scorecard.ScorecardContent.TheoremPackageRef, package.TheoremPackageId, StringComparison.Ordinal)
            || !string.Equals(scorecard.ScorecardContent.TheoryAuditRef, audit.AuditId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Formalization frontier requires one passed, promotion-eligible audit-candidate package.");
        }
        PaperPortfolioPaperDecision decision = portfolioDecision.DecisionContent.Decisions
            .SingleOrDefault(item => string.Equals(
                item.PaperId,
                program.ProgramContent.PaperId,
                StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                "Portfolio decision does not contain this paper.");
        if (!string.Equals(decision.Action, "promote-to-frontier", StringComparison.Ordinal)
            || !string.Equals(decision.TheoryProgramRef, program.TheoryProgramId, StringComparison.Ordinal)
            || !string.Equals(decision.ScorecardRef, scorecard.ScorecardId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Portfolio decision did not promote this exact paper scorecard.");
        }
    }

    private static void ValidateSemanticKinds(
        PaperTheoremPackage package,
        IReadOnlyDictionary<string, PaperFormalizationFrontierNodeSpec> specs)
    {
        foreach (string id in package.TheoremPackageContent.MainTheoremClaimIds)
        {
            if (!string.Equals(specs[id].FormalizationKind, "main-theorem", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Every main theorem must be marked main-theorem in the frontier.");
            }
        }
        foreach (string id in package.TheoremPackageContent.SharpnessClaimIds)
        {
            if (!string.Equals(specs[id].FormalizationKind, "sharpness", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Every sharpness claim must be marked sharpness in the frontier.");
            }
        }
        foreach (string id in package.TheoremPackageContent.CorollaryClaimIds)
        {
            if (!string.Equals(specs[id].FormalizationKind, "corollary", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Every corollary claim must be marked corollary in the frontier.");
            }
        }
    }

    private static void ValidateContent(PaperFormalizationFrontierContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        RequireDigest(content.TheoryProgramRef, "theory_program_ref");
        RequireDigest(content.TheoremPackageRef, "theorem_package_ref");
        RequireDigest(content.TheoryAuditRef, "theory_audit_ref");
        RequireDigest(content.ScorecardRef, "scorecard_ref");
        RequireDigest(content.PortfolioDecisionRef, "portfolio_decision_ref");
        RequireText(content.PaperId, "paper_id", 512);
        RequireDigest(content.TruthReleaseDigest, "truth_release_digest");
        RequireDigest(content.TopologyDigest, "topology_digest");
        RequireDigest(content.PaperResearchInputRef, "paper_research_input_ref");
        if (content.Nodes is null || content.Nodes.Count < 3)
        {
            throw new InvalidDataException(
                "A formalization frontier must contain a multi-claim theorem package.");
        }
        var byNode = new Dictionary<string, PaperFormalizationFrontierNode>(StringComparer.Ordinal);
        var claims = new HashSet<string>(StringComparer.Ordinal);
        foreach (PaperFormalizationFrontierNode node in content.Nodes)
        {
            ValidateNode(node);
            if (!byNode.TryAdd(node.NodeId, node) || !claims.Add(node.ClaimId))
            {
                throw new InvalidDataException(
                    "Frontier node and claim identities must be unique.");
            }
            string expectedNodeId = Reference(new PaperFormalizationFrontierNodeIdentity(
                content.TheoremPackageRef,
                content.PaperId,
                node.ClaimId));
            if (!string.Equals(node.NodeId, expectedNodeId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Frontier node_id does not address its theorem-package claim identity.");
            }
        }
        foreach (PaperFormalizationFrontierNode node in content.Nodes)
        {
            foreach (string dependency in node.DependencyNodeIds)
            {
                if (!byNode.TryGetValue(dependency, out PaperFormalizationFrontierNode? dependencyNode)
                    || dependencyNode.ParallelWave >= node.ParallelWave)
                {
                    throw new InvalidDataException(
                        "Every frontier dependency must resolve in an earlier parallel wave.");
                }
            }
        }
        RequireNodeIds(content.MainTheoremNodeIds, byNode, "main_theorem_node_ids", 1);
        RequireNodeIds(content.SharpnessNodeIds, byNode, "sharpness_node_ids", 1);
        RequireNodeIds(content.CorollaryNodeIds, byNode, "corollary_node_ids", 1);
        int expectedDepth = content.Nodes.Max(node => node.ParallelWave) + 1;
        int expectedWidth = content.Nodes.GroupBy(node => node.ParallelWave)
            .Max(group => group.Count());
        if (content.CriticalPathDepth != expectedDepth
            || content.MaximumWaveWidth != expectedWidth)
        {
            throw new InvalidDataException(
                "Frontier critical path or maximum wave width is inconsistent.");
        }
        ParseUtc(content.CreatedAt, "created_at");
    }

    private static void ValidateNode(PaperFormalizationFrontierNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        RequireDigest(node.NodeId, "node_id");
        RequireClaimId(node.ClaimId, "claim_id");
        RequireText(node.Title, "title", 1024);
        if (!FormalizationKinds.Contains(node.FormalizationKind))
        {
            throw new InvalidDataException(
                $"Unsupported formalization kind {node.FormalizationKind}.");
        }
        RequireText(node.TheoremPackageKind, "theorem_package_kind", 128);
        RequireText(node.InformalStatement, "informal_statement", 32768);
        RequireText(node.FormalStatement, "formal_statement", 32768, 20);
        RequireDigestList(node.DependencyNodeIds, "dependency_node_ids", 0);
        if (node.ParallelWave < 0 || node.Priority is < 0 or > 100)
        {
            throw new InvalidDataException(
                "Frontier parallel wave or priority is invalid.");
        }
        RequireText(node.TargetLeanPackage, "target_lean_package", 1024);
        RequireText(node.TargetLeanModule, "target_lean_module", 2048);
        RequireText(node.AcceptanceCriterion, "acceptance_criterion", 16384, 20);
        RequireExact(node.InitialStatus, InitialNodeStatus, "initial_status");
    }

    private static void ValidateSpec(PaperFormalizationFrontierNodeSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        RequireClaimId(spec.ClaimId, "claim_id");
        if (!FormalizationKinds.Contains(spec.FormalizationKind)
            || spec.Priority is < 0 or > 100)
        {
            throw new InvalidDataException(
                "Frontier node specification kind or priority is invalid.");
        }
        RequireText(spec.TargetLeanPackage, "target_lean_package", 1024);
        RequireText(spec.TargetLeanModule, "target_lean_module", 2048);
        RequireText(spec.FormalStatement, "formal_statement", 32768, 20);
        RequireText(spec.AcceptanceCriterion, "acceptance_criterion", 16384, 20);
    }

    private static void RequireNodeIds(
        IReadOnlyList<string>? values,
        IReadOnlyDictionary<string, PaperFormalizationFrontierNode> byNode,
        string name,
        int minimum)
    {
        RequireDigestList(values, name, minimum);
        foreach (string value in values!)
        {
            if (!byNode.ContainsKey(value))
            {
                throw new InvalidDataException($"{name} contains an unresolved node.");
            }
        }
    }

    private static void RequireDigestList(
        IReadOnlyList<string>? values,
        string name,
        int minimum)
    {
        if (values is null || values.Count < minimum)
        {
            throw new InvalidDataException($"{name} is incomplete.");
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            RequireDigest(value, name);
            if (!seen.Add(value))
            {
                throw new InvalidDataException($"{name} contains duplicates.");
            }
        }
    }

    private static void RequireClaimId(string value, string name)
    {
        if (!ClaimIdPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException($"{name} contains a noncanonical claim id.");
        }
    }

    private static void RequireText(
        string value,
        string name,
        int maximumLength,
        int minimumLength = 1)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length < minimumLength
            || value.Length > maximumLength)
        {
            throw new InvalidDataException(
                $"{name} must contain between {minimumLength} and {maximumLength} characters.");
        }
    }

    private static string Reference<T>(T content) =>
        CanonicalJson.Sha256Reference(CanonicalJson.Serialize(content));

    private static void RequireIdentity<T>(string reference, T content, string name)
    {
        RequireDigest(reference, name);
        if (!string.Equals(reference, Reference(content), StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{name} does not address canonical content bytes.");
        }
    }

    private static void RequireDigest(string value, string name)
    {
        if (!DigestPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException($"{name} must be sha256:<64 lowercase hex>.");
        }
    }

    private static void RequireExact(string actual, string expected, string name)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{name} must be {expected}.");
        }
    }

    private static DateTimeOffset ParseUtc(string value, string name)
    {
        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset parsed))
        {
            throw new InvalidDataException($"{name} must be an RFC 3339 timestamp.");
        }
        return parsed;
    }
}
