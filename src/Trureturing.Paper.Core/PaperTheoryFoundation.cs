using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Trureturing.Paper.Core;

public static class PaperTheoryFoundationSchemas
{
    public const string ScopeRequest = "paper-theory-scope-request.v1";
    public const string Scope = "paper-theory-scope.v1";
    public const string InventoryRequest = "paper-theory-inventory-request.v1";
    public const string Inventory = "paper-theory-inventory.v1";
}

public sealed record PaperCodexPhaseContract(
    [property: JsonRequired] IReadOnlyList<string> ExactInputRefs,
    [property: JsonRequired] IReadOnlyList<string> PermittedArtifactFamilies,
    [property: JsonRequired] IReadOnlyList<string> ScientificTasks,
    [property: JsonRequired] IReadOnlyList<string> ForbiddenShortcuts,
    [property: JsonRequired] IReadOnlyList<string> RequiredOutputSchemas,
    [property: JsonRequired] IReadOnlyList<string> PassConditions,
    [property: JsonRequired] IReadOnlyList<string> FailConditions);

public sealed record PaperTheoryScopeRequestContent(
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string Phase,
    [property: JsonRequired] PaperCodexPhaseContract Contract,
    [property: JsonRequired] string RequestedAt);

public sealed record PaperTheoryScopeRequest(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string RequestId,
    [property: JsonRequired] PaperTheoryScopeRequestContent RequestContent);

public sealed record PaperTheoryScopeContent(
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string ScopeRequestRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string ResearchQuestion,
    [property: JsonRequired] string AbstractionTarget,
    [property: JsonRequired] string PublicationFloor,
    [property: JsonRequired] IReadOnlyList<string> InScopeObligations,
    [property: JsonRequired] IReadOnlyList<string> SupportingOnly,
    [property: JsonRequired] IReadOnlyList<string> OutOfScope,
    [property: JsonRequired] string SplitPolicy,
    [property: JsonRequired] IReadOnlyList<string> CounterexampleObligations,
    [property: JsonRequired] string CreatedAt);

public sealed record PaperTheoryScope(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string ScopeId,
    [property: JsonRequired] PaperTheoryScopeContent ScopeContent);

public sealed record PaperTheoryInventoryRequestContent(
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string ScopeRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string Phase,
    [property: JsonRequired] PaperCodexPhaseContract Contract,
    [property: JsonRequired] string RequestedAt);

public sealed record PaperTheoryInventoryRequest(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string RequestId,
    [property: JsonRequired] PaperTheoryInventoryRequestContent RequestContent);

public sealed record PaperTheoryClaimInventoryItem(
    [property: JsonRequired] string ClaimId,
    [property: JsonRequired] string Title,
    [property: JsonRequired] string Kind,
    [property: JsonRequired] string Status,
    [property: JsonRequired] string Statement,
    [property: JsonRequired] IReadOnlyList<string> Dependencies,
    [property: JsonRequired] string RoleInArgument,
    [property: JsonRequired] string RequiredAction);

public sealed record PaperTheoryInventoryContent(
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string ScopeRef,
    [property: JsonRequired] string InventoryRequestRef,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] IReadOnlyList<PaperTheoryClaimInventoryItem> Items,
    [property: JsonRequired] IReadOnlyList<string> MainTheoremClaimIds,
    [property: JsonRequired] IReadOnlyList<string> MissingInterfaces,
    [property: JsonRequired] IReadOnlyList<string> StrongerVariants,
    [property: JsonRequired] IReadOnlyList<string> WeakerVariants,
    [property: JsonRequired] IReadOnlyList<string> CounterexampleObligations,
    [property: JsonRequired] string CreatedAt);

public sealed record PaperTheoryInventory(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string InventoryId,
    [property: JsonRequired] PaperTheoryInventoryContent InventoryContent);

public static class PaperTheoryFoundationService
{
    public const string ScopePhase = "A0-scope";
    public const string InventoryPhase = "A1-inventory";

