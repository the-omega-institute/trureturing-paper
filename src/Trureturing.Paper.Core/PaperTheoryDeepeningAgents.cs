using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Trureturing.Paper.Core;

internal sealed record PaperTheoryDeepeningAgentContext(
    PaperTheoryProgram Program,
    PaperTheoryScope Scope,
    PaperTheoryInventory Inventory,
    PaperTheoryDeepeningRequest Request,
    PaperTheoremPackage? PreviousPackage);

internal sealed record PaperTheoryDeepeningBaselineClaim(
    string ClaimId,
    string Kind,
    string Statement,
    IReadOnlyList<string> Dependencies,
    bool ProofComplete,
    string ProofStatus,
    IReadOnlyList<string> ProofOutline,
    string NoveltyStatus,
    bool LoadBearing);

public static class PaperTheoryDeepeningAgentService
{
    private const int MaximumControlBytes = 4 * 1024 * 1024;
    private const int MaximumArtifactBytes = 32 * 1024 * 1024;

    private static readonly Regex DigestPattern = new(
        "^sha256:[0-9a-f]{64}$",
        RegexOptions.CultureInvariant);
    private static readonly Regex PaperIdPattern = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$",
        RegexOptions.CultureInvariant);
    private static readonly Regex SchemaPattern = new(
        "^[a-z][a-z0-9.-]*\\.v[0-9]+$",
        RegexOptions.CultureInvariant);
    private static readonly Regex RelativePathPattern = new(
        "^[A-Za-z0-9._+@=-]+(?:/[A-Za-z0-9._+@=-]+)*$",
        RegexOptions.CultureInvariant);
    private static readonly HashSet<string> AllowedEvidenceRoots = new(
        ["artifacts", "Papers", "work", "contracts", "docs", "src", "tools", "tests"],
        StringComparer.Ordinal);
    private static readonly HashSet<string> CompleteProofStatuses = new(
        ["informal-complete", "certified-foundation"],
        StringComparer.Ordinal);
    private static readonly HashSet<string> TheoremLikeKinds = new(
        ["lemma", "proposition", "theorem", "corollary"],
        StringComparer.Ordinal);

    public static PaperTheoryDeepeningAgentTaskStaged StageTask(
        string repositoryRoot,
        string dispatchPath)
    {
        string root = RequireRepositoryRoot(repositoryRoot);
        string fullDispatchPath = RequireDispatchPath(root, dispatchPath);
        byte[] dispatchBytes = ReadBoundedFile(
            fullDispatchPath,
            MaximumControlBytes,
            "Theory-deepening dispatch");
        string dispatchRef = Reference(dispatchBytes);
        PaperTheoryDeepeningAgentDispatch dispatch =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryDeepeningAgentDispatch>(
                dispatchBytes);
        Validate(dispatch);

        string immutableDispatchPath = ArtifactPath(
            root,
            "dispatches",
            dispatchRef);
        _ = PutImmutable(immutableDispatchPath, dispatchBytes);
        string dispatchRelativePath = RelativePath(root, immutableDispatchPath);
        PaperTheoryDeepeningAgentContext context = LoadContext(root, dispatch);
        PaperAgentTask task = BuildTask(
            root,
            dispatch,
            dispatchRef,
            dispatchRelativePath,
            context);
        PaperAgentRuntimeService.Validate(task);

        byte[] taskBytes = CanonicalJson.Serialize(task);
        string taskRef = Reference(taskBytes);
        string stagedPath = Path.Combine(
            root,
            "inbox",
            "agent-tasks",
            $"theory-deepening-{Hex(taskRef)}.json");
        bool replayed = PutImmutable(stagedPath, taskBytes);
        PaperAgentProfile profile = PaperAgentRuntimeService.GetProfile(task.Phase);
        return new PaperTheoryDeepeningAgentTaskStaged(
            PaperTheoryDeepeningAgentSchemas.TaskStaged,
            dispatchRef,
            taskRef,
            stagedPath,
            dispatch.PaperId,
            dispatch.TheoryProgramRef,
            dispatch.RequestRef,
            context.Request.RequestContent.Round,
            task.Phase,
            profile.AgentRole,
            profile.ContextMode,
            replayed);
    }

    public static PaperTheoryDeepeningAgentResultAdmitted AdmitResult(
        string repositoryRoot,
        string taskRef)
    {
        string root = RequireRepositoryRoot(repositoryRoot);
        RequireDigest(taskRef, nameof(taskRef));
        PaperAgentTask task = ReadRegisteredTask(root, taskRef);
        if (!string.Equals(task.Phase, "theory-deepening", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Only an A2 theory-deepening task can enter this admission bridge.");
        }

        PaperAgentTaskCursor agentCursor = ReadAgentCursor(root, task, taskRef);
        PaperAgentResultWire result = ReadAgentResult(
            root,
            task,
            taskRef,
            agentCursor.ResultRef);
        RequireCursorMatchesResult(agentCursor, result);
        if (!string.Equals(result.Status, "completed", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Only a completed theory-deepening result can be admitted.");
        }

        PaperAgentInputArtifact dispatchInput = task.ExactInputs
            .SingleOrDefault(input => string.Equals(
                input.Schema,
                PaperTheoryDeepeningAgentSchemas.Dispatch,
                StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                "Theory-deepening task is missing its immutable dispatch input.");
        byte[] dispatchBytes = ReadExactInput(root, dispatchInput);
        string dispatchRef = Reference(dispatchBytes);
        PaperTheoryDeepeningAgentDispatch dispatch =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryDeepeningAgentDispatch>(
                dispatchBytes);
        Validate(dispatch);
        PaperTheoryDeepeningAgentContext context = LoadContext(root, dispatch);
        ValidateTaskBinding(
            root,
            task,
            dispatch,
            dispatchRef,
            dispatchInput.RepositoryRelativePath,
            context);

        string cursorPath = AdmissionCursorPath(root, taskRef);
        if (File.Exists(cursorPath))
        {
            return ReplayAdmission(
                root,
                ReadAdmissionCursor(cursorPath),
                taskRef,
                agentCursor,
                dispatch,
                dispatchRef);
        }

        if (agentCursor.Outputs.Count != 1)
        {
            throw new InvalidDataException(
                "A completed theory-deepening result must contain exactly one draft bundle.");
        }
        PaperAgentStoredOutput output = agentCursor.Outputs[0];
        if (!string.Equals(
                output.Schema,
                PaperTheoryDeepeningAgentSchemas.Draft,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Theory-deepening output has the wrong draft schema.");
        }
        byte[] draftBytes = ReadAgentOutput(root, output.ArtifactRef);
        PaperTheoryDeepeningDraft draft =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryDeepeningDraft>(
                draftBytes);
        ValidateDraft(draft, context, dispatch);

        PaperTheoryIteration iteration = CreateIteration(context, draft);
        PaperTheoremPackage package = CreatePackage(context, draft, iteration);
        PaperTheoryDeepeningDelta delta = ComputeDelta(
            context,
            draft,
            iteration,
            package);
        IReadOnlyList<PaperCandidateSplitProposal> splitProposals =
            CreateSplitProposals(draft, package);
        IReadOnlyList<PaperResearchLedgerEntry> ledgerEntries =
            CreateLedgerEntries(draft, package);
        ValidatePortfolioRoutes(draft, package, splitProposals, ledgerEntries);

        string expectedNextRoute = string.Equals(
            package.TheoremPackageContent.Maturity,
            "audit-candidate",
            StringComparison.Ordinal)
            ? "theory-audit"
            : "theory-deepening";
        if (!string.Equals(result.NextRoute, expectedNextRoute, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"A completed A2 result with maturity {package.TheoremPackageContent.Maturity} must route to {expectedNextRoute}.");
        }

        PaperTheoryDeepeningStoredArtifact storedIteration = StoreDomain(
            root,
            "iterations",
            iteration.Schema,
            iteration.IterationId,
            iteration.IterationContent,
            iteration);
        PaperTheoryDeepeningStoredArtifact storedPackage = StoreDomain(
            root,
            "theorem-packages",
            package.Schema,
            package.TheoremPackageId,
            package.TheoremPackageContent,
            package);
        PaperTheoryDeepeningStoredArtifact storedDelta = StoreDomain(
            root,
            "deltas",
            delta.Schema,
            delta.DeltaId,
            delta.DeltaContent,
            delta);
        PaperTheoryDeepeningStoredArtifact[] storedSplits = splitProposals
            .Select(proposal => StoreDomain(
                root,
                "split-proposals",
                proposal.Schema,
                proposal.ProposalId,
                proposal.ProposalContent,
                proposal))
            .OrderBy(value => value.ArtifactRef, StringComparer.Ordinal)
            .ToArray();
        PaperTheoryDeepeningStoredArtifact[] storedLedger = ledgerEntries
            .Select(entry => StoreDomain(
                root,
                "research-ledger",
                entry.Schema,
                entry.EntryId,
                entry.EntryContent,
                entry))
            .OrderBy(value => value.ArtifactRef, StringComparer.Ordinal)
            .ToArray();
        string[] mergeCandidates = NormalizeMergeCandidateIds(
            draft.Iteration.MergeCandidatePaperIds,
            dispatch.PaperId);

        var cursor = new PaperTheoryDeepeningAgentAdmissionCursor(
            PaperTheoryDeepeningAgentSchemas.AdmissionCursor,
            taskRef,
            agentCursor.ResultRef,
            dispatchRef,
            dispatch.PaperId,
            dispatch.TheoryProgramRef,
            dispatch.RequestRef,
            context.Request.RequestContent.Round,
            storedIteration,
            storedPackage,
            storedDelta,
            storedSplits,
            storedLedger,
            mergeCandidates,
            package.TheoremPackageContent.Maturity,
            expectedNextRoute,
            agentCursor.RunId,
            agentCursor.Provenance,
            result.CompletedAt);
        Validate(cursor);
        byte[] cursorBytes = CanonicalJson.Serialize(cursor);
        Directory.CreateDirectory(Path.GetDirectoryName(cursorPath)!);
        try
        {
            PaperResearchInputStore.WriteAtomic(
                cursorPath,
                cursorBytes,
                overwrite: false);
        }
        catch (IOException) when (File.Exists(cursorPath))
        {
            return ReplayAdmission(
                root,
                ReadAdmissionCursor(cursorPath),
                taskRef,
                agentCursor,
                dispatch,
                dispatchRef);
        }
        return Recorded(cursor, replayed: false);
    }

    public static void Validate(PaperTheoryDeepeningAgentDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        RequireExact(
            dispatch.Schema,
            PaperTheoryDeepeningAgentSchemas.Dispatch,
            nameof(dispatch.Schema));
        RequirePaperId(dispatch.PaperId);
        RequireDigest(dispatch.TheoryProgramRef, nameof(dispatch.TheoryProgramRef));
        RequireDigest(dispatch.RequestRef, nameof(dispatch.RequestRef));
        if (dispatch.ExactInputs is null
            || dispatch.ExactInputs.Count is < 4 or > 64)
        {
            throw new InvalidDataException(
                "Theory-deepening dispatch must contain between four and sixty-four exact inputs.");
        }
        var refs = new HashSet<string>(StringComparer.Ordinal);
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (PaperAgentInputArtifact input in dispatch.ExactInputs)
        {
            ArgumentNullException.ThrowIfNull(input);
            RequireSchema(input.Schema, nameof(input.Schema));
            RequireDigest(input.ArtifactRef, nameof(input.ArtifactRef));
            RequireRepositoryRelativePath(
                input.RepositoryRelativePath,
                nameof(input.RepositoryRelativePath));
            if (!refs.Add(input.ArtifactRef) || !paths.Add(input.RepositoryRelativePath))
            {
                throw new InvalidDataException(
                    "Theory-deepening exact input refs and paths must be unique.");
            }
        }
        if (!refs.Contains(dispatch.TheoryProgramRef)
            || !refs.Contains(dispatch.RequestRef))
        {
            throw new InvalidDataException(
                "Theory-deepening dispatch must include its program and request content artifacts.");
        }
        ParseUtc(dispatch.RequestedAt, nameof(dispatch.RequestedAt));
    }

    public static void Validate(PaperTheoryDeepeningDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);
        RequireExact(delta.Schema, PaperTheoryDeepeningAgentSchemas.Delta, nameof(delta.Schema));
        PaperTheoryDeepeningDeltaContent content = delta.DeltaContent
            ?? throw new InvalidDataException("delta_content is required.");
        RequireDigest(content.DeepeningRequestRef, nameof(content.DeepeningRequestRef));
        if (content.BaselineSchema is not PaperTheoryFoundationSchemas.Inventory
            and not PaperTheoryDeepeningSchemas.TheoremPackage)
        {
            throw new InvalidDataException("A2 delta baseline schema is unsupported.");
        }
        RequireDigest(content.BaselineRef, nameof(content.BaselineRef));
        RequireDigest(content.IterationRef, nameof(content.IterationRef));
        RequireDigest(content.TheoremPackageRef, nameof(content.TheoremPackageRef));
        RequireClaimIds(content.NewClaimIds, nameof(content.NewClaimIds));
        RequireClaimIds(content.StrengthenedClaimIds, nameof(content.StrengthenedClaimIds));
        RequireClaimIds(content.RetiredClaimIds, nameof(content.RetiredClaimIds));
        if (content.DependencyEdgesAdded < 0
            || content.ProofObligationsClosed < 1
            || content.CounterexamplesResolved < 0
            || content.SubstantiveDimensions is null
            || content.SubstantiveDimensions.Count < 3
            || !content.Passed)
        {
            throw new InvalidDataException(
                "A2 computed delta does not demonstrate substantive progress.");
        }
        RequireTextList(
            content.SubstantiveDimensions,
            nameof(content.SubstantiveDimensions),
            256,
            3);
        ParseUtc(content.ComputedAt, nameof(content.ComputedAt));
        RequireIdentity(delta.DeltaId, content, nameof(delta.DeltaId));
    }

    public static void Validate(PaperTheoryDeepeningAgentAdmissionCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        RequireExact(
            cursor.Schema,
            PaperTheoryDeepeningAgentSchemas.AdmissionCursor,
            nameof(cursor.Schema));
        RequireDigest(cursor.TaskRef, nameof(cursor.TaskRef));
        RequireDigest(cursor.ResultRef, nameof(cursor.ResultRef));
        RequireDigest(cursor.DispatchRef, nameof(cursor.DispatchRef));
        RequirePaperId(cursor.PaperId);
        RequireDigest(cursor.TheoryProgramRef, nameof(cursor.TheoryProgramRef));
        RequireDigest(cursor.RequestRef, nameof(cursor.RequestRef));
        if (cursor.Round < 1)
        {
            throw new InvalidDataException("A2 admission round must be positive.");
        }
        ValidateStoredCoordinate(cursor.Iteration, PaperTheoryDeepeningSchemas.TheoryIteration);
        ValidateStoredCoordinate(cursor.TheoremPackage, PaperTheoryDeepeningSchemas.TheoremPackage);
        ValidateStoredCoordinate(cursor.Delta, PaperTheoryDeepeningAgentSchemas.Delta);
        ValidateStoredCoordinates(
            cursor.SplitProposals,
            PaperTheoryDeepeningSchemas.SplitProposal,
            nameof(cursor.SplitProposals));
        ValidateStoredCoordinates(
            cursor.ResearchLedgerEntries,
            PaperTheoryDeepeningSchemas.ResearchLedgerEntry,
            nameof(cursor.ResearchLedgerEntries),
            minimum: 1);
        string[] normalizedMerge = NormalizeMergeCandidateIds(
            cursor.MergeCandidatePaperIds,
            cursor.PaperId);
        if (!normalizedMerge.SequenceEqual(cursor.MergeCandidatePaperIds, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "A2 admission merge candidate IDs are not canonical and sorted.");
        }
        if (cursor.Maturity is not "developing" and not "audit-candidate")
        {
            throw new InvalidDataException("A2 admission maturity is invalid.");
        }
        string expectedRoute = cursor.Maturity == "audit-candidate"
            ? "theory-audit"
            : "theory-deepening";
        RequireExact(cursor.NextRoute, expectedRoute, nameof(cursor.NextRoute));
        RequireRunId(cursor.RunId);
        if (cursor.Provenance is not "produced" and not "adopted")
        {
            throw new InvalidDataException("A2 admission provenance is invalid.");
        }
        ParseUtc(cursor.AdmittedAt, nameof(cursor.AdmittedAt));
    }

    private static PaperAgentTask BuildTask(
        string root,
        PaperTheoryDeepeningAgentDispatch dispatch,
        string dispatchRef,
        string dispatchRelativePath,
        PaperTheoryDeepeningAgentContext context)
    {
        ValidateInputSources(root, dispatch.ExactInputs);
        PaperAgentProfile profile = PaperAgentRuntimeService.GetProfile("theory-deepening");
        return new PaperAgentTask(
            PaperAgentSchemas.Task,
            dispatch.PaperId,
            dispatch.TheoryProgramRef,
            profile.Phase,
            profile.AgentRole,
            profile.ContextMode,
            TaskInputs(dispatch, dispatchRef, dispatchRelativePath),
            [new PaperAgentExpectedOutput(
                PaperTheoryDeepeningAgentSchemas.Draft,
                "outputs/theory-deepening-draft.json")],
            ["theory-deepening", "theory-audit", "blocked"],
            BuildInstruction(context.Request),
            TaskForbiddenShortcuts(context.Request.RequestContent.Contract),
            dispatch.RequestedAt);
    }

    private static PaperAgentInputArtifact[] TaskInputs(
        PaperTheoryDeepeningAgentDispatch dispatch,
        string dispatchRef,
        string dispatchRelativePath) =>
        dispatch.ExactInputs
            .Append(new PaperAgentInputArtifact(
                PaperTheoryDeepeningAgentSchemas.Dispatch,
                dispatchRef,
                dispatchRelativePath))
            .OrderBy(input => input.Schema, StringComparer.Ordinal)
            .ThenBy(input => input.ArtifactRef, StringComparer.Ordinal)
            .ToArray();

    private static string[] TaskForbiddenShortcuts(PaperCodexPhaseContract contract) =>
        contract.ForbiddenShortcuts
            .Append("Do not compute or invent iteration, theorem-package, split-proposal, or ledger identifiers; repository validation owns canonical identity.")
            .Append("Do not emit final domain envelopes; write only the declared paper-theory-deepening-draft.v1 bundle.")
            .Append("Do not claim progress counters that are not witnessed by the returned theorem-package delta.")
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static string BuildInstruction(PaperTheoryDeepeningRequest request)
    {
        PaperTheoryDeepeningRequestContent content = request.RequestContent;
        var builder = new StringBuilder();
        builder.AppendLine("Execute one bounded A2 abstract-theory deepening round from the exact supplied paper foundation.");
        builder.AppendLine("Write exactly one paper-theory-deepening-draft.v1 object to outputs/theory-deepening-draft.json.");
        builder.AppendLine($"Use theory_program_ref={content.TheoryProgramRef}.");
        builder.AppendLine($"Use scope_ref={content.ScopeRef}.");
        builder.AppendLine($"Use inventory_ref={content.InventoryRef}.");
        builder.AppendLine($"Use deepening_request_ref={request.RequestId}.");
        builder.AppendLine($"Use paper_id={content.PaperId} and round={content.Round}.");
        builder.AppendLine("Repeat prior_theorem_package_refs exactly as supplied.");
        builder.AppendLine("The iteration draft must identify every new, strengthened, retired, and otherwise changed claim and supply a multi-step proof spine.");
        builder.AppendLine("The theorem-package draft must contain the coherent post-iteration claim DAG, maturity, main theorem, corollary, sharpness, proof status, novelty boundary, and publication significance.");
        builder.AppendLine("Add one research-ledger draft for the prior-work boundary, plus typed entries for every split, merge, or counterexample route used in this round.");
        builder.AppendLine("The repository independently computes the actual theorem delta and rejects self-reported progress that the returned package does not witness.");
        AppendContract(builder, content.Contract);
        return builder.ToString();
    }

    private static void AppendContract(
        StringBuilder builder,
        PaperCodexPhaseContract contract)
    {
        builder.AppendLine("Scientific tasks:");
        foreach (string value in contract.ScientificTasks)
        {
            builder.AppendLine($"- {value}");
        }
        builder.AppendLine("Pass conditions:");
        foreach (string value in contract.PassConditions)
        {
            builder.AppendLine($"- {value}");
        }
        builder.AppendLine("Fail conditions:");
        foreach (string value in contract.FailConditions)
        {
            builder.AppendLine($"- {value}");
        }
    }

    private static PaperTheoryDeepeningAgentContext LoadContext(
        string root,
        PaperTheoryDeepeningAgentDispatch dispatch)
    {
        ValidateInputSources(root, dispatch.ExactInputs);
        PaperTheoryProgram program = ReadProgram(root, dispatch);
        PaperTheoryScope scope = ReadScope(root, dispatch, program);
        PaperTheoryInventory inventory = ReadInventory(root, dispatch, program, scope);
        PaperAgentInputArtifact requestInput = RequiredInput(
            dispatch.ExactInputs,
            PaperTheoryDeepeningSchemas.DeepeningRequest,
            dispatch.RequestRef,
            "deepening request");
        PaperTheoryDeepeningRequestContent requestContent =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryDeepeningRequestContent>(
                ReadExactInput(root, requestInput));
        var request = new PaperTheoryDeepeningRequest(
            PaperTheoryDeepeningSchemas.DeepeningRequest,
            dispatch.RequestRef,
            requestContent);
        PaperTheoryDeepeningService.Validate(request);
        PaperTheoremPackage? previous = request.RequestContent.PriorTheoremPackageRefs.Count == 0
            ? null
            : ReadPreviousPackage(
                root,
                dispatch,
                request.RequestContent.PriorTheoremPackageRefs[0]);
        PaperTheoryDeepeningRequest expected =
            PaperTheoryDeepeningService.CreateDeepeningRequest(
                program,
                scope,
                inventory,
                previous,
                request.RequestContent.Round,
                request.RequestContent.RequestedAt);
        if (!CanonicalJson.Serialize(expected.RequestContent).AsSpan().SequenceEqual(
                CanonicalJson.Serialize(request.RequestContent))
            || !string.Equals(expected.RequestId, request.RequestId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Theory-deepening request changed its repository-owned phase contract.");
        }
        if (!string.Equals(dispatch.PaperId, program.ProgramContent.PaperId, StringComparison.Ordinal)
            || !string.Equals(dispatch.TheoryProgramRef, program.TheoryProgramId, StringComparison.Ordinal)
            || !string.Equals(dispatch.RequestRef, request.RequestId, StringComparison.Ordinal)
            || !string.Equals(dispatch.RequestedAt, request.RequestContent.RequestedAt, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Theory-deepening dispatch changed its domain request identity or timestamp.");
        }
        RequireInputRefsExactly(
            dispatch.ExactInputs,
            request.RequestContent.Contract.ExactInputRefs.Append(request.RequestId));
        return new(program, scope, inventory, request, previous);
    }

    private static PaperTheoryProgram ReadProgram(
        string root,
        PaperTheoryDeepeningAgentDispatch dispatch)
    {
        PaperAgentInputArtifact input = RequiredInput(
            dispatch.ExactInputs,
            PaperPortfolioSchemas.TheoryProgram,
            dispatch.TheoryProgramRef,
            "theory program");
        PaperTheoryProgramContent content =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryProgramContent>(
                ReadExactInput(root, input));
        var program = new PaperTheoryProgram(
            PaperPortfolioSchemas.TheoryProgram,
            dispatch.TheoryProgramRef,
            content);
        PaperPortfolioService.Validate(program);
        return program;
    }

    private static PaperTheoryScope ReadScope(
        string root,
        PaperTheoryDeepeningAgentDispatch dispatch,
        PaperTheoryProgram program)
    {
        PaperAgentInputArtifact input = dispatch.ExactInputs.SingleOrDefault(value =>
            string.Equals(value.Schema, PaperTheoryFoundationSchemas.Scope, StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                "Theory-deepening dispatch is missing its exact A0 scope content.");
        PaperTheoryScopeContent content =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryScopeContent>(
                ReadExactInput(root, input));
        var scope = new PaperTheoryScope(
            PaperTheoryFoundationSchemas.Scope,
            input.ArtifactRef,
            content);
        PaperTheoryFoundationService.Validate(scope, program);
        return scope;
    }

    private static PaperTheoryInventory ReadInventory(
        string root,
        PaperTheoryDeepeningAgentDispatch dispatch,
        PaperTheoryProgram program,
        PaperTheoryScope scope)
    {
        PaperAgentInputArtifact input = dispatch.ExactInputs.SingleOrDefault(value =>
            string.Equals(value.Schema, PaperTheoryFoundationSchemas.Inventory, StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                "Theory-deepening dispatch is missing its exact A1 inventory content.");
        PaperTheoryInventoryContent content =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryInventoryContent>(
                ReadExactInput(root, input));
        var inventory = new PaperTheoryInventory(
            PaperTheoryFoundationSchemas.Inventory,
            input.ArtifactRef,
            content);
        PaperTheoryFoundationService.Validate(inventory);
        if (!string.Equals(inventory.InventoryContent.TheoryProgramRef, program.TheoryProgramId, StringComparison.Ordinal)
            || !string.Equals(inventory.InventoryContent.ScopeRef, scope.ScopeId, StringComparison.Ordinal)
            || !string.Equals(inventory.InventoryContent.PaperId, program.ProgramContent.PaperId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Theory inventory does not belong to the supplied program and scope.");
        }
        return inventory;
    }

    private static PaperTheoremPackage ReadPreviousPackage(
        string root,
        PaperTheoryDeepeningAgentDispatch dispatch,
        string packageRef)
    {
        PaperAgentInputArtifact input = RequiredInput(
            dispatch.ExactInputs,
            PaperTheoryDeepeningSchemas.TheoremPackage,
            packageRef,
            "prior theorem package");
        PaperTheoremPackageContent content =
            PaperResearchInputJson.DeserializeStrict<PaperTheoremPackageContent>(
                ReadExactInput(root, input));
        var package = new PaperTheoremPackage(
            PaperTheoryDeepeningSchemas.TheoremPackage,
            packageRef,
            content);
        PaperTheoryDeepeningService.Validate(package);
        return package;
    }

    private static void ValidateDraft(
        PaperTheoryDeepeningDraft draft,
        PaperTheoryDeepeningAgentContext context,
        PaperTheoryDeepeningAgentDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(draft);
        RequireExact(draft.Schema, PaperTheoryDeepeningAgentSchemas.Draft, nameof(draft.Schema));
        if (!string.Equals(draft.TheoryProgramRef, dispatch.TheoryProgramRef, StringComparison.Ordinal)
            || !string.Equals(draft.ScopeRef, context.Scope.ScopeId, StringComparison.Ordinal)
            || !string.Equals(draft.InventoryRef, context.Inventory.InventoryId, StringComparison.Ordinal)
            || !string.Equals(draft.DeepeningRequestRef, dispatch.RequestRef, StringComparison.Ordinal)
            || !draft.PriorTheoremPackageRefs.SequenceEqual(
                context.Request.RequestContent.PriorTheoremPackageRefs,
                StringComparer.Ordinal)
            || !string.Equals(draft.PaperId, dispatch.PaperId, StringComparison.Ordinal)
            || draft.Round != context.Request.RequestContent.Round)
        {
            throw new InvalidDataException(
                "Theory-deepening draft changed its program, foundation, request, prior package, paper, or round.");
        }
        ArgumentNullException.ThrowIfNull(draft.Iteration);
        ArgumentNullException.ThrowIfNull(draft.TheoremPackage);
        if (draft.SplitProposals is null || draft.SplitProposals.Count > 16)
        {
            throw new InvalidDataException(
                "A2 draft split_proposals must contain between zero and sixteen entries.");
        }
        if (draft.ResearchLedgerEntries is null
            || draft.ResearchLedgerEntries.Count is < 1 or > 32)
        {
            throw new InvalidDataException(
                "Every A2 round must return between one and thirty-two research-ledger drafts.");
        }
        RequireNotBefore(
            draft.Iteration.CreatedAt,
            context.Request.RequestContent.RequestedAt,
            "iteration created_at");
        RequireNotBefore(
            draft.TheoremPackage.CreatedAt,
            draft.Iteration.CreatedAt,
            "theorem-package created_at");
        RequireNotBefore(
            draft.CreatedAt,
            draft.TheoremPackage.CreatedAt,
            "deepening draft created_at");
    }

    private static PaperTheoryIteration CreateIteration(
        PaperTheoryDeepeningAgentContext context,
        PaperTheoryDeepeningDraft draft)
    {
        PaperTheoryIterationDraft value = draft.Iteration;
        var content = new PaperTheoryIterationContent(
            context.Program.TheoryProgramId,
            context.Scope.ScopeId,
            context.Inventory.InventoryId,
            context.Request.RequestId,
            context.Request.RequestContent.PriorTheoremPackageRefs,
            context.Program.ProgramContent.PaperId,
            context.Request.RequestContent.Round,
            value.ChangedClaimIds,
            value.NewClaimIds,
            value.StrengthenedClaimIds,
            value.RetiredClaimIds,
            value.ProofSpine,
            value.NovelIncrement,
            value.PriorWorkBoundary,
            value.CounterexampleFindings,
            value.SplitCandidateClaimIds,
            value.MergeCandidatePaperIds,
            value.ProgressEvidence,
            value.CreatedAt);
        var iteration = new PaperTheoryIteration(
            PaperTheoryDeepeningSchemas.TheoryIteration,
            Identity(content),
            content);
        PaperTheoryDeepeningService.Validate(iteration);
        return iteration;
    }

    private static PaperTheoremPackage CreatePackage(
        PaperTheoryDeepeningAgentContext context,
        PaperTheoryDeepeningDraft draft,
        PaperTheoryIteration iteration)
    {
        PaperTheoremPackageDraft value = draft.TheoremPackage;
        var content = new PaperTheoremPackageContent(
            context.Program.TheoryProgramId,
            context.Scope.ScopeId,
            context.Inventory.InventoryId,
            iteration.IterationId,
            context.Program.ProgramContent.PaperId,
            context.Request.RequestContent.Round,
            value.Maturity,
            value.Claims,
            value.MainTheoremClaimIds,
            value.CorollaryClaimIds,
            value.SharpnessClaimIds,
            value.OpenProofObligations,
            value.KnownResultsToCite,
            value.NoveltySummary,
            value.PublicationSignificance,
            value.CreatedAt);
        return PaperTheoryDeepeningService.CreateTheoremPackage(
            context.Program,
            context.Scope,
            context.Inventory,
            iteration,
            content);
    }

    private static PaperTheoryDeepeningDelta ComputeDelta(
        PaperTheoryDeepeningAgentContext context,
        PaperTheoryDeepeningDraft draft,
        PaperTheoryIteration iteration,
        PaperTheoremPackage package)
    {
        IReadOnlyDictionary<string, PaperTheoryDeepeningBaselineClaim> baseline =
            BaselineClaims(context);
        var current = package.TheoremPackageContent.Claims.ToDictionary(
            claim => claim.ClaimId,
            StringComparer.Ordinal);
        string[] actualNew = current.Keys
            .Where(id => !baseline.ContainsKey(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        string[] actualRetired = baseline.Keys
            .Where(id => !current.ContainsKey(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        string[] actualStrengthened = baseline.Keys
            .Where(current.ContainsKey)
            .Where(id => TheoremLikeKinds.Contains(current[id].Kind))
            .Where(id => MateriallyChanged(baseline[id], current[id]))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        string[] actualChanged = baseline.Keys
            .Where(current.ContainsKey)
            .Where(id => MateriallyChanged(baseline[id], current[id]))
            .Concat(actualNew)
            .Concat(actualRetired)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        RequireSameSet(iteration.IterationContent.NewClaimIds, actualNew, "new_claim_ids");
        RequireSameSet(iteration.IterationContent.RetiredClaimIds, actualRetired, "retired_claim_ids");
        RequireSameSet(
            iteration.IterationContent.StrengthenedClaimIds,
            actualStrengthened,
            "strengthened_claim_ids");
        RequireSameSet(
            iteration.IterationContent.ChangedClaimIds,
            actualChanged,
            "changed_claim_ids");
        if (actualNew.Any(id => !TheoremLikeKinds.Contains(current[id].Kind)))
        {
            throw new InvalidDataException(
                "A2 new_claim_ids must contain theorem-like claims because the progress contract counts them as theorem-like results.");
        }

        int addedEdges = CountAddedEdges(baseline, current);
        int proofClosures = CountProofClosures(context, baseline, current, package);
        int counterexamplesResolved = CountCounterexamplesResolved(draft, package);
        bool abstractionChanged = baseline.Keys
            .Where(current.ContainsKey)
            .Any(id => string.Equals(current[id].Kind, "definition", StringComparison.Ordinal)
                && MateriallyChanged(baseline[id], current[id]))
            || actualNew.Any(id => string.Equals(current[id].Kind, "definition", StringComparison.Ordinal));
        bool noveltyBoundaryChanged = context.PreviousPackage is null
            ? actualNew.Length + actualStrengthened.Length > 0
            : !string.Equals(
                context.PreviousPackage.TheoremPackageContent.NoveltySummary,
                package.TheoremPackageContent.NoveltySummary,
                StringComparison.Ordinal);

        PaperTheoryProgressEvidence reported = iteration.IterationContent.ProgressEvidence;
        if (reported.NewTheoremLikeClaims != actualNew.Length
            || reported.StrengthenedTheoremLikeClaims != actualStrengthened.Length
            || reported.DependencyEdgesAdded != addedEdges
            || reported.ProofObligationsClosed > proofClosures
            || reported.CounterexamplesResolved != counterexamplesResolved
            || reported.AbstractionChanged != abstractionChanged
            || reported.NoveltyBoundaryChanged != noveltyBoundaryChanged)
        {
            throw new InvalidDataException(
                "A2 self-reported progress does not match the repository-computed theorem-package delta.");
        }

        var dimensions = new List<string>();
        if (actualNew.Length > 0) dimensions.Add("new-theorem-like-claims");
        if (actualStrengthened.Length > 0) dimensions.Add("strengthened-theorem-like-claims");
        if (addedEdges > 0) dimensions.Add("dependency-structure");
        if (proofClosures > 0) dimensions.Add("proof-closure");
        if (counterexamplesResolved > 0) dimensions.Add("counterexample-or-sharpness");
        if (abstractionChanged) dimensions.Add("abstraction");
        if (noveltyBoundaryChanged) dimensions.Add("novelty-boundary");
        if (actualNew.Length + actualStrengthened.Length < 1
            || proofClosures < 1
            || addedEdges + counterexamplesResolved < 1
                && !abstractionChanged
                && !noveltyBoundaryChanged
            || dimensions.Count < 3)
        {
            throw new InvalidDataException(
                "A2 repository-computed delta is a fake extension without theorem, proof, and structural progress.");
        }

        string baselineSchema = context.PreviousPackage is null
            ? PaperTheoryFoundationSchemas.Inventory
            : PaperTheoryDeepeningSchemas.TheoremPackage;
        string baselineRef = context.PreviousPackage?.TheoremPackageId
            ?? context.Inventory.InventoryId;
        var content = new PaperTheoryDeepeningDeltaContent(
            context.Request.RequestId,
            baselineSchema,
            baselineRef,
            iteration.IterationId,
            package.TheoremPackageId,
            actualNew,
            actualStrengthened,
            actualRetired,
            addedEdges,
            proofClosures,
            counterexamplesResolved,
            abstractionChanged,
            noveltyBoundaryChanged,
            dimensions,
            true,
            draft.CreatedAt);
        var delta = new PaperTheoryDeepeningDelta(
            PaperTheoryDeepeningAgentSchemas.Delta,
            Identity(content),
            content);
        Validate(delta);
        return delta;
    }

    private static IReadOnlyDictionary<string, PaperTheoryDeepeningBaselineClaim> BaselineClaims(
        PaperTheoryDeepeningAgentContext context)
    {
        if (context.PreviousPackage is not null)
        {
            return context.PreviousPackage.TheoremPackageContent.Claims.ToDictionary(
                claim => claim.ClaimId,
                claim => new PaperTheoryDeepeningBaselineClaim(
                    claim.ClaimId,
                    claim.Kind,
                    claim.Statement,
                    claim.Dependencies,
                    CompleteProofStatuses.Contains(claim.ProofStatus),
                    claim.ProofStatus,
                    claim.ProofOutline,
                    claim.NoveltyStatus,
                    claim.LoadBearing),
                StringComparer.Ordinal);
        }
        return context.Inventory.InventoryContent.Items.ToDictionary(
            item => item.ClaimId,
            item => new PaperTheoryDeepeningBaselineClaim(
                item.ClaimId,
                item.Kind,
                item.Statement,
                item.Dependencies,
                string.Equals(item.Status, "certified-foundation", StringComparison.Ordinal),
                item.Status,
                [],
                "inventory-baseline",
                string.Equals(item.Kind, "theorem", StringComparison.Ordinal)),
            StringComparer.Ordinal);
    }

    private static bool MateriallyChanged(
        PaperTheoryDeepeningBaselineClaim baseline,
        PaperTheoremPackageClaim current) =>
        !string.Equals(baseline.Kind, current.Kind, StringComparison.Ordinal)
        || !string.Equals(baseline.Statement, current.Statement, StringComparison.Ordinal)
        || !baseline.Dependencies.SequenceEqual(current.Dependencies, StringComparer.Ordinal)
        || baseline.ProofComplete != CompleteProofStatuses.Contains(current.ProofStatus)
        || !string.Equals(baseline.ProofStatus, current.ProofStatus, StringComparison.Ordinal)
        || !baseline.ProofOutline.SequenceEqual(current.ProofOutline, StringComparer.Ordinal)
        || !string.Equals(baseline.NoveltyStatus, current.NoveltyStatus, StringComparison.Ordinal)
        || baseline.LoadBearing != current.LoadBearing;

    private static int CountAddedEdges(
        IReadOnlyDictionary<string, PaperTheoryDeepeningBaselineClaim> baseline,
        IReadOnlyDictionary<string, PaperTheoremPackageClaim> current)
    {
        var oldEdges = baseline.Values
            .SelectMany(claim => claim.Dependencies.Select(dependency =>
                $"{claim.ClaimId}\n{dependency}"))
            .ToHashSet(StringComparer.Ordinal);
        return current.Values
            .SelectMany(claim => claim.Dependencies.Select(dependency =>
                $"{claim.ClaimId}\n{dependency}"))
            .Distinct(StringComparer.Ordinal)
            .Count(edge => !oldEdges.Contains(edge));
    }

    private static int CountProofClosures(
        PaperTheoryDeepeningAgentContext context,
        IReadOnlyDictionary<string, PaperTheoryDeepeningBaselineClaim> baseline,
        IReadOnlyDictionary<string, PaperTheoremPackageClaim> current,
        PaperTheoremPackage package)
    {
        int claimsClosed = current.Values.Count(claim =>
            CompleteProofStatuses.Contains(claim.ProofStatus)
            && (!baseline.TryGetValue(claim.ClaimId, out PaperTheoryDeepeningBaselineClaim? old)
                || !old.ProofComplete));
        int openObligationsClosed = context.PreviousPackage is null
            ? 0
            : Math.Max(
                0,
                context.PreviousPackage.TheoremPackageContent.OpenProofObligations.Count
                    - package.TheoremPackageContent.OpenProofObligations.Count);
        return claimsClosed + openObligationsClosed;
    }

    private static int CountCounterexamplesResolved(
        PaperTheoryDeepeningDraft draft,
        PaperTheoremPackage package)
    {
        if (draft.Iteration.CounterexampleFindings.Count == 0)
        {
            return 0;
        }
        var byId = package.TheoremPackageContent.Claims.ToDictionary(
            claim => claim.ClaimId,
            StringComparer.Ordinal);
        bool hasCompleteWitness = package.TheoremPackageContent.SharpnessClaimIds
            .Any(id => byId.TryGetValue(id, out PaperTheoremPackageClaim? claim)
                && CompleteProofStatuses.Contains(claim.ProofStatus))
            || package.TheoremPackageContent.Claims.Any(claim =>
                string.Equals(claim.Kind, "counterexample", StringComparison.Ordinal)
                && CompleteProofStatuses.Contains(claim.ProofStatus));
        return hasCompleteWitness
            ? draft.Iteration.CounterexampleFindings.Distinct(StringComparer.Ordinal).Count()
            : 0;
    }

    private static IReadOnlyList<PaperCandidateSplitProposal> CreateSplitProposals(
        PaperTheoryDeepeningDraft draft,
        PaperTheoremPackage package)
    {
        var proposals = new List<PaperCandidateSplitProposal>();
        foreach (PaperCandidateSplitProposalDraft value in draft.SplitProposals)
        {
            RequireNotBefore(
                value.ProposedAt,
                package.TheoremPackageContent.CreatedAt,
                "split proposal proposed_at");
            var content = new PaperCandidateSplitProposalContent(
                package.TheoremPackageContent.TheoryProgramRef,
                package.TheoremPackageId,
                package.TheoremPackageContent.PaperId,
                value.ProposedPaperId,
                value.ExtractedClaimIds,
                value.IndependentResearchQuestion,
                value.IndependentProofSpine,
                value.ScopeMismatch,
                value.PublicationRationale,
                value.OverlapRisk,
                value.ProposedAt);
            proposals.Add(PaperTheoryPortfolioProposalService.CreateSplitProposal(
                package,
                content));
        }
        return proposals
            .OrderBy(proposal => proposal.ProposalId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<PaperResearchLedgerEntry> CreateLedgerEntries(
        PaperTheoryDeepeningDraft draft,
        PaperTheoremPackage package)
    {
        var entries = new List<PaperResearchLedgerEntry>();
        foreach (PaperResearchLedgerEntryDraft value in draft.ResearchLedgerEntries)
        {
            RequireNotBefore(
                value.RecordedAt,
                package.TheoremPackageContent.CreatedAt,
                "research ledger recorded_at");
            var content = new PaperResearchLedgerEntryContent(
                package.TheoremPackageContent.TheoryProgramRef,
                package.TheoremPackageId,
                package.TheoremPackageContent.PaperId,
                value.DiscoveryKind,
                value.RelatedRefs,
                value.Summary,
                value.WhyRecorded,
                value.PromotionStatus,
                value.RecordedAt);
            entries.Add(PaperTheoryPortfolioProposalService.CreateLedgerEntry(
                package,
                content));
        }
        return entries
            .OrderBy(entry => entry.EntryId, StringComparer.Ordinal)
            .ToArray();
    }

    private static void ValidatePortfolioRoutes(
        PaperTheoryDeepeningDraft draft,
        PaperTheoremPackage package,
        IReadOnlyList<PaperCandidateSplitProposal> splitProposals,
        IReadOnlyList<PaperResearchLedgerEntry> ledgerEntries)
    {
        string[] extracted = splitProposals
            .SelectMany(proposal => proposal.ProposalContent.ExtractedClaimIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        RequireSameSet(
            draft.Iteration.SplitCandidateClaimIds,
            extracted,
            "split_candidate_claim_ids");
        string[] proposedPaperIds = splitProposals
            .Select(proposal => proposal.ProposalContent.ProposedPaperId)
            .ToArray();
        if (proposedPaperIds.Distinct(StringComparer.Ordinal).Count() != proposedPaperIds.Length)
        {
            throw new InvalidDataException(
                "A2 split proposals must create distinct paper IDs.");
        }
        string[] mergeCandidates = NormalizeMergeCandidateIds(
            draft.Iteration.MergeCandidatePaperIds,
            package.TheoremPackageContent.PaperId);
        var discoveryKinds = ledgerEntries
            .Select(entry => entry.EntryContent.DiscoveryKind)
            .ToHashSet(StringComparer.Ordinal);
        if (!discoveryKinds.Contains("prior-work-boundary"))
        {
            throw new InvalidDataException(
                "Every A2 round must record a prior-work-boundary ledger entry.");
        }
        if (splitProposals.Count > 0 && !discoveryKinds.Contains("split-candidate"))
        {
            throw new InvalidDataException(
                "A2 split proposals require a split-candidate ledger entry.");
        }
        if (mergeCandidates.Length > 0 && !discoveryKinds.Contains("merge-candidate"))
        {
            throw new InvalidDataException(
                "A2 merge candidates require a merge-candidate ledger entry.");
        }
        if (draft.Iteration.ProgressEvidence.CounterexamplesResolved > 0
            && !discoveryKinds.Contains("counterexample"))
        {
            throw new InvalidDataException(
                "A2 counterexample progress requires a counterexample ledger entry.");
        }
    }

    private static string[] NormalizeMergeCandidateIds(
        IReadOnlyList<string> values,
        string sourcePaperId)
    {
        if (values is null)
        {
            throw new InvalidDataException("merge_candidate_paper_ids is required.");
        }
        var normalized = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            RequirePaperId(value);
            if (string.Equals(value, sourcePaperId, StringComparison.Ordinal)
                || !normalized.Add(value))
            {
                throw new InvalidDataException(
                    "A2 merge candidate paper IDs must be distinct from the source and unique.");
            }
        }
        return normalized.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static PaperTheoryDeepeningStoredArtifact StoreDomain<TContent, TEnvelope>(
        string root,
        string family,
        string domainSchema,
        string domainRef,
        TContent content,
        TEnvelope envelope)
    {
        byte[] contentBytes = CanonicalJson.Serialize(content);
        if (!string.Equals(Reference(contentBytes), domainRef, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Canonical A2 content does not match its domain identifier.");
        }
        string contentPath = ArtifactPath(root, family, domainRef);
        _ = PutImmutable(contentPath, contentBytes);
        byte[] envelopeBytes = CanonicalJson.Serialize(envelope);
        string envelopeRef = Reference(envelopeBytes);
        string envelopePath = ArtifactPath(root, "envelopes", envelopeRef);
        _ = PutImmutable(envelopePath, envelopeBytes);
        return new PaperTheoryDeepeningStoredArtifact(
            domainSchema,
            domainRef,
            RelativePath(root, contentPath),
            envelopeRef,
            RelativePath(root, envelopePath));
    }

    private static void ValidateTaskBinding(
        string root,
        PaperAgentTask task,
        PaperTheoryDeepeningAgentDispatch dispatch,
        string dispatchRef,
        string dispatchRelativePath,
        PaperTheoryDeepeningAgentContext context)
    {
        PaperAgentTask expected = BuildTask(
            root,
            dispatch,
            dispatchRef,
            dispatchRelativePath,
            context);
        if (!CanonicalJson.Serialize(task).AsSpan().SequenceEqual(CanonicalJson.Serialize(expected)))
        {
            throw new InvalidDataException(
                "Theory-deepening task changed its dispatch-owned contract.");
        }
    }

    private static PaperTheoryDeepeningAgentResultAdmitted ReplayAdmission(
        string root,
        PaperTheoryDeepeningAgentAdmissionCursor cursor,
        string taskRef,
        PaperAgentTaskCursor agentCursor,
        PaperTheoryDeepeningAgentDispatch dispatch,
        string dispatchRef)
    {
        Validate(cursor);
        if (!string.Equals(cursor.TaskRef, taskRef, StringComparison.Ordinal)
            || !string.Equals(cursor.ResultRef, agentCursor.ResultRef, StringComparison.Ordinal)
            || !string.Equals(cursor.DispatchRef, dispatchRef, StringComparison.Ordinal)
            || !string.Equals(cursor.PaperId, dispatch.PaperId, StringComparison.Ordinal)
            || !string.Equals(cursor.TheoryProgramRef, dispatch.TheoryProgramRef, StringComparison.Ordinal)
            || !string.Equals(cursor.RequestRef, dispatch.RequestRef, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "A2 admission cursor changed task, result, dispatch, or paper identity.");
        }
        ValidateStoredArtifact(root, cursor.Iteration);
        ValidateStoredArtifact(root, cursor.TheoremPackage);
        ValidateStoredArtifact(root, cursor.Delta);
        foreach (PaperTheoryDeepeningStoredArtifact artifact in cursor.SplitProposals)
        {
            ValidateStoredArtifact(root, artifact);
        }
        foreach (PaperTheoryDeepeningStoredArtifact artifact in cursor.ResearchLedgerEntries)
        {
            ValidateStoredArtifact(root, artifact);
        }
        return Recorded(cursor, replayed: true);
    }

    private static void ValidateStoredArtifact(
        string root,
        PaperTheoryDeepeningStoredArtifact coordinate)
    {
        byte[] contentBytes = ReadRepositoryArtifact(
            root,
            coordinate.ContentPath,
            coordinate.ArtifactRef,
            "Stored A2 domain content");
        byte[] envelopeBytes = ReadRepositoryArtifact(
            root,
            coordinate.EnvelopePath,
            coordinate.EnvelopeRef,
            "Stored A2 domain envelope");
        switch (coordinate.Schema)
        {
            case PaperTheoryDeepeningSchemas.TheoryIteration:
            {
                PaperTheoryIterationContent content =
                    PaperResearchInputJson.DeserializeStrict<PaperTheoryIterationContent>(contentBytes);
                var expected = new PaperTheoryIteration(
                    coordinate.Schema,
                    coordinate.ArtifactRef,
                    content);
                PaperTheoryDeepeningService.Validate(expected);
                PaperTheoryIteration envelope =
                    PaperResearchInputJson.DeserializeStrict<PaperTheoryIteration>(envelopeBytes);
                PaperTheoryDeepeningService.Validate(envelope);
                RequireExact(envelope.IterationId, expected.IterationId, "stored iteration id");
                break;
            }
            case PaperTheoryDeepeningSchemas.TheoremPackage:
            {
                PaperTheoremPackageContent content =
                    PaperResearchInputJson.DeserializeStrict<PaperTheoremPackageContent>(contentBytes);
                var expected = new PaperTheoremPackage(
                    coordinate.Schema,
                    coordinate.ArtifactRef,
                    content);
                PaperTheoryDeepeningService.Validate(expected);
                PaperTheoremPackage envelope =
                    PaperResearchInputJson.DeserializeStrict<PaperTheoremPackage>(envelopeBytes);
                PaperTheoryDeepeningService.Validate(envelope);
                RequireExact(envelope.TheoremPackageId, expected.TheoremPackageId, "stored theorem package id");
                break;
            }
            case PaperTheoryDeepeningAgentSchemas.Delta:
            {
                PaperTheoryDeepeningDeltaContent content =
                    PaperResearchInputJson.DeserializeStrict<PaperTheoryDeepeningDeltaContent>(contentBytes);
                var expected = new PaperTheoryDeepeningDelta(
                    coordinate.Schema,
                    coordinate.ArtifactRef,
                    content);
                Validate(expected);
                PaperTheoryDeepeningDelta envelope =
                    PaperResearchInputJson.DeserializeStrict<PaperTheoryDeepeningDelta>(envelopeBytes);
                Validate(envelope);
                RequireExact(envelope.DeltaId, expected.DeltaId, "stored delta id");
                break;
            }
            case PaperTheoryDeepeningSchemas.SplitProposal:
            {
                PaperCandidateSplitProposal envelope =
                    PaperResearchInputJson.DeserializeStrict<PaperCandidateSplitProposal>(envelopeBytes);
                PaperTheoryPortfolioProposalService.Validate(envelope);
                RequireExact(envelope.ProposalId, coordinate.ArtifactRef, "stored split proposal id");
                break;
            }
            case PaperTheoryDeepeningSchemas.ResearchLedgerEntry:
            {
                PaperResearchLedgerEntry envelope =
                    PaperResearchInputJson.DeserializeStrict<PaperResearchLedgerEntry>(envelopeBytes);
                PaperTheoryPortfolioProposalService.Validate(envelope);
                RequireExact(envelope.EntryId, coordinate.ArtifactRef, "stored ledger entry id");
                break;
            }
            default:
                throw new InvalidDataException(
                    $"Unsupported stored A2 schema {coordinate.Schema}.");
        }
    }

    private static PaperTheoryDeepeningAgentResultAdmitted Recorded(
        PaperTheoryDeepeningAgentAdmissionCursor cursor,
        bool replayed) =>
        new(
            PaperTheoryDeepeningAgentSchemas.ResultAdmitted,
            cursor.TaskRef,
            cursor.ResultRef,
            cursor.DispatchRef,
            cursor.PaperId,
            cursor.TheoryProgramRef,
            cursor.RequestRef,
            cursor.Round,
            cursor.Iteration,
            cursor.TheoremPackage,
            cursor.Delta,
            cursor.SplitProposals,
            cursor.ResearchLedgerEntries,
            cursor.MergeCandidatePaperIds,
            cursor.Maturity,
            cursor.NextRoute,
            cursor.RunId,
            cursor.Provenance,
            cursor.AdmittedAt,
            replayed);

    private static PaperAgentTask ReadRegisteredTask(string root, string taskRef)
    {
        byte[] bytes = ReadImmutable(
            AgentArtifactPath(root, "tasks", taskRef),
            taskRef,
            "Registered Paper agent task");
        PaperAgentTask task =
            PaperResearchInputJson.DeserializeStrict<PaperAgentTask>(bytes);
        PaperAgentRuntimeService.Validate(task);
        ValidateInputSources(root, task.ExactInputs);
        return task;
    }

    private static PaperAgentTaskCursor ReadAgentCursor(
        string root,
        PaperAgentTask task,
        string taskRef)
    {
        string path = Path.Combine(
            root,
            "work",
            "paper-agents",
            "cursors",
            Hex(taskRef) + ".json");
        PaperAgentTaskCursor cursor =
            PaperResearchInputJson.DeserializeStrict<PaperAgentTaskCursor>(
                ReadBoundedFile(path, MaximumControlBytes, "Paper agent cursor"));
        PaperAgentRuntimeService.Validate(cursor, task, taskRef);
        return cursor;
    }

    private static PaperAgentResultWire ReadAgentResult(
        string root,
        PaperAgentTask task,
        string taskRef,
        string resultRef)
    {
        PaperAgentResultWire result =
            PaperResearchInputJson.DeserializeStrict<PaperAgentResultWire>(
                ReadImmutable(
                    AgentArtifactPath(root, "results", resultRef),
                    resultRef,
                    "Paper agent result"));
        PaperAgentRuntimeService.Validate(result, task, taskRef);
        return result;
    }

    private static byte[] ReadAgentOutput(string root, string outputRef) =>
        ReadImmutable(
            AgentArtifactPath(root, "outputs", outputRef),
            outputRef,
            "Paper agent output");

    private static void RequireCursorMatchesResult(
        PaperAgentTaskCursor cursor,
        PaperAgentResultWire result)
    {
        if (!string.Equals(cursor.Status, result.Status, StringComparison.Ordinal)
            || !string.Equals(cursor.Summary, result.Summary, StringComparison.Ordinal)
            || !string.Equals(cursor.NextRoute, result.NextRoute, StringComparison.Ordinal)
            || !string.Equals(cursor.BlockerCode, result.BlockerCode, StringComparison.Ordinal)
            || !string.Equals(cursor.CompletedAt, result.CompletedAt, StringComparison.Ordinal)
            || cursor.Outputs.Count != result.Outputs.Count)
        {
            throw new InvalidDataException(
                "Paper agent cursor does not match its immutable result artifact.");
        }
        string[] cursorOutputs = cursor.Outputs
            .Select(output => $"{output.Schema}\n{output.WorkspaceRelativePath}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] resultOutputs = result.Outputs
            .Select(output => $"{output.Schema}\n{output.WorkspaceRelativePath}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!cursorOutputs.SequenceEqual(resultOutputs, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Paper agent cursor changed the result output set.");
        }
    }

    private static PaperTheoryDeepeningAgentAdmissionCursor ReadAdmissionCursor(string path)
    {
        PaperTheoryDeepeningAgentAdmissionCursor cursor =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryDeepeningAgentAdmissionCursor>(
                ReadBoundedFile(path, MaximumControlBytes, "A2 admission cursor"));
        Validate(cursor);
        return cursor;
    }

    private static PaperAgentInputArtifact RequiredInput(
        IReadOnlyList<PaperAgentInputArtifact> inputs,
        string schema,
        string artifactRef,
        string name)
    {
        PaperAgentInputArtifact? input = inputs.SingleOrDefault(value =>
            string.Equals(value.Schema, schema, StringComparison.Ordinal)
            && string.Equals(value.ArtifactRef, artifactRef, StringComparison.Ordinal));
        return input ?? throw new InvalidDataException(
            $"Theory-deepening dispatch is missing the exact {name} content artifact.");
    }

    private static void RequireInputRefsExactly(
        IReadOnlyList<PaperAgentInputArtifact> inputs,
        IEnumerable<string> expectedRefs)
    {
        string[] actual = inputs.Select(input => input.ArtifactRef)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] expected = expectedRefs
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Theory-deepening dispatch changed the domain request exact-input closure.");
        }
    }

    private static void ValidateInputSources(
        string root,
        IReadOnlyList<PaperAgentInputArtifact> inputs)
    {
        foreach (PaperAgentInputArtifact input in inputs)
        {
            _ = ReadExactInput(root, input);
        }
    }

    private static byte[] ReadExactInput(
        string root,
        PaperAgentInputArtifact input) =>
        ReadRepositoryArtifact(
            root,
            input.RepositoryRelativePath,
            input.ArtifactRef,
            $"Exact input {input.Schema}");

    private static byte[] ReadRepositoryArtifact(
        string root,
        string relativePath,
        string expectedRef,
        string name)
    {
        RequireRepositoryRelativePath(relativePath, name);
        string full = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        RequirePathWithin(root, full, name);
        RejectReparsePointsBetween(root, full, name);
        return ReadImmutable(full, expectedRef, name);
    }

    private static void ValidateStoredCoordinate(
        PaperTheoryDeepeningStoredArtifact coordinate,
        string expectedSchema)
    {
        ArgumentNullException.ThrowIfNull(coordinate);
        RequireExact(coordinate.Schema, expectedSchema, nameof(coordinate.Schema));
        RequireDigest(coordinate.ArtifactRef, nameof(coordinate.ArtifactRef));
        RequireRepositoryRelativePath(coordinate.ContentPath, nameof(coordinate.ContentPath));
        RequireDigest(coordinate.EnvelopeRef, nameof(coordinate.EnvelopeRef));
        RequireRepositoryRelativePath(coordinate.EnvelopePath, nameof(coordinate.EnvelopePath));
    }

    private static void ValidateStoredCoordinates(
        IReadOnlyList<PaperTheoryDeepeningStoredArtifact> values,
        string expectedSchema,
        string name,
        int minimum = 0)
    {
        if (values is null || values.Count < minimum || values.Count > 32)
        {
            throw new InvalidDataException($"{name} has an invalid count.");
        }
        var refs = new HashSet<string>(StringComparer.Ordinal);
        foreach (PaperTheoryDeepeningStoredArtifact value in values)
        {
            ValidateStoredCoordinate(value, expectedSchema);
            if (!refs.Add(value.ArtifactRef))
            {
                throw new InvalidDataException($"{name} contains duplicate artifacts.");
            }
        }
    }

    private static void RequireClaimIds(IReadOnlyList<string> values, string name)
    {
        if (values is null)
        {
            throw new InvalidDataException($"{name} is required.");
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            if (string.IsNullOrWhiteSpace(value) || !seen.Add(value))
            {
                throw new InvalidDataException($"{name} contains invalid or duplicate claim IDs.");
            }
        }
    }

    private static void RequireSameSet(
        IReadOnlyList<string> declared,
        IReadOnlyList<string> actual,
        string name)
    {
        string[] left = declared.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        string[] right = actual.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (!left.SequenceEqual(right, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                $"A2 {name} does not match the repository-computed theorem-package delta.");
        }
    }

    private static string RequireRepositoryRoot(string repositoryRoot)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            throw new InvalidDataException("Paper repository root is required.");
        }
        string root = Path.GetFullPath(repositoryRoot);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException(
                $"Paper repository root does not exist: {root}");
        }
        return root;
    }

    private static string RequireDispatchPath(string root, string dispatchPath)
    {
        if (string.IsNullOrWhiteSpace(dispatchPath))
        {
            throw new InvalidDataException("Theory-deepening dispatch path is required.");
        }
        string full = Path.GetFullPath(dispatchPath);
        string inbox = Path.GetFullPath(Path.Combine(root, "inbox", "theory-deepening"));
        RequirePathWithin(inbox, full, "Theory-deepening dispatch path");
        if (!File.Exists(full)
            || !string.Equals(Path.GetExtension(full), ".json", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Theory-deepening dispatch must be an existing JSON file in its deployment inbox.");
        }
        RejectReparsePointsBetween(inbox, full, "Theory-deepening dispatch path");
        return full;
    }

    private static string ArtifactPath(string root, string family, string reference)
    {
        string hex = Hex(reference);
        return Path.Combine(
            root,
            "artifacts",
            "paper-theory-deepening",
            family,
            "sha256",
            hex[..2],
            hex + ".json");
    }

    private static string AgentArtifactPath(string root, string family, string reference)
    {
        string hex = Hex(reference);
        return Path.Combine(
            root,
            "artifacts",
            "paper-agents",
            family,
            "sha256",
            hex[..2],
            hex + ".json");
    }

    private static string AdmissionCursorPath(string root, string taskRef) =>
        Path.Combine(
            root,
            "work",
            "paper-theory-deepening",
            "cursors",
            Hex(taskRef) + ".json");

    private static bool PutImmutable(string path, ReadOnlySpan<byte> bytes)
    {
        if (File.Exists(path))
        {
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
            {
                throw new InvalidDataException($"Content-address collision at {path}.");
            }
            return true;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        PaperResearchInputStore.WriteAtomic(path, bytes, overwrite: false);
        return false;
    }

    private static byte[] ReadImmutable(string path, string expectedRef, string name)
    {
        RequireDigest(expectedRef, nameof(expectedRef));
        byte[] bytes = ReadBoundedFile(path, MaximumArtifactBytes, name);
        if (!string.Equals(Reference(bytes), expectedRef, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{name} failed content-address verification.");
        }
        return bytes;
    }

    private static byte[] ReadBoundedFile(string path, int maximumBytes, string name)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"{name} is missing.", path);
        }
        var info = new FileInfo(path);
        if (info.Length < 1 || info.Length > maximumBytes)
        {
            throw new InvalidDataException(
                $"{name} must contain between one and {maximumBytes} bytes.");
        }
        return File.ReadAllBytes(path);
    }

    private static string RelativePath(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    private static string Reference(ReadOnlySpan<byte> bytes) =>
        PaperResearchInputStore.Reference(bytes);

    private static string Identity<T>(T content) =>
        CanonicalJson.Sha256Reference(CanonicalJson.Serialize(content));

    private static void RequireIdentity<T>(string reference, T content, string name)
    {
        RequireDigest(reference, name);
        if (!string.Equals(reference, Identity(content), StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{name} does not address canonical content bytes.");
        }
    }

    private static string Hex(string reference)
    {
        RequireDigest(reference, nameof(reference));
        return reference["sha256:".Length..];
    }

    private static void RequireRepositoryRelativePath(string value, string name)
    {
        RequireCanonicalRelativePath(value, name);
        string first = value.Split('/')[0];
        if (!AllowedEvidenceRoots.Contains(first))
        {
            throw new InvalidDataException(
                $"{name} is outside the approved Paper evidence roots.");
        }
    }

    private static void RequireCanonicalRelativePath(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 4096
            || Path.IsPathRooted(value)
            || value.Contains('\\')
            || !RelativePathPattern.IsMatch(value))
        {
            throw new InvalidDataException($"{name} is not a canonical relative path.");
        }
        foreach (string segment in value.Split('/'))
        {
            if (segment is "." or ".." || segment.All(character => character == '.'))
            {
                throw new InvalidDataException($"{name} contains an unsafe path segment.");
            }
        }
    }

    private static void RequirePathWithin(string root, string path, string name)
    {
        string normalizedRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string normalizedPath = Path.GetFullPath(path);
        if (!normalizedPath.StartsWith(normalizedRoot, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{name} escapes its owned filesystem boundary.");
        }
    }

    private static void RejectReparsePointsBetween(string boundaryRoot, string path, string name)
    {
        string root = Path.GetFullPath(boundaryRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string full = Path.GetFullPath(path);
        RequirePathWithin(root, full, name);
        string relative = Path.GetRelativePath(root, full);
        string current = root;
        if (Directory.Exists(current) || File.Exists(current))
        {
            RejectReparsePoint(current, name);
        }
        foreach (string segment in relative.Split(
                     Path.DirectorySeparatorChar,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (Directory.Exists(current) || File.Exists(current))
            {
                RejectReparsePoint(current, name);
            }
        }
    }

    private static void RejectReparsePoint(string path, string name)
    {
        FileAttributes attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException($"{name} cannot traverse a symbolic link.");
        }
    }

    private static void RequireSchema(string value, string name)
    {
        if (!SchemaPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException($"{name} is not a versioned schema name.");
        }
    }

    private static void RequirePaperId(string value)
    {
        if (!PaperIdPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException("paper_id is not a canonical identifier.");
        }
    }

    private static void RequireDigest(string value, string name)
    {
        if (!DigestPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException(
                $"{name} must be sha256:<64 lowercase hex>.");
        }
    }

    private static void RequireRunId(string value)
    {
        if (value is null || value.Length > 512 || value.Contains('\n') || value.Contains('\r'))
        {
            throw new InvalidDataException("run_id is invalid.");
        }
    }

    private static void RequireTextList(
        IReadOnlyList<string> values,
        string name,
        int maximumLength,
        int minimum)
    {
        if (values is null || values.Count < minimum)
        {
            throw new InvalidDataException($"{name} is incomplete.");
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            if (string.IsNullOrWhiteSpace(value)
                || value.Length > maximumLength
                || !seen.Add(value))
            {
                throw new InvalidDataException($"{name} contains invalid or duplicate values.");
            }
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
                out DateTimeOffset parsed)
            || parsed.Offset != TimeSpan.Zero)
        {
            throw new InvalidDataException($"{name} must be an RFC 3339 UTC timestamp.");
        }
        return parsed;
    }

    private static void RequireNotBefore(string actual, string lowerBound, string name)
    {
        if (ParseUtc(actual, name) < ParseUtc(lowerBound, "lower-bound timestamp"))
        {
            throw new InvalidDataException($"{name} cannot precede its causal input.");
        }
    }
}
