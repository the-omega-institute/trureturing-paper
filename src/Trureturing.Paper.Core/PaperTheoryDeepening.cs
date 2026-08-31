using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Trureturing.Paper.Core;

public static class PaperTheoryDeepeningSchemas
{
    public const string DeepeningRequest = "paper-theory-deepening-request.v1";
    public const string TheoryIteration = "paper-theory-iteration.v1";
    public const string TheoremPackage = "paper-theorem-package.v1";
    public const string SplitProposal = "paper-candidate-split-proposal.v1";
    public const string MergeProposal = "paper-candidate-merge-proposal.v1";
    public const string ResearchLedgerEntry = "paper-research-ledger-entry.v1";
}

public sealed record PaperTheoryDeepeningRequestContent(
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string ScopeRef,
    [property: JsonRequired] string InventoryRef,
    [property: JsonRequired] IReadOnlyList<string> PriorTheoremPackageRefs,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] int Round,
    [property: JsonRequired] string Phase,
    [property: JsonRequired] PaperCodexPhaseContract Contract,
    [property: JsonRequired] string RequestedAt);

public sealed record PaperTheoryDeepeningRequest(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string RequestId,
    [property: JsonRequired] PaperTheoryDeepeningRequestContent RequestContent);

public sealed record PaperTheoryProgressEvidence(
    [property: JsonRequired] int NewTheoremLikeClaims,
    [property: JsonRequired] int StrengthenedTheoremLikeClaims,
    [property: JsonRequired] int DependencyEdgesAdded,
    [property: JsonRequired] int ProofObligationsClosed,
    [property: JsonRequired] int CounterexamplesResolved,
    [property: JsonRequired] bool AbstractionChanged,
    [property: JsonRequired] bool NoveltyBoundaryChanged);

public sealed record PaperTheoryIterationContent(
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string ScopeRef,
    [property: JsonRequired] string InventoryRef,
    [property: JsonRequired] string DeepeningRequestRef,
    [property: JsonRequired] IReadOnlyList<string> PriorTheoremPackageRefs,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] int Round,
    [property: JsonRequired] IReadOnlyList<string> ChangedClaimIds,
    [property: JsonRequired] IReadOnlyList<string> NewClaimIds,
    [property: JsonRequired] IReadOnlyList<string> StrengthenedClaimIds,
    [property: JsonRequired] IReadOnlyList<string> RetiredClaimIds,
    [property: JsonRequired] IReadOnlyList<string> ProofSpine,
    [property: JsonRequired] string NovelIncrement,
    [property: JsonRequired] string PriorWorkBoundary,
    [property: JsonRequired] IReadOnlyList<string> CounterexampleFindings,
    [property: JsonRequired] IReadOnlyList<string> SplitCandidateClaimIds,
    [property: JsonRequired] IReadOnlyList<string> MergeCandidatePaperIds,
    [property: JsonRequired] PaperTheoryProgressEvidence ProgressEvidence,
    [property: JsonRequired] string CreatedAt);

public sealed record PaperTheoryIteration(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string IterationId,
    [property: JsonRequired] PaperTheoryIterationContent IterationContent);

public sealed record PaperTheoremPackageClaim(
    [property: JsonRequired] string ClaimId,
    [property: JsonRequired] string Title,
    [property: JsonRequired] string Kind,
    [property: JsonRequired] string Statement,
    [property: JsonRequired] IReadOnlyList<string> Dependencies,
    [property: JsonRequired] string ProofStatus,
    [property: JsonRequired] IReadOnlyList<string> ProofOutline,
    [property: JsonRequired] string NoveltyStatus,
    [property: JsonRequired] bool LoadBearing);