    private static readonly Regex DigestPattern =
        new("^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant);
    private static readonly Regex ClaimIdPattern =
        new("^[A-Za-z][A-Za-z0-9._:-]{0,255}$", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> ClaimKinds = new(
        ["definition", "lemma", "proposition", "theorem", "corollary",
         "conjecture", "counterexample", "proof-interface"],
        StringComparer.Ordinal);
    private static readonly HashSet<string> ClaimStatuses = new(
        ["certified-foundation", "proposed", "missing", "weak", "proof-gap",
         "supporting", "out-of-scope"],
        StringComparer.Ordinal);
    private static readonly HashSet<string> TheoremLikeKinds = new(
        ["lemma", "proposition", "theorem", "corollary"],
        StringComparer.Ordinal);

    public static PaperTheoryScopeRequest CreateScopeRequest(
        PaperTheoryProgram program,
        string requestedAt)
    {
        PaperPortfolioService.Validate(program);
        ParseUtc(requestedAt, nameof(requestedAt));
        PaperTheoryProgramContent p = program.ProgramContent;
        var contract = new PaperCodexPhaseContract(
            [
                program.TheoryProgramId,
                p.CandidatePaperRef,
                p.LiteratureResearchRef,
                p.IntuitionProposalRef,
                p.PaperResearchInputRef
            ],
            ["paper-theory-scope.v1"],
            [
                "State the central research question for this paper.",
                "Select the canonical abstraction target and bind it to the exact candidate evidence.",
                "List every theorem obligation that must be closed inside this paper.",
                "Separate supporting material and independent split-paper directions.",
                "Name counterexample and sharpness obligations before theorem development."
            ],
            [
                "Do not run Lean or emit a Formalize request.",
                "Do not write journal prose or assemble a manuscript.",
                "Do not weaken the central question merely to make it easy.",
                "Do not invent certified facts or silently replace exact input references."
            ],
            [PaperTheoryFoundationSchemas.Scope],
            [
                "The scope contains a nonempty research question and abstraction target.",
                "At least one in-scope theorem obligation is explicit.",
                "Supporting, out-of-scope, split, and counterexample boundaries are explicit."
            ],
            [
                "Only wording or presentation changed.",
                "The scope omits theorem obligations or counterexample duties.",
                "The output changes the paper program or exact research state."
            ]);
        var content = new PaperTheoryScopeRequestContent(
            program.TheoryProgramId,
            p.PaperId,
            ScopePhase,
            contract,
            requestedAt);
        ValidateContract(contract);
        return new(
            PaperTheoryFoundationSchemas.ScopeRequest,
            Reference(content),
            content);
    }

    public static PaperTheoryScope CreateScope(
        PaperTheoryProgram program,
        PaperTheoryScopeRequest request,
        PaperTheoryScopeContent content)
    {
        PaperPortfolioService.Validate(program);
        Validate(request, program);
        ValidateScopeContent(content);
        if (!string.Equals(content.TheoryProgramRef, program.TheoryProgramId, StringComparison.Ordinal)
            || !string.Equals(content.ScopeRequestRef, request.RequestId, StringComparison.Ordinal)
            || !string.Equals(content.PaperId, program.ProgramContent.PaperId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Theory scope changed its program, request, or paper identity.");
        }
        return new(PaperTheoryFoundationSchemas.Scope, Reference(content), content);
    }

    public static PaperTheoryInventoryRequest CreateInventoryRequest(
        PaperTheoryProgram program,
        PaperTheoryScope scope,
        string requestedAt)
    {
        PaperPortfolioService.Validate(program);
        Validate(scope, program);
        ParseUtc(requestedAt, nameof(requestedAt));
        var contract = new PaperCodexPhaseContract(
            [
                program.TheoryProgramId,
                scope.ScopeId,
                program.ProgramContent.CandidatePaperRef,
                program.ProgramContent.LiteratureResearchRef,
                program.ProgramContent.PaperResearchInputRef
            ],
            ["paper-theory-inventory.v1"],
            [
                "Inventory every definition, theorem, lemma, corollary, conjecture, counterexample, and unfinished proof interface.",
                "Build the internal theorem dependency graph and identify the main theorem chain.",
                "Classify missing, weak, supporting, and out-of-scope claims.",
                "Record stronger and weaker variants and every counterexample obligation.",
                "Specify an actionable next operation for each claim."
            ],
            [
                "Do not edit or strengthen the mathematical theory in the inventory phase.",
                "Do not run Lean, dispatch Formalize, or write a manuscript.",
                "Do not hide a missing theorem by relabelling it as exposition.",
                "Do not omit negative evidence, weak variants, or dependency gaps."
            ],
            [PaperTheoryFoundationSchemas.Inventory],
            [
                "The inventory contains a multi-claim theorem chain with at least one internal dependency edge.",
                "Every main theorem identifier resolves to a theorem item.",
                "The dependency graph is acyclic and every internal dependency resolves.",
                "Missing interfaces and stronger, weaker, and counterexample routes are explicit."
            ],
            [
                "The output is a single isolated lemma rather than a paper-level theorem package inventory.",
                "Any main theorem, dependency, or claim identity is unresolved.",
                "The inventory changes the exact scope or paper program."
            ]);
        var content = new PaperTheoryInventoryRequestContent(
            program.TheoryProgramId,
            scope.ScopeId,
            program.ProgramContent.PaperId,
            InventoryPhase,
            contract,
            requestedAt);
        ValidateContract(contract);
        return new(
            PaperTheoryFoundationSchemas.InventoryRequest,
            Reference(content),
            content);
    }

    public static PaperTheoryInventory CreateInventory(
        PaperTheoryProgram program,
        PaperTheoryScope scope,
        PaperTheoryInventoryRequest request,
        PaperTheoryInventoryContent content)
    {
        PaperPortfolioService.Validate(program);
        Validate(scope, program);
        Validate(request, program, scope);
        ValidateInventoryContent(content);
        if (!string.Equals(content.TheoryProgramRef, program.TheoryProgramId, StringComparison.Ordinal)
            || !string.Equals(content.ScopeRef, scope.ScopeId, StringComparison.Ordinal)
            || !string.Equals(content.InventoryRequestRef, request.RequestId, StringComparison.Ordinal)
            || !string.Equals(content.PaperId, program.ProgramContent.PaperId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Theory inventory changed its program, scope, request, or paper identity.");
        }
        return new(PaperTheoryFoundationSchemas.Inventory, Reference(content), content);
    }

    public static PaperCandidateState AdvanceAfterScope(
        PaperCandidateState state,
        PaperTheoryScope scope,
        string advancedAt)
    {
        PaperPortfolioService.Validate(state);
        Validate(scope);
        ParseUtc(advancedAt, nameof(advancedAt));
        if (!string.Equals(state.Phase, "scope-pending", StringComparison.Ordinal)
            || !string.Equals(state.PaperId, scope.ScopeContent.PaperId, StringComparison.Ordinal)
            || !string.Equals(
                state.TheoryProgramRef,
                scope.ScopeContent.TheoryProgramRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("Only the matching scope-pending paper may advance.");
        }
        return state with
        {
            Phase = "inventory-pending",
            CompletedCycles = state.CompletedCycles + 1,
            ConsecutiveNoProgressCycles = 0,
            LastProgressAt = advancedAt,
            StatusReason = $"scope ready: {scope.ScopeId}"
        };
    }

    public static PaperCandidateState AdvanceAfterInventory(
        PaperCandidateState state,
        PaperTheoryInventory inventory,
        string advancedAt)
    {
        PaperPortfolioService.Validate(state);
        Validate(inventory);
        ParseUtc(advancedAt, nameof(advancedAt));
        if (!string.Equals(state.Phase, "inventory-pending", StringComparison.Ordinal)
            || !string.Equals(state.PaperId, inventory.InventoryContent.PaperId, StringComparison.Ordinal)
            || !string.Equals(
                state.TheoryProgramRef,
                inventory.InventoryContent.TheoryProgramRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Only the matching inventory-pending paper may advance.");
        }
        return state with
        {
            Phase = "theory-deepening",
            CompletedCycles = state.CompletedCycles + 1,
            ConsecutiveNoProgressCycles = 0,
            LastProgressAt = advancedAt,
            StatusReason = $"theory inventory ready: {inventory.InventoryId}"
        };
    }

    public static void Validate(PaperTheoryScopeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireSchema(request.Schema, PaperTheoryFoundationSchemas.ScopeRequest);
        PaperTheoryScopeRequestContent c = request.RequestContent
            ?? throw new InvalidDataException("request_content is required.");
        RequireDigest(c.TheoryProgramRef, "theory_program_ref");
        RequireText(c.PaperId, "paper_id", 512);
        RequireExact(c.Phase, ScopePhase, "phase");
        ValidateContract(c.Contract);
        ParseUtc(c.RequestedAt, "requested_at");
        RequireIdentity(request.RequestId, c, nameof(request.RequestId));
    }

    public static void Validate(
        PaperTheoryScopeRequest request,
        PaperTheoryProgram program)
    {
        Validate(request);
        if (!string.Equals(
                request.RequestContent.TheoryProgramRef,
                program.TheoryProgramId,
                StringComparison.Ordinal)
            || !string.Equals(
                request.RequestContent.PaperId,
                program.ProgramContent.PaperId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Scope request does not address the supplied theory program.");
        }
        RequireContainsExactly(
            request.RequestContent.Contract.ExactInputRefs,
            [
                program.TheoryProgramId,
                program.ProgramContent.CandidatePaperRef,
                program.ProgramContent.LiteratureResearchRef,
                program.ProgramContent.IntuitionProposalRef,
                program.ProgramContent.PaperResearchInputRef
            ],
            "scope exact_input_refs");
    }

    public static void Validate(PaperTheoryScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        RequireSchema(scope.Schema, PaperTheoryFoundationSchemas.Scope);
        ValidateScopeContent(scope.ScopeContent);
        RequireIdentity(scope.ScopeId, scope.ScopeContent, nameof(scope.ScopeId));
    }

    public static void Validate(PaperTheoryScope scope, PaperTheoryProgram program)
    {
        Validate(scope);
        if (!string.Equals(
                scope.ScopeContent.TheoryProgramRef,
                program.TheoryProgramId,
                StringComparison.Ordinal)
            || !string.Equals(
                scope.ScopeContent.PaperId,
                program.ProgramContent.PaperId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Theory scope does not address the supplied theory program.");
        }
    }

    public static void Validate(PaperTheoryInventoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireSchema(request.Schema, PaperTheoryFoundationSchemas.InventoryRequest);
        PaperTheoryInventoryRequestContent c = request.RequestContent
            ?? throw new InvalidDataException("request_content is required.");
        RequireDigest(c.TheoryProgramRef, "theory_program_ref");
        RequireDigest(c.ScopeRef, "scope_ref");
        RequireText(c.PaperId, "paper_id", 512);
        RequireExact(c.Phase, InventoryPhase, "phase");
        ValidateContract(c.Contract);
        ParseUtc(c.RequestedAt, "requested_at");
        RequireIdentity(request.RequestId, c, nameof(request.RequestId));
    }

    public static void Validate(
        PaperTheoryInventoryRequest request,
        PaperTheoryProgram program,
        PaperTheoryScope scope)
    {
        Validate(request);
        if (!string.Equals(
                request.RequestContent.TheoryProgramRef,
                program.TheoryProgramId,
                StringComparison.Ordinal)
            || !string.Equals(
                request.RequestContent.ScopeRef,
                scope.ScopeId,
                StringComparison.Ordinal)
            || !string.Equals(
                request.RequestContent.PaperId,
                program.ProgramContent.PaperId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Inventory request does not address the supplied program and scope.");
        }
    }

    public static void Validate(PaperTheoryInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        RequireSchema(inventory.Schema, PaperTheoryFoundationSchemas.Inventory);
        ValidateInventoryContent(inventory.InventoryContent);
        RequireIdentity(
            inventory.InventoryId,
            inventory.InventoryContent,
            nameof(inventory.InventoryId));
    }

    private static void ValidateScopeContent(PaperTheoryScopeContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        RequireDigest(content.TheoryProgramRef, "theory_program_ref");
        RequireDigest(content.ScopeRequestRef, "scope_request_ref");
        RequireText(content.PaperId, "paper_id", 512);
        RequireText(content.ResearchQuestion, "research_question", 16384);
        RequireText(content.AbstractionTarget, "abstraction_target", 16384);
        RequireText(content.PublicationFloor, "publication_floor", 8192);
        RequireTextList(content.InScopeObligations, "in_scope_obligations", 16384, 1);
        RequireTextList(content.SupportingOnly, "supporting_only", 8192, 0);
        RequireTextList(content.OutOfScope, "out_of_scope", 8192, 0);
        RequireText(content.SplitPolicy, "split_policy", 8192);
        RequireTextList(
            content.CounterexampleObligations,
            "counterexample_obligations",
            8192,
            1);
        ParseUtc(content.CreatedAt, "created_at");
    }

    private static void ValidateInventoryContent(PaperTheoryInventoryContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        RequireDigest(content.TheoryProgramRef, "theory_program_ref");
        RequireDigest(content.ScopeRef, "scope_ref");
        RequireDigest(content.InventoryRequestRef, "inventory_request_ref");
        RequireText(content.PaperId, "paper_id", 512);
        if (content.Items is null || content.Items.Count < 3)
        {
            throw new InvalidDataException(
                "A paper theory inventory must contain at least three claim items.");
        }
        var byId = new Dictionary<string, PaperTheoryClaimInventoryItem>(
            StringComparer.Ordinal);
        int theoremLikeCount = 0;
        int internalEdgeCount = 0;
        foreach (PaperTheoryClaimInventoryItem item in content.Items)
        {
            ValidateItem(item);
            if (!byId.TryAdd(item.ClaimId, item))
            {
                throw new InvalidDataException("Inventory claim_id values must be unique.");
            }
            if (TheoremLikeKinds.Contains(item.Kind))
            {
                theoremLikeCount++;
            }
        }
        if (theoremLikeCount < 2)
        {
            throw new InvalidDataException(
                "A paper inventory must contain a series of at least two theorem-like claims.");
        }
        foreach (PaperTheoryClaimInventoryItem item in content.Items)
        {
            foreach (string dependency in item.Dependencies)
            {
                if (!byId.ContainsKey(dependency))
                {
                    throw new InvalidDataException(
                        $"Inventory dependency {dependency} does not resolve.");
                }
                internalEdgeCount++;
            }
        }
        if (internalEdgeCount == 0)
        {
            throw new InvalidDataException(
                "A paper inventory must contain an internal theorem dependency edge.");
        }
        RequireTextList(content.MainTheoremClaimIds, "main_theorem_claim_ids", 256, 1);
        foreach (string mainId in content.MainTheoremClaimIds)
        {
            if (!byId.TryGetValue(mainId, out PaperTheoryClaimInventoryItem item)
                || !string.Equals(item.Kind, "theorem", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Every main_theorem_claim_id must resolve to a theorem item.");
            }
        }
        EnsureAcyclic(byId);
        RequireTextList(content.MissingInterfaces, "missing_interfaces", 8192, 0);
        RequireTextList(content.StrongerVariants, "stronger_variants", 8192, 1);
        RequireTextList(content.WeakerVariants, "weaker_variants", 8192, 1);
        RequireTextList(
            content.CounterexampleObligations,
            "counterexample_obligations",
            8192,
            1);
        ParseUtc(content.CreatedAt, "created_at");
    }

    private static void ValidateItem(PaperTheoryClaimInventoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!ClaimIdPattern.IsMatch(item.ClaimId ?? string.Empty))
        {
            throw new InvalidDataException("claim_id is not canonical.");
        }
        RequireText(item.Title, "title", 1024);
        if (!ClaimKinds.Contains(item.Kind))
        {
            throw new InvalidDataException($"Unsupported inventory kind {item.Kind}.");
        }
        if (!ClaimStatuses.Contains(item.Status))
        {
            throw new InvalidDataException($"Unsupported inventory status {item.Status}.");
        }
        RequireText(item.Statement, "statement", 32768);
        RequireTextList(item.Dependencies, "dependencies", 256, 0);
        RequireText(item.RoleInArgument, "role_in_argument", 8192);
        RequireText(item.RequiredAction, "required_action", 8192);
    }

    private static void EnsureAcyclic(
        IReadOnlyDictionary<string, PaperTheoryClaimInventoryItem> byId)
    {
        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string id in byId.Keys)
        {
            Visit(id);
        }
        void Visit(string id)
        {
            if (state.TryGetValue(id, out int mark))
            {
                if (mark == 1)
                {
                    throw new InvalidDataException(
                        "Theory inventory dependency graph must be acyclic.");
                }
                return;
            }
            state[id] = 1;
            foreach (string dependency in byId[id].Dependencies)
            {
                Visit(dependency);
            }
            state[id] = 2;
        }
    }

    private static void ValidateContract(PaperCodexPhaseContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        RequireDigestList(contract.ExactInputRefs, "exact_input_refs", 1);
        RequireTextList(
            contract.PermittedArtifactFamilies,
            "permitted_artifact_families",
            512,
            1);
        RequireTextList(contract.ScientificTasks, "scientific_tasks", 8192, 1);
        RequireTextList(contract.ForbiddenShortcuts, "forbidden_shortcuts", 8192, 1);
        RequireTextList(
            contract.RequiredOutputSchemas,
            "required_output_schemas",
            512,
            1);
        RequireTextList(contract.PassConditions, "pass_conditions", 8192, 1);
        RequireTextList(contract.FailConditions, "fail_conditions", 8192, 1);
    }

    private static void RequireContainsExactly(
        IReadOnlyList<string> actual,
        IReadOnlyList<string> expected,
        string name)
    {
        if (actual.Count != expected.Count
            || !actual.OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(expected.OrderBy(value => value, StringComparer.Ordinal)))
        {
            throw new InvalidDataException($"{name} changed its exact evidence set.");
        }
    }

    private static void RequireDigestList(
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

    private static void RequireSchema(string actual, string expected) =>
        RequireExact(actual, expected, "schema");

    private static void RequireExact(string actual, string expected, string name)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{name} must be {expected}.");
        }
    }

    private static void RequireDigest(string value, string name)
    {
        if (!DigestPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException($"{name} must be sha256:<64 lowercase hex>.");
        }
    }

    private static void RequireText(string value, string name, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength)
        {
            throw new InvalidDataException(
                $"{name} must contain between 1 and {maximumLength} characters.");
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