public sealed record PaperTheoremPackageContent(
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string ScopeRef,
    [property: JsonRequired] string InventoryRef,
    [property: JsonRequired] string IterationRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] int PackageVersion,
    [property: JsonRequired] string Maturity,
    [property: JsonRequired] IReadOnlyList<PaperTheoremPackageClaim> Claims,
    [property: JsonRequired] IReadOnlyList<string> MainTheoremClaimIds,
    [property: JsonRequired] IReadOnlyList<string> CorollaryClaimIds,
    [property: JsonRequired] IReadOnlyList<string> SharpnessClaimIds,
    [property: JsonRequired] IReadOnlyList<string> OpenProofObligations,
    [property: JsonRequired] IReadOnlyList<string> KnownResultsToCite,
    [property: JsonRequired] string NoveltySummary,
    [property: JsonRequired] string PublicationSignificance,
    [property: JsonRequired] string CreatedAt);

public sealed record PaperTheoremPackage(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string TheoremPackageId,
    [property: JsonRequired] PaperTheoremPackageContent TheoremPackageContent);

public static class PaperTheoryDeepeningService
{
    public const string DeepeningPhase = "A2-theory-deepening";

    private static readonly Regex DigestPattern =
        new("^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant);
    private static readonly Regex ClaimIdPattern =
        new("^[A-Za-z][A-Za-z0-9._:-]{0,255}$", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> ClaimKinds = new(
        ["definition", "lemma", "proposition", "theorem", "corollary",
         "counterexample", "proof-interface"],
        StringComparer.Ordinal);
    private static readonly HashSet<string> TheoremLikeKinds =
        new(["lemma", "proposition", "theorem", "corollary"], StringComparer.Ordinal);
    private static readonly HashSet<string> ProofStatuses = new(
        ["certified-foundation", "informal-complete", "informal-gap", "disproved"],
        StringComparer.Ordinal);
    private static readonly HashSet<string> NoveltyStatuses = new(
        ["new", "strengthened", "known-tool", "negative-boundary"],
        StringComparer.Ordinal);
    private static readonly HashSet<string> MaturityValues =
        new(["developing", "audit-candidate"], StringComparer.Ordinal);

    public static PaperTheoryDeepeningRequest CreateDeepeningRequest(
        PaperTheoryProgram program,
        PaperTheoryScope scope,
        PaperTheoryInventory inventory,
        PaperTheoremPackage? previousPackage,
        int round,
        string requestedAt)
    {
        PaperPortfolioService.Validate(program);
        PaperTheoryFoundationService.Validate(scope, program);
        PaperTheoryFoundationService.Validate(inventory);
        EnsureFoundationBinding(program, scope, inventory);
        ParseUtc(requestedAt, nameof(requestedAt));
        if (round < 1)
        {
            throw new InvalidDataException("Deepening round must be positive.");
        }

        IReadOnlyList<string> priorRefs = previousPackage is null
            ? []
            : [previousPackage.TheoremPackageId];
        if (round == 1 && priorRefs.Count != 0)
        {
            throw new InvalidDataException("Round one cannot bind a prior theorem package.");
        }
        if (round > 1)
        {
            if (previousPackage is null)
            {
                throw new InvalidDataException(
                    "Later deepening rounds must bind exactly one prior theorem package.");
            }
            Validate(previousPackage);
            EnsurePackageBinding(program, scope, inventory, previousPackage);
            if (previousPackage.TheoremPackageContent.PackageVersion != round - 1)
            {
                throw new InvalidDataException(
                    "Prior theorem package version must immediately precede the request round.");
            }
        }

        var contract = new PaperCodexPhaseContract(
            [
                program.TheoryProgramId,
                scope.ScopeId,
                inventory.InventoryId,
                .. priorRefs
            ],
            [
                PaperTheoryDeepeningSchemas.TheoryIteration,
                PaperTheoryDeepeningSchemas.TheoremPackage,
                PaperTheoryDeepeningSchemas.SplitProposal,
                PaperTheoryDeepeningSchemas.MergeProposal,
                PaperTheoryDeepeningSchemas.ResearchLedgerEntry
            ],
            [
                "Search for the canonical abstraction that makes the theorem system cohere.",
                "Strengthen the central theorem and its supporting dependency chain.",
                "Close an explicit informal proof spine rather than adding isolated observations.",
                "Derive nontrivial corollaries, converses, classifications, bounds, or rigidity consequences.",
                "Test hypotheses through sharpness examples and structural counterexamples.",
                "Separate known tools from the manuscript's genuinely new increment.",
                "Identify mature split candidates and cross-paper merge candidates.",
                "Return one complete theorem-package update for this paper in this round."
            ],
            [
                "Do not run Lean, dispatch Formalize, certify claims, or write manuscript prose.",
                "Do not count renaming, synonym restatement, or notation cleanup as progress.",
                "Do not add an easy isolated lemma only to increase theorem count.",
                "Do not weaken the central theorem merely to close the round.",
                "Do not claim proof completion without an explicit multi-step proof spine.",
                "Do not duplicate a sibling paper or published theorem without attribution."
            ],
            [
                PaperTheoryDeepeningSchemas.TheoryIteration,
                PaperTheoryDeepeningSchemas.TheoremPackage
            ],
            [
                "At least one theorem-like claim is new or materially strengthened.",
                "At least one proof obligation is closed.",
                "The dependency graph, abstraction, novelty boundary, or counterexample analysis changes materially.",
                "The updated package remains a coherent multi-theorem DAG.",
                "Every claimed novel increment is separated from known cited tools."
            ],
            [
                "The output changes only wording, notation, order, or presentation.",
                "The theorem package has no complete proof spine or no publication-level increment.",
                "The paper is padded with unrelated results instead of split or merge proposals.",
                "Any exact program, scope, inventory, or prior-package reference changes."
            ]);

        var content = new PaperTheoryDeepeningRequestContent(
            program.TheoryProgramId,
            scope.ScopeId,
            inventory.InventoryId,
            priorRefs,
            program.ProgramContent.PaperId,
            round,
            DeepeningPhase,
            contract,
            requestedAt);
        ValidateContract(contract);
        return new(
            PaperTheoryDeepeningSchemas.DeepeningRequest,
            Reference(content),
            content);
    }

    public static PaperTheoryIteration CreateIteration(
        PaperTheoryProgram program,
        PaperTheoryScope scope,
        PaperTheoryInventory inventory,
        PaperTheoryDeepeningRequest request,
        PaperTheoryIterationContent content)
    {
        PaperPortfolioService.Validate(program);
        PaperTheoryFoundationService.Validate(scope, program);
        PaperTheoryFoundationService.Validate(inventory);
        Validate(request);
        EnsureFoundationBinding(program, scope, inventory);
        ValidateIterationContent(content, inventory);
        if (!string.Equals(content.TheoryProgramRef, program.TheoryProgramId, StringComparison.Ordinal)
            || !string.Equals(content.ScopeRef, scope.ScopeId, StringComparison.Ordinal)
            || !string.Equals(content.InventoryRef, inventory.InventoryId, StringComparison.Ordinal)
            || !string.Equals(content.DeepeningRequestRef, request.RequestId, StringComparison.Ordinal)
            || !string.Equals(content.PaperId, program.ProgramContent.PaperId, StringComparison.Ordinal)
            || content.Round != request.RequestContent.Round
            || !content.PriorTheoremPackageRefs.SequenceEqual(
                request.RequestContent.PriorTheoremPackageRefs,
                StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Theory iteration changed its program, foundation, request, round, or prior package.");
        }
        return new(
            PaperTheoryDeepeningSchemas.TheoryIteration,
            Reference(content),
            content);
    }

    public static PaperTheoremPackage CreateTheoremPackage(
        PaperTheoryProgram program,
        PaperTheoryScope scope,
        PaperTheoryInventory inventory,
        PaperTheoryIteration iteration,
        PaperTheoremPackageContent content)
    {
        PaperPortfolioService.Validate(program);
        PaperTheoryFoundationService.Validate(scope, program);
        PaperTheoryFoundationService.Validate(inventory);
        Validate(iteration);
        EnsureFoundationBinding(program, scope, inventory);
        ValidatePackageContent(content);
        if (!string.Equals(content.TheoryProgramRef, program.TheoryProgramId, StringComparison.Ordinal)
            || !string.Equals(content.ScopeRef, scope.ScopeId, StringComparison.Ordinal)
            || !string.Equals(content.InventoryRef, inventory.InventoryId, StringComparison.Ordinal)
            || !string.Equals(content.IterationRef, iteration.IterationId, StringComparison.Ordinal)
            || !string.Equals(content.PaperId, program.ProgramContent.PaperId, StringComparison.Ordinal)
            || content.PackageVersion != iteration.IterationContent.Round)
        {
            throw new InvalidDataException(
                "Theorem package changed its program, foundation, iteration, paper, or version.");
        }
        return new(
            PaperTheoryDeepeningSchemas.TheoremPackage,
            Reference(content),
            content);
    }

    public static PaperCandidateState AdvanceAfterDeepening(
        PaperCandidateState state,
        PaperTheoremPackage package,
        string advancedAt)
    {
        PaperPortfolioService.Validate(state);
        Validate(package);
        ParseUtc(advancedAt, nameof(advancedAt));
        if (!string.Equals(state.Phase, "theory-deepening", StringComparison.Ordinal)
            || !string.Equals(state.PaperId, package.TheoremPackageContent.PaperId, StringComparison.Ordinal)
            || !string.Equals(
                state.TheoryProgramRef,
                package.TheoremPackageContent.TheoryProgramRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Only the matching theory-deepening paper may accept a theorem package.");
        }
        string next = string.Equals(
            package.TheoremPackageContent.Maturity,
            "audit-candidate",
            StringComparison.Ordinal)
            ? "audit-pending"
            : "theory-deepening";
        return state with
        {
            Phase = next,
            CompletedCycles = state.CompletedCycles + 1,
            ConsecutiveNoProgressCycles = 0,
            LastProgressAt = advancedAt,
            StatusReason = $"theorem package {package.TheoremPackageId}; maturity={package.TheoremPackageContent.Maturity}"
        };
    }

    public static PaperCandidateState RecordNoProgress(
        PaperCandidateState state,
        string reason,
        string recordedAt)
    {
        PaperPortfolioService.Validate(state);
        ParseUtc(recordedAt, nameof(recordedAt));
        RequireText(reason, nameof(reason), 4096);
        if (!string.Equals(state.Phase, "theory-deepening", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "No-progress evidence may only be recorded during theory deepening.");
        }
        return state with
        {
            CompletedCycles = state.CompletedCycles + 1,
            ConsecutiveNoProgressCycles = state.ConsecutiveNoProgressCycles + 1,
            StatusReason = $"no substantive theorem progress: {reason}"
        };
    }

    public static void Validate(PaperTheoryDeepeningRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireExact(request.Schema, PaperTheoryDeepeningSchemas.DeepeningRequest, "schema");
        PaperTheoryDeepeningRequestContent c = request.RequestContent
            ?? throw new InvalidDataException("request_content is required.");
        RequireDigest(c.TheoryProgramRef, "theory_program_ref");
        RequireDigest(c.ScopeRef, "scope_ref");
        RequireDigest(c.InventoryRef, "inventory_ref");
        RequireDigestList(c.PriorTheoremPackageRefs, "prior_theorem_package_refs", 0, 1);
        RequireText(c.PaperId, "paper_id", 512);
        if (c.Round < 1
            || (c.Round == 1 && c.PriorTheoremPackageRefs.Count != 0)
            || (c.Round > 1 && c.PriorTheoremPackageRefs.Count != 1))
        {
            throw new InvalidDataException(
                "Deepening request round and prior-package coordinates are inconsistent.");
        }
        RequireExact(c.Phase, DeepeningPhase, "phase");
        ValidateContract(c.Contract);
        ParseUtc(c.RequestedAt, "requested_at");
        RequireIdentity(request.RequestId, c, nameof(request.RequestId));
    }

    public static void Validate(PaperTheoryIteration iteration)
    {
        ArgumentNullException.ThrowIfNull(iteration);
        RequireExact(iteration.Schema, PaperTheoryDeepeningSchemas.TheoryIteration, "schema");
        ValidateIterationContent(iteration.IterationContent, null);
        RequireIdentity(
            iteration.IterationId,
            iteration.IterationContent,
            nameof(iteration.IterationId));
    }

    public static void Validate(PaperTheoremPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        RequireExact(package.Schema, PaperTheoryDeepeningSchemas.TheoremPackage, "schema");
        ValidatePackageContent(package.TheoremPackageContent);
        RequireIdentity(
            package.TheoremPackageId,
            package.TheoremPackageContent,
            nameof(package.TheoremPackageId));
    }

    private static void ValidateIterationContent(
        PaperTheoryIterationContent content,
        PaperTheoryInventory? inventory)
    {
        ArgumentNullException.ThrowIfNull(content);
        RequireDigest(content.TheoryProgramRef, "theory_program_ref");
        RequireDigest(content.ScopeRef, "scope_ref");
        RequireDigest(content.InventoryRef, "inventory_ref");
        RequireDigest(content.DeepeningRequestRef, "deepening_request_ref");
        RequireDigestList(content.PriorTheoremPackageRefs, "prior_theorem_package_refs", 0, 1);
        RequireText(content.PaperId, "paper_id", 512);
        if (content.Round < 1
            || (content.Round == 1 && content.PriorTheoremPackageRefs.Count != 0)
            || (content.Round > 1 && content.PriorTheoremPackageRefs.Count != 1))
        {
            throw new InvalidDataException(
                "Theory iteration round and prior-package coordinates are inconsistent.");
        }
        RequireClaimIdList(content.ChangedClaimIds, "changed_claim_ids", 1);
        RequireClaimIdList(content.NewClaimIds, "new_claim_ids", 0);
        RequireClaimIdList(content.StrengthenedClaimIds, "strengthened_claim_ids", 0);
        RequireClaimIdList(content.RetiredClaimIds, "retired_claim_ids", 0);
        EnsureDisjoint(
            content.NewClaimIds,
            content.StrengthenedClaimIds,
            content.RetiredClaimIds);
        var changed = new HashSet<string>(content.ChangedClaimIds, StringComparer.Ordinal);
        if (!content.NewClaimIds.Concat(content.StrengthenedClaimIds)
            .Concat(content.RetiredClaimIds).All(changed.Contains))
        {
            throw new InvalidDataException(
                "changed_claim_ids must include every new, strengthened, and retired claim.");
        }
        if (inventory is not null)
        {
            var existing = inventory.InventoryContent.Items
                .Select(item => item.ClaimId)
                .ToHashSet(StringComparer.Ordinal);
            if (content.NewClaimIds.Any(existing.Contains)
                || content.StrengthenedClaimIds.Any(id => !existing.Contains(id))
                || content.RetiredClaimIds.Any(id => !existing.Contains(id)))
            {
                throw new InvalidDataException(
                    "Iteration new, strengthened, and retired claim sets do not match the inventory.");
            }
        }
        RequireTextList(content.ProofSpine, "proof_spine", 16384, 3);
        RequireText(content.NovelIncrement, "novel_increment", 32768, 80);
        RequireText(content.PriorWorkBoundary, "prior_work_boundary", 32768, 40);
        RequireTextList(
            content.CounterexampleFindings,
            "counterexample_findings",
            16384,
            1);
        RequireClaimIdList(
            content.SplitCandidateClaimIds,
            "split_candidate_claim_ids",
            0);
        RequireTextList(
            content.MergeCandidatePaperIds,
            "merge_candidate_paper_ids",
            512,
            0);
        ValidateProgress(
            content.ProgressEvidence,
            content.NewClaimIds.Count,
            content.StrengthenedClaimIds.Count);
        ParseUtc(content.CreatedAt, "created_at");
    }

    private static void ValidateProgress(
        PaperTheoryProgressEvidence evidence,
        int newClaimCount,
        int strengthenedClaimCount)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.NewTheoremLikeClaims < 0
            || evidence.StrengthenedTheoremLikeClaims < 0
            || evidence.DependencyEdgesAdded < 0
            || evidence.ProofObligationsClosed < 0
            || evidence.CounterexamplesResolved < 0
            || evidence.NewTheoremLikeClaims != newClaimCount
            || evidence.StrengthenedTheoremLikeClaims != strengthenedClaimCount)
        {
            throw new InvalidDataException(
                "Theory progress evidence counters are invalid or inconsistent.");
        }
        if (evidence.NewTheoremLikeClaims + evidence.StrengthenedTheoremLikeClaims < 1)
        {
            throw new InvalidDataException(
                "A2 fake extension: at least one theorem-like claim must be new or strengthened.");
        }
        if (evidence.ProofObligationsClosed < 1)
        {
            throw new InvalidDataException(
                "A2 fake extension: at least one proof obligation must be closed.");
        }
        if (evidence.DependencyEdgesAdded < 1
            && evidence.CounterexamplesResolved < 1
            && !evidence.AbstractionChanged
            && !evidence.NoveltyBoundaryChanged)
        {
            throw new InvalidDataException(
                "A2 fake extension: no structural, abstraction, novelty, or counterexample progress.");
        }
    }

    private static void ValidatePackageContent(PaperTheoremPackageContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        RequireDigest(content.TheoryProgramRef, "theory_program_ref");
        RequireDigest(content.ScopeRef, "scope_ref");
        RequireDigest(content.InventoryRef, "inventory_ref");
        RequireDigest(content.IterationRef, "iteration_ref");
        RequireText(content.PaperId, "paper_id", 512);
        if (content.PackageVersion < 1 || !MaturityValues.Contains(content.Maturity))
        {
            throw new InvalidDataException("Theorem package version or maturity is invalid.");
        }
        if (content.Claims is null || content.Claims.Count < 3)
        {
            throw new InvalidDataException(
                "A theorem package must contain at least three claims.");
        }
        var byId = new Dictionary<string, PaperTheoremPackageClaim>(StringComparer.Ordinal);
        int theoremLikeCount = 0;
        foreach (PaperTheoremPackageClaim claim in content.Claims)
        {
            ValidatePackageClaim(claim);
            if (!byId.TryAdd(claim.ClaimId, claim))
            {
                throw new InvalidDataException(
                    "Theorem package claim_id values must be unique.");
            }
            if (TheoremLikeKinds.Contains(claim.Kind))
            {
                theoremLikeCount++;
            }
        }
        if (theoremLikeCount < 2)
        {
            throw new InvalidDataException(
                "A theorem package must contain a series of theorem-like claims.");
        }
        foreach (PaperTheoremPackageClaim claim in content.Claims)
        {
            foreach (string dependency in claim.Dependencies)
            {
                if (!byId.ContainsKey(dependency))
                {
                    throw new InvalidDataException(
                        $"Theorem-package dependency {dependency} does not resolve.");
                }
            }
        }
        EnsureAcyclic(byId);
        RequireClaimIdList(content.MainTheoremClaimIds, "main_theorem_claim_ids", 1);
        RequireClaimIdList(content.CorollaryClaimIds, "corollary_claim_ids", 0);
        RequireClaimIdList(content.SharpnessClaimIds, "sharpness_claim_ids", 0);
        foreach (string id in content.MainTheoremClaimIds)
        {
            if (!byId.TryGetValue(id, out PaperTheoremPackageClaim? claim)
                || !string.Equals(claim.Kind, "theorem", StringComparison.Ordinal)
                || !claim.LoadBearing)
            {
                throw new InvalidDataException(
                    "Every main theorem must resolve to a load-bearing theorem claim.");
            }
        }
        RequireKinds(content.CorollaryClaimIds, byId, "corollary", "corollary_claim_ids");
        foreach (string id in content.SharpnessClaimIds)
        {
            if (!byId.ContainsKey(id))
            {
                throw new InvalidDataException(
                    "Every sharpness_claim_id must resolve in the theorem package.");
            }
        }
        RequireTextList(content.OpenProofObligations, "open_proof_obligations", 16384, 0);
        RequireTextList(content.KnownResultsToCite, "known_results_to_cite", 16384, 1);
        RequireText(content.NoveltySummary, "novelty_summary", 32768, 80);
        RequireText(content.PublicationSignificance, "publication_significance", 32768, 80);
        ParseUtc(content.CreatedAt, "created_at");

        if (string.Equals(content.Maturity, "audit-candidate", StringComparison.Ordinal))
        {
            if (content.OpenProofObligations.Count != 0
                || content.CorollaryClaimIds.Count < 1
                || content.SharpnessClaimIds.Count < 1)
            {
                throw new InvalidDataException(
                    "An audit-candidate package needs no open proof obligations, a corollary, and a sharpness claim.");
            }
            foreach (PaperTheoremPackageClaim claim in content.Claims.Where(c => c.LoadBearing))
            {
                if (!IsProofComplete(claim.ProofStatus))
                {
                    throw new InvalidDataException(
                        "Every load-bearing audit-candidate claim needs a complete informal or certified proof.");
                }
            }
        }
    }

    private static void ValidatePackageClaim(PaperTheoremPackageClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);
        RequireClaimId(claim.ClaimId, "claim_id");
        RequireText(claim.Title, "title", 1024);
        if (!ClaimKinds.Contains(claim.Kind)
            || !ProofStatuses.Contains(claim.ProofStatus)
            || !NoveltyStatuses.Contains(claim.NoveltyStatus))
        {
            throw new InvalidDataException(
                "Theorem package claim kind, proof status, or novelty status is unsupported.");
        }
        RequireText(claim.Statement, "statement", 32768);
        RequireClaimIdList(claim.Dependencies, "dependencies", 0);
        int minimumOutline = IsProofComplete(claim.ProofStatus) ? 2 : 1;
        RequireTextList(claim.ProofOutline, "proof_outline", 16384, minimumOutline);
    }

    private static bool IsProofComplete(string status) =>
        string.Equals(status, "informal-complete", StringComparison.Ordinal)
        || string.Equals(status, "certified-foundation", StringComparison.Ordinal);

    private static void RequireKinds(
        IReadOnlyList<string> ids,
        IReadOnlyDictionary<string, PaperTheoremPackageClaim> byId,
        string kind,
        string name)
    {
        foreach (string id in ids)
        {
            if (!byId.TryGetValue(id, out PaperTheoremPackageClaim? claim)
                || !string.Equals(claim.Kind, kind, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Every {name} entry must resolve to kind {kind}.");
            }
        }
    }

    private static void EnsureFoundationBinding(
        PaperTheoryProgram program,
        PaperTheoryScope scope,
        PaperTheoryInventory inventory)
    {
        if (!string.Equals(scope.ScopeContent.TheoryProgramRef, program.TheoryProgramId, StringComparison.Ordinal)
            || !string.Equals(inventory.InventoryContent.TheoryProgramRef, program.TheoryProgramId, StringComparison.Ordinal)
            || !string.Equals(inventory.InventoryContent.ScopeRef, scope.ScopeId, StringComparison.Ordinal)
            || !string.Equals(program.ProgramContent.PaperId, scope.ScopeContent.PaperId, StringComparison.Ordinal)
            || !string.Equals(program.ProgramContent.PaperId, inventory.InventoryContent.PaperId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Program, scope, and inventory do not describe one paper foundation.");
        }
    }

    private static void EnsurePackageBinding(
        PaperTheoryProgram program,
        PaperTheoryScope scope,
        PaperTheoryInventory inventory,
        PaperTheoremPackage package)
    {
        if (!string.Equals(package.TheoremPackageContent.TheoryProgramRef, program.TheoryProgramId, StringComparison.Ordinal)
            || !string.Equals(package.TheoremPackageContent.ScopeRef, scope.ScopeId, StringComparison.Ordinal)
            || !string.Equals(package.TheoremPackageContent.InventoryRef, inventory.InventoryId, StringComparison.Ordinal)
            || !string.Equals(package.TheoremPackageContent.PaperId, program.ProgramContent.PaperId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Prior theorem package does not belong to this paper foundation.");
        }
    }

    private static void EnsureDisjoint(params IReadOnlyList<string>[] lists)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (IReadOnlyList<string> list in lists)
        {
            foreach (string value in list)
            {
                if (!seen.Add(value))
                {
                    throw new InvalidDataException(
                        "New, strengthened, and retired claim sets must be disjoint.");
                }
            }
        }
    }

    private static void EnsureAcyclic(
        IReadOnlyDictionary<string, PaperTheoremPackageClaim> byId)
    {
        var marks = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string id in byId.Keys)
        {
            Visit(id);
        }
        void Visit(string id)
        {
            if (marks.TryGetValue(id, out int mark))
            {
                if (mark == 1)
                {
                    throw new InvalidDataException(
                        "Theorem package dependency graph must be acyclic.");
                }
                return;
            }
            marks[id] = 1;
            foreach (string dependency in byId[id].Dependencies)
            {
                Visit(dependency);
            }
            marks[id] = 2;
        }
    }

    private static void ValidateContract(PaperCodexPhaseContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        RequireDigestList(contract.ExactInputRefs, "exact_input_refs", 3, 4);
        RequireTextList(contract.PermittedArtifactFamilies, "permitted_artifact_families", 512, 1);
        RequireTextList(contract.ScientificTasks, "scientific_tasks", 8192, 1);
        RequireTextList(contract.ForbiddenShortcuts, "forbidden_shortcuts", 8192, 1);
        RequireTextList(contract.RequiredOutputSchemas, "required_output_schemas", 512, 1);
        RequireTextList(contract.PassConditions, "pass_conditions", 8192, 1);
        RequireTextList(contract.FailConditions, "fail_conditions", 8192, 1);
    }

    private static void RequireClaimIdList(
        IReadOnlyList<string>? values,
        string name,
        int minimumCount)
    {
        if (values is null || values.Count < minimumCount)
        {
            throw new InvalidDataException($"{name} is incomplete.");
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            RequireClaimId(value, name);
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

    private static void RequireDigestList(
        IReadOnlyList<string>? values,
        string name,
        int minimumCount,
        int maximumCount)
    {
        if (values is null || values.Count < minimumCount || values.Count > maximumCount)
        {
            throw new InvalidDataException($"{name} has an invalid count.");
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

    private static void RequireTextList(
        IReadOnlyList<string>? values,
        string name,
        int maximumLength,
        int minimumCount)
    {
        if (values is null || values.Count < minimumCount)
        {
            throw new InvalidDataException($"{name} is incomplete.");
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            RequireText(value, name, maximumLength);
            if (!seen.Add(value))
            {
                throw new InvalidDataException($"{name} contains duplicates.");
            }
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
