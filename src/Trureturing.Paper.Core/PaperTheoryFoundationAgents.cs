using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Trureturing.Paper.Core;

public static class PaperTheoryFoundationAgentSchemas
{
    public const string Dispatch = "paper-theory-foundation-agent-dispatch.v1";
    public const string ScopeDraft = "paper-theory-scope-draft.v1";
    public const string InventoryDraft = "paper-theory-inventory-draft.v1";
    public const string TaskStaged = "paper-theory-foundation-agent-task-staged.v1";
    public const string AdmissionCursor = "paper-theory-foundation-agent-cursor.v1";
    public const string ResultAdmitted = "paper-theory-foundation-agent-result-admitted.v1";
}

public sealed record PaperTheoryFoundationAgentDispatch(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string Kind,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string RequestRef,
    [property: JsonRequired] IReadOnlyList<PaperAgentInputArtifact> ExactInputs,
    [property: JsonRequired] string RequestedAt);

public sealed record PaperTheoryScopeDraft(
    [property: JsonRequired] string Schema,
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

public sealed record PaperTheoryInventoryDraft(
    [property: JsonRequired] string Schema,
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

public sealed record PaperTheoryFoundationAgentTaskStaged(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string TaskPath,
    [property: JsonRequired] string Kind,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string RequestRef,
    [property: JsonRequired] string Phase,
    [property: JsonRequired] string AgentRole,
    [property: JsonRequired] string ContextMode,
    [property: JsonRequired] bool Replayed);

public sealed record PaperTheoryFoundationAgentAdmissionCursor(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string ResultRef,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string Kind,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string RequestRef,
    [property: JsonRequired] string DomainSchema,
    [property: JsonRequired] string DomainRef,
    [property: JsonRequired] string DomainContentPath,
    [property: JsonRequired] string EnvelopeRef,
    [property: JsonRequired] string EnvelopePath,
    [property: JsonRequired] string NextRoute,
    [property: JsonRequired] string RunId,
    [property: JsonRequired] string Provenance,
    [property: JsonRequired] string AdmittedAt);

public sealed record PaperTheoryFoundationAgentResultAdmitted(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string TaskRef,
    [property: JsonRequired] string ResultRef,
    [property: JsonRequired] string DispatchRef,
    [property: JsonRequired] string Kind,
    [property: JsonRequired] string PaperId,
    [property: JsonRequired] string TheoryProgramRef,
    [property: JsonRequired] string RequestRef,
    [property: JsonRequired] string DomainSchema,
    [property: JsonRequired] string DomainRef,
    [property: JsonRequired] string DomainContentPath,
    [property: JsonRequired] string EnvelopeRef,
    [property: JsonRequired] string EnvelopePath,
    [property: JsonRequired] string NextRoute,
    [property: JsonRequired] string RunId,
    [property: JsonRequired] string Provenance,
    [property: JsonRequired] string AdmittedAt,
    [property: JsonRequired] bool Replayed);

internal sealed record PaperTheoryScopeAgentContext(
    PaperTheoryProgram Program,
    PaperTheoryScopeRequest Request);

internal sealed record PaperTheoryInventoryAgentContext(
    PaperTheoryProgram Program,
    PaperTheoryScope Scope,
    PaperTheoryInventoryRequest Request);

internal sealed record PaperTheoryFoundationStoredDomain(
    string DomainSchema,
    string DomainRef,
    string DomainContentPath,
    string EnvelopeRef,
    string EnvelopePath,
    string CreatedAt);

public static class PaperTheoryFoundationAgentService
{
    public const string ScopeKind = "scope";
    public const string InventoryKind = "inventory";

    private const int MaximumDispatchBytes = 2 * 1024 * 1024;
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

    public static PaperTheoryFoundationAgentTaskStaged StageTask(
        string repositoryRoot,
        string dispatchPath)
    {
        string root = RequireRepositoryRoot(repositoryRoot);
        string fullDispatchPath = RequireDispatchPath(root, dispatchPath);
        byte[] dispatchBytes = ReadBoundedFile(
            fullDispatchPath,
            MaximumDispatchBytes,
            "Theory-foundation agent dispatch");
        string dispatchRef = Reference(dispatchBytes);
        PaperTheoryFoundationAgentDispatch dispatch =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryFoundationAgentDispatch>(
                dispatchBytes);
        Validate(dispatch);

        string immutableDispatchPath = ArtifactPath(
            root,
            "dispatches",
            dispatchRef,
            ".json");
        _ = PutImmutable(immutableDispatchPath, dispatchBytes);
        string dispatchRelativePath = RelativePath(root, immutableDispatchPath);

        PaperAgentTask task = dispatch.Kind switch
        {
            ScopeKind => BuildScopeTask(
                root,
                dispatch,
                dispatchRef,
                dispatchRelativePath,
                LoadScopeContext(root, dispatch)),
            InventoryKind => BuildInventoryTask(
                root,
                dispatch,
                dispatchRef,
                dispatchRelativePath,
                LoadInventoryContext(root, dispatch)),
            _ => throw new InvalidDataException(
                $"Unsupported theory-foundation dispatch kind {dispatch.Kind}.")
        };
        PaperAgentRuntimeService.Validate(task);
        byte[] taskBytes = CanonicalJson.Serialize(task);
        string taskRef = Reference(taskBytes);
        string taskPath = Path.Combine(
            root,
            "inbox",
            "agent-tasks",
            $"theory-foundation-{Hex(taskRef)}.json");
        bool replayed = PutImmutable(taskPath, taskBytes);
        PaperAgentProfile profile = PaperAgentRuntimeService.GetProfile(task.Phase);
        return new PaperTheoryFoundationAgentTaskStaged(
            PaperTheoryFoundationAgentSchemas.TaskStaged,
            dispatchRef,
            taskRef,
            taskPath,
            dispatch.Kind,
            dispatch.PaperId,
            dispatch.TheoryProgramRef,
            dispatch.RequestRef,
            task.Phase,
            profile.AgentRole,
            profile.ContextMode,
            replayed);
    }

    public static PaperTheoryFoundationAgentResultAdmitted AdmitResult(
        string repositoryRoot,
        string taskRef)
    {
        string root = RequireRepositoryRoot(repositoryRoot);
        RequireDigest(taskRef, nameof(taskRef));
        PaperAgentTask task = ReadRegisteredTask(root, taskRef);
        if (task.Phase is not "theory-scope" and not "theory-inventory")
        {
            throw new InvalidDataException(
                "Only A0 scope or A1 inventory tasks can enter the foundation admission bridge.");
        }

        PaperAgentTaskCursor agentCursor = ReadAgentCursor(root, task, taskRef);
        PaperAgentResultWire result = ReadAgentResult(
            root,
            task,
            taskRef,
            agentCursor.ResultRef);
        ValidateAgentCursorResult(agentCursor, result);
        if (!string.Equals(result.Status, "completed", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Only completed foundation-agent results can be admitted as domain artifacts.");
        }

        PaperAgentInputArtifact dispatchInput = task.ExactInputs
            .SingleOrDefault(input => string.Equals(
                input.Schema,
                PaperTheoryFoundationAgentSchemas.Dispatch,
                StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                "Foundation-agent task is missing its immutable dispatch input.");
        byte[] dispatchBytes = ReadExactInput(root, dispatchInput);
        string dispatchRef = Reference(dispatchBytes);
        PaperTheoryFoundationAgentDispatch dispatch =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryFoundationAgentDispatch>(
                dispatchBytes);
        Validate(dispatch);
        ValidateTaskBinding(task, dispatch, dispatchRef, dispatchInput.RepositoryRelativePath);

        string expectedNextRoute = ExpectedNextRoute(dispatch.Kind);
        if (!string.Equals(result.NextRoute, expectedNextRoute, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"A completed {dispatch.Kind} result must advance to {expectedNextRoute}.");
        }

        string admissionCursorPath = AdmissionCursorPath(root, taskRef);
        if (File.Exists(admissionCursorPath))
        {
            return ReplayAdmission(
                root,
                ReadAdmissionCursor(admissionCursorPath),
                task,
                agentCursor,
                dispatch,
                dispatchRef);
        }

        PaperAgentStoredOutput output = agentCursor.Outputs.SingleOrDefault()
            ?? throw new InvalidDataException(
                "Completed foundation-agent result must contain exactly one output.");
        string expectedDraftSchema = ExpectedDraftSchema(dispatch.Kind);
        if (!string.Equals(output.Schema, expectedDraftSchema, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Foundation-agent output does not use the expected draft schema.");
        }
        byte[] outputBytes = ReadAgentOutput(root, output.ArtifactRef);
        PaperTheoryFoundationStoredDomain stored = dispatch.Kind switch
        {
            ScopeKind => AdmitScope(
                root,
                dispatch,
                LoadScopeContext(root, dispatch),
                outputBytes),
            InventoryKind => AdmitInventory(
                root,
                dispatch,
                LoadInventoryContext(root, dispatch),
                outputBytes),
            _ => throw new InvalidDataException(
                $"Unsupported theory-foundation dispatch kind {dispatch.Kind}.")
        };

        var cursor = new PaperTheoryFoundationAgentAdmissionCursor(
            PaperTheoryFoundationAgentSchemas.AdmissionCursor,
            taskRef,
            agentCursor.ResultRef,
            dispatchRef,
            dispatch.Kind,
            dispatch.PaperId,
            dispatch.TheoryProgramRef,
            dispatch.RequestRef,
            stored.DomainSchema,
            stored.DomainRef,
            stored.DomainContentPath,
            stored.EnvelopeRef,
            stored.EnvelopePath,
            expectedNextRoute,
            agentCursor.RunId,
            agentCursor.Provenance,
            result.CompletedAt);
        Validate(cursor);
        byte[] cursorBytes = CanonicalJson.Serialize(cursor);
        try
        {
            PaperResearchInputStore.WriteAtomic(
                admissionCursorPath,
                cursorBytes,
                overwrite: false);
        }
        catch (IOException) when (File.Exists(admissionCursorPath))
        {
            PaperTheoryFoundationAgentAdmissionCursor existing =
                ReadAdmissionCursor(admissionCursorPath);
            return ReplayAdmission(
                root,
                existing,
                task,
                agentCursor,
                dispatch,
                dispatchRef);
        }
        return Recorded(cursor, replayed: false);
    }

    public static void Validate(PaperTheoryFoundationAgentDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        RequireExact(
            dispatch.Schema,
            PaperTheoryFoundationAgentSchemas.Dispatch,
            nameof(dispatch.Schema));
        if (dispatch.Kind is not ScopeKind and not InventoryKind)
        {
            throw new InvalidDataException(
                "Theory-foundation dispatch kind must be scope or inventory.");
        }
        RequirePaperId(dispatch.PaperId);
        RequireDigest(dispatch.TheoryProgramRef, nameof(dispatch.TheoryProgramRef));
        RequireDigest(dispatch.RequestRef, nameof(dispatch.RequestRef));
        if (dispatch.ExactInputs is null
            || dispatch.ExactInputs.Count is < 2 or > 64)
        {
            throw new InvalidDataException(
                "Theory-foundation dispatch must contain between two and sixty-four exact inputs.");
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
                    "Theory-foundation exact input refs and paths must be unique.");
            }
        }
        if (!refs.Contains(dispatch.TheoryProgramRef)
            || !refs.Contains(dispatch.RequestRef))
        {
            throw new InvalidDataException(
                "Theory-foundation dispatch must include its program and request content artifacts.");
        }
        ParseUtc(dispatch.RequestedAt, nameof(dispatch.RequestedAt));
    }

    public static void Validate(PaperTheoryFoundationAgentAdmissionCursor cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        RequireExact(
            cursor.Schema,
            PaperTheoryFoundationAgentSchemas.AdmissionCursor,
            nameof(cursor.Schema));
        RequireDigest(cursor.TaskRef, nameof(cursor.TaskRef));
        RequireDigest(cursor.ResultRef, nameof(cursor.ResultRef));
        RequireDigest(cursor.DispatchRef, nameof(cursor.DispatchRef));
        if (cursor.Kind is not ScopeKind and not InventoryKind)
        {
            throw new InvalidDataException("Foundation admission cursor kind is invalid.");
        }
        RequirePaperId(cursor.PaperId);
        RequireDigest(cursor.TheoryProgramRef, nameof(cursor.TheoryProgramRef));
        RequireDigest(cursor.RequestRef, nameof(cursor.RequestRef));
        RequireSchema(cursor.DomainSchema, nameof(cursor.DomainSchema));
        RequireDigest(cursor.DomainRef, nameof(cursor.DomainRef));
        RequireRepositoryRelativePath(cursor.DomainContentPath, nameof(cursor.DomainContentPath));
        RequireDigest(cursor.EnvelopeRef, nameof(cursor.EnvelopeRef));
        RequireRepositoryRelativePath(cursor.EnvelopePath, nameof(cursor.EnvelopePath));
        RequireExact(cursor.NextRoute, ExpectedNextRoute(cursor.Kind), nameof(cursor.NextRoute));
        RequireRunId(cursor.RunId);
        if (cursor.Provenance is not "produced" and not "adopted")
        {
            throw new InvalidDataException("Foundation admission provenance is invalid.");
        }
        ParseUtc(cursor.AdmittedAt, nameof(cursor.AdmittedAt));
    }

    private static PaperAgentTask BuildScopeTask(
        string root,
        PaperTheoryFoundationAgentDispatch dispatch,
        string dispatchRef,
        string dispatchRelativePath,
        PaperTheoryScopeAgentContext context)
    {
        ValidateInputSources(root, dispatch.ExactInputs);
        PaperAgentProfile profile = PaperAgentRuntimeService.GetProfile("theory-scope");
        PaperAgentTask task = new(
            PaperAgentSchemas.Task,
            dispatch.PaperId,
            dispatch.TheoryProgramRef,
            profile.Phase,
            profile.AgentRole,
            profile.ContextMode,
            TaskInputs(dispatch, dispatchRef, dispatchRelativePath),
            [new PaperAgentExpectedOutput(
                PaperTheoryFoundationAgentSchemas.ScopeDraft,
                "outputs/scope-draft.json")],
            ["theory-inventory", "theory-scope", "blocked"],
            BuildScopeInstruction(context.Request),
            TaskForbiddenShortcuts(context.Request.RequestContent.Contract),
            dispatch.RequestedAt);
        PaperAgentRuntimeService.Validate(task);
        return task;
    }

    private static PaperAgentTask BuildInventoryTask(
        string root,
        PaperTheoryFoundationAgentDispatch dispatch,
        string dispatchRef,
        string dispatchRelativePath,
        PaperTheoryInventoryAgentContext context)
    {
        ValidateInputSources(root, dispatch.ExactInputs);
        PaperAgentProfile profile = PaperAgentRuntimeService.GetProfile("theory-inventory");
        PaperAgentTask task = new(
            PaperAgentSchemas.Task,
            dispatch.PaperId,
            dispatch.TheoryProgramRef,
            profile.Phase,
            profile.AgentRole,
            profile.ContextMode,
            TaskInputs(dispatch, dispatchRef, dispatchRelativePath),
            [new PaperAgentExpectedOutput(
                PaperTheoryFoundationAgentSchemas.InventoryDraft,
                "outputs/inventory-draft.json")],
            ["theory-deepening", "theory-inventory", "blocked"],
            BuildInventoryInstruction(context.Request),
            TaskForbiddenShortcuts(context.Request.RequestContent.Contract),
            dispatch.RequestedAt);
        PaperAgentRuntimeService.Validate(task);
        return task;
    }

    private static PaperAgentInputArtifact[] TaskInputs(
        PaperTheoryFoundationAgentDispatch dispatch,
        string dispatchRef,
        string dispatchRelativePath) =>
        dispatch.ExactInputs
            .Append(new PaperAgentInputArtifact(
                PaperTheoryFoundationAgentSchemas.Dispatch,
                dispatchRef,
                dispatchRelativePath))
            .OrderBy(input => input.Schema, StringComparer.Ordinal)
            .ThenBy(input => input.ArtifactRef, StringComparer.Ordinal)
            .ToArray();

    private static string[] TaskForbiddenShortcuts(PaperCodexPhaseContract contract) =>
        contract.ForbiddenShortcuts
            .Append("Do not compute or invent the final domain artifact identifier; the repository validator owns canonical identity.")
            .Append("Do not emit a final scope or inventory envelope; write only the declared draft schema.")
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static string BuildScopeInstruction(PaperTheoryScopeRequest request)
    {
        PaperTheoryScopeRequestContent content = request.RequestContent;
        var builder = new StringBuilder();
        builder.AppendLine("Execute the A0 paper-theory scope phase from the exact supplied evidence.");
        builder.AppendLine("Write exactly one paper-theory-scope-draft.v1 object to outputs/scope-draft.json.");
        builder.AppendLine($"Bind theory_program_ref to {content.TheoryProgramRef}.");
        builder.AppendLine($"Bind scope_request_ref to {request.RequestId}.");
        builder.AppendLine($"Bind paper_id to {content.PaperId}.");
        builder.AppendLine("The draft fields are research_question, abstraction_target, publication_floor, in_scope_obligations, supporting_only, out_of_scope, split_policy, counterexample_obligations, and created_at.");
        builder.AppendLine("The repository will construct and hash the final paper-theory-scope.v1 envelope after full domain validation.");
        AppendContract(builder, content.Contract);
        return builder.ToString();
    }

    private static string BuildInventoryInstruction(PaperTheoryInventoryRequest request)
    {
        PaperTheoryInventoryRequestContent content = request.RequestContent;
        var builder = new StringBuilder();
        builder.AppendLine("Execute the A1 paper-theory inventory phase from the exact approved scope and evidence.");
        builder.AppendLine("Write exactly one paper-theory-inventory-draft.v1 object to outputs/inventory-draft.json.");
        builder.AppendLine($"Bind theory_program_ref to {content.TheoryProgramRef}.");
        builder.AppendLine($"Bind scope_ref to {content.ScopeRef}.");
        builder.AppendLine($"Bind inventory_request_ref to {request.RequestId}.");
        builder.AppendLine($"Bind paper_id to {content.PaperId}.");
        builder.AppendLine("Inventory every claim as a structured item with claim_id, title, kind, status, statement, dependencies, role_in_argument, and required_action.");
        builder.AppendLine("The repository will construct and hash the final paper-theory-inventory.v1 envelope after validating the complete acyclic theorem DAG.");
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

    private static PaperTheoryScopeAgentContext LoadScopeContext(
        string root,
        PaperTheoryFoundationAgentDispatch dispatch)
    {
        ValidateInputSources(root, dispatch.ExactInputs);
        PaperAgentInputArtifact programInput = RequiredInput(
            dispatch.ExactInputs,
            PaperPortfolioSchemas.TheoryProgram,
            dispatch.TheoryProgramRef,
            "theory program");
        PaperTheoryProgramContent programContent =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryProgramContent>(
                ReadExactInput(root, programInput));
        var program = new PaperTheoryProgram(
            PaperPortfolioSchemas.TheoryProgram,
            dispatch.TheoryProgramRef,
            programContent);
        PaperPortfolioService.Validate(program);

        PaperAgentInputArtifact requestInput = RequiredInput(
            dispatch.ExactInputs,
            PaperTheoryFoundationSchemas.ScopeRequest,
            dispatch.RequestRef,
            "scope request");
        PaperTheoryScopeRequestContent requestContent =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryScopeRequestContent>(
                ReadExactInput(root, requestInput));
        var request = new PaperTheoryScopeRequest(
            PaperTheoryFoundationSchemas.ScopeRequest,
            dispatch.RequestRef,
            requestContent);
        PaperTheoryFoundationService.Validate(request, program);
        RequireDispatchIdentity(
            dispatch,
            program.ProgramContent.PaperId,
            program.TheoryProgramId,
            request.RequestId,
            request.RequestContent.RequestedAt);
        RequireInputRefsExactly(
            dispatch.ExactInputs,
            request.RequestContent.Contract.ExactInputRefs.Append(request.RequestId));
        return new PaperTheoryScopeAgentContext(program, request);
    }

    private static PaperTheoryInventoryAgentContext LoadInventoryContext(
        string root,
        PaperTheoryFoundationAgentDispatch dispatch)
    {
        ValidateInputSources(root, dispatch.ExactInputs);
        PaperAgentInputArtifact programInput = RequiredInput(
            dispatch.ExactInputs,
            PaperPortfolioSchemas.TheoryProgram,
            dispatch.TheoryProgramRef,
            "theory program");
        PaperTheoryProgramContent programContent =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryProgramContent>(
                ReadExactInput(root, programInput));
        var program = new PaperTheoryProgram(
            PaperPortfolioSchemas.TheoryProgram,
            dispatch.TheoryProgramRef,
            programContent);
        PaperPortfolioService.Validate(program);

        PaperAgentInputArtifact requestInput = RequiredInput(
            dispatch.ExactInputs,
            PaperTheoryFoundationSchemas.InventoryRequest,
            dispatch.RequestRef,
            "inventory request");
        PaperTheoryInventoryRequestContent requestContent =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryInventoryRequestContent>(
                ReadExactInput(root, requestInput));
        var request = new PaperTheoryInventoryRequest(
            PaperTheoryFoundationSchemas.InventoryRequest,
            dispatch.RequestRef,
            requestContent);
        PaperAgentInputArtifact scopeInput = RequiredInput(
            dispatch.ExactInputs,
            PaperTheoryFoundationSchemas.Scope,
            request.RequestContent.ScopeRef,
            "theory scope");
        PaperTheoryScopeContent scopeContent =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryScopeContent>(
                ReadExactInput(root, scopeInput));
        var scope = new PaperTheoryScope(
            PaperTheoryFoundationSchemas.Scope,
            request.RequestContent.ScopeRef,
            scopeContent);
        PaperTheoryFoundationService.Validate(scope, program);
        PaperTheoryFoundationService.Validate(request, program, scope);
        RequireDispatchIdentity(
            dispatch,
            program.ProgramContent.PaperId,
            program.TheoryProgramId,
            request.RequestId,
            request.RequestContent.RequestedAt);
        RequireInputRefsExactly(
            dispatch.ExactInputs,
            request.RequestContent.Contract.ExactInputRefs.Append(request.RequestId));
        return new PaperTheoryInventoryAgentContext(program, scope, request);
    }

    private static PaperTheoryFoundationStoredDomain AdmitScope(
        string root,
        PaperTheoryFoundationAgentDispatch dispatch,
        PaperTheoryScopeAgentContext context,
        byte[] draftBytes)
    {
        PaperTheoryScopeDraft draft =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryScopeDraft>(draftBytes);
        RequireExact(
            draft.Schema,
            PaperTheoryFoundationAgentSchemas.ScopeDraft,
            nameof(draft.Schema));
        if (!string.Equals(draft.TheoryProgramRef, dispatch.TheoryProgramRef, StringComparison.Ordinal)
            || !string.Equals(draft.ScopeRequestRef, dispatch.RequestRef, StringComparison.Ordinal)
            || !string.Equals(draft.PaperId, dispatch.PaperId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Scope draft changed its program, request, or paper identity.");
        }
        RequireNotBefore(
            draft.CreatedAt,
            context.Request.RequestContent.RequestedAt,
            "scope draft created_at");
        var content = new PaperTheoryScopeContent(
            draft.TheoryProgramRef,
            draft.ScopeRequestRef,
            draft.PaperId,
            draft.ResearchQuestion,
            draft.AbstractionTarget,
            draft.PublicationFloor,
            draft.InScopeObligations,
            draft.SupportingOnly,
            draft.OutOfScope,
            draft.SplitPolicy,
            draft.CounterexampleObligations,
            draft.CreatedAt);
        PaperTheoryScope scope = PaperTheoryFoundationService.CreateScope(
            context.Program,
            context.Request,
            content);
        return StoreScope(root, scope);
    }

    private static PaperTheoryFoundationStoredDomain AdmitInventory(
        string root,
        PaperTheoryFoundationAgentDispatch dispatch,
        PaperTheoryInventoryAgentContext context,
        byte[] draftBytes)
    {
        PaperTheoryInventoryDraft draft =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryInventoryDraft>(draftBytes);
        RequireExact(
            draft.Schema,
            PaperTheoryFoundationAgentSchemas.InventoryDraft,
            nameof(draft.Schema));
        if (!string.Equals(draft.TheoryProgramRef, dispatch.TheoryProgramRef, StringComparison.Ordinal)
            || !string.Equals(draft.ScopeRef, context.Scope.ScopeId, StringComparison.Ordinal)
            || !string.Equals(draft.InventoryRequestRef, dispatch.RequestRef, StringComparison.Ordinal)
            || !string.Equals(draft.PaperId, dispatch.PaperId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Inventory draft changed its program, scope, request, or paper identity.");
        }
        RequireNotBefore(
            draft.CreatedAt,
            context.Request.RequestContent.RequestedAt,
            "inventory draft created_at");
        var content = new PaperTheoryInventoryContent(
            draft.TheoryProgramRef,
            draft.ScopeRef,
            draft.InventoryRequestRef,
            draft.PaperId,
            draft.Items,
            draft.MainTheoremClaimIds,
            draft.MissingInterfaces,
            draft.StrongerVariants,
            draft.WeakerVariants,
            draft.CounterexampleObligations,
            draft.CreatedAt);
        PaperTheoryInventory inventory = PaperTheoryFoundationService.CreateInventory(
            context.Program,
            context.Scope,
            context.Request,
            content);
        return StoreInventory(root, inventory);
    }

    private static PaperTheoryFoundationStoredDomain StoreScope(
        string root,
        PaperTheoryScope scope)
    {
        byte[] contentBytes = CanonicalJson.Serialize(scope.ScopeContent);
        if (!string.Equals(Reference(contentBytes), scope.ScopeId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Canonical scope content does not match scope_id.");
        }
        string contentPath = ArtifactPath(root, "scopes", scope.ScopeId, ".json");
        _ = PutImmutable(contentPath, contentBytes);
        byte[] envelopeBytes = CanonicalJson.Serialize(scope);
        string envelopeRef = Reference(envelopeBytes);
        string envelopePath = ArtifactPath(root, "envelopes", envelopeRef, ".json");
        _ = PutImmutable(envelopePath, envelopeBytes);
        return new PaperTheoryFoundationStoredDomain(
            scope.Schema,
            scope.ScopeId,
            RelativePath(root, contentPath),
            envelopeRef,
            RelativePath(root, envelopePath),
            scope.ScopeContent.CreatedAt);
    }

    private static PaperTheoryFoundationStoredDomain StoreInventory(
        string root,
        PaperTheoryInventory inventory)
    {
        byte[] contentBytes = CanonicalJson.Serialize(inventory.InventoryContent);
        if (!string.Equals(Reference(contentBytes), inventory.InventoryId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Canonical inventory content does not match inventory_id.");
        }
        string contentPath = ArtifactPath(root, "inventories", inventory.InventoryId, ".json");
        _ = PutImmutable(contentPath, contentBytes);
        byte[] envelopeBytes = CanonicalJson.Serialize(inventory);
        string envelopeRef = Reference(envelopeBytes);
        string envelopePath = ArtifactPath(root, "envelopes", envelopeRef, ".json");
        _ = PutImmutable(envelopePath, envelopeBytes);
        return new PaperTheoryFoundationStoredDomain(
            inventory.Schema,
            inventory.InventoryId,
            RelativePath(root, contentPath),
            envelopeRef,
            RelativePath(root, envelopePath),
            inventory.InventoryContent.CreatedAt);
    }

    private static PaperTheoryFoundationAgentResultAdmitted ReplayAdmission(
        string root,
        PaperTheoryFoundationAgentAdmissionCursor cursor,
        PaperAgentTask task,
        PaperAgentTaskCursor agentCursor,
        PaperTheoryFoundationAgentDispatch dispatch,
        string dispatchRef)
    {
        Validate(cursor);
        if (!string.Equals(cursor.TaskRef, task is null ? string.Empty : agentCursor.TaskRef, StringComparison.Ordinal)
            || !string.Equals(cursor.ResultRef, agentCursor.ResultRef, StringComparison.Ordinal)
            || !string.Equals(cursor.DispatchRef, dispatchRef, StringComparison.Ordinal)
            || !string.Equals(cursor.Kind, dispatch.Kind, StringComparison.Ordinal)
            || !string.Equals(cursor.PaperId, dispatch.PaperId, StringComparison.Ordinal)
            || !string.Equals(cursor.TheoryProgramRef, dispatch.TheoryProgramRef, StringComparison.Ordinal)
            || !string.Equals(cursor.RequestRef, dispatch.RequestRef, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Foundation admission cursor changed task, result, dispatch, or paper identity.");
        }
        ValidateStoredDomain(root, cursor);
        return Recorded(cursor, replayed: true);
    }

    private static void ValidateStoredDomain(
        string root,
        PaperTheoryFoundationAgentAdmissionCursor cursor)
    {
        byte[] contentBytes = ReadRepositoryArtifact(
            root,
            cursor.DomainContentPath,
            cursor.DomainRef,
            "Foundation domain content");
        byte[] envelopeBytes = ReadRepositoryArtifact(
            root,
            cursor.EnvelopePath,
            cursor.EnvelopeRef,
            "Foundation domain envelope");
        if (cursor.Kind == ScopeKind)
        {
            PaperTheoryScopeContent content =
                PaperResearchInputJson.DeserializeStrict<PaperTheoryScopeContent>(contentBytes);
            var scope = new PaperTheoryScope(cursor.DomainSchema, cursor.DomainRef, content);
            PaperTheoryFoundationService.Validate(scope);
            PaperTheoryScope envelope =
                PaperResearchInputJson.DeserializeStrict<PaperTheoryScope>(envelopeBytes);
            PaperTheoryFoundationService.Validate(envelope);
            if (!string.Equals(envelope.ScopeId, scope.ScopeId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Stored scope envelope does not match stored scope content.");
            }
        }
        else
        {
            PaperTheoryInventoryContent content =
                PaperResearchInputJson.DeserializeStrict<PaperTheoryInventoryContent>(contentBytes);
            var inventory = new PaperTheoryInventory(
                cursor.DomainSchema,
                cursor.DomainRef,
                content);
            PaperTheoryFoundationService.Validate(inventory);
            PaperTheoryInventory envelope =
                PaperResearchInputJson.DeserializeStrict<PaperTheoryInventory>(envelopeBytes);
            PaperTheoryFoundationService.Validate(envelope);
            if (!string.Equals(
                    envelope.InventoryId,
                    inventory.InventoryId,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Stored inventory envelope does not match stored inventory content.");
            }
        }
    }

    private static PaperTheoryFoundationAgentResultAdmitted Recorded(
        PaperTheoryFoundationAgentAdmissionCursor cursor,
        bool replayed) =>
        new(
            PaperTheoryFoundationAgentSchemas.ResultAdmitted,
            cursor.TaskRef,
            cursor.ResultRef,
            cursor.DispatchRef,
            cursor.Kind,
            cursor.PaperId,
            cursor.TheoryProgramRef,
            cursor.RequestRef,
            cursor.DomainSchema,
            cursor.DomainRef,
            cursor.DomainContentPath,
            cursor.EnvelopeRef,
            cursor.EnvelopePath,
            cursor.NextRoute,
            cursor.RunId,
            cursor.Provenance,
            cursor.AdmittedAt,
            replayed);

    private static void ValidateTaskBinding(
        PaperAgentTask task,
        PaperTheoryFoundationAgentDispatch dispatch,
        string dispatchRef,
        string dispatchRelativePath)
    {
        PaperAgentTask expected = dispatch.Kind switch
        {
            ScopeKind => BuildScopeTask(
                RequireRepositoryRootFromTaskPath(dispatchRelativePath),
                dispatch,
                dispatchRef,
                dispatchRelativePath,
                throw new InvalidOperationException("unreachable")),
            _ => task
        };
        _ = expected;

        string expectedPhase = dispatch.Kind == ScopeKind
            ? "theory-scope"
            : "theory-inventory";
        string expectedDraft = ExpectedDraftSchema(dispatch.Kind);
        string expectedOutputPath = dispatch.Kind == ScopeKind
            ? "outputs/scope-draft.json"
            : "outputs/inventory-draft.json";
        PaperAgentProfile profile = PaperAgentRuntimeService.GetProfile(expectedPhase);
        if (!string.Equals(task.PaperId, dispatch.PaperId, StringComparison.Ordinal)
            || !string.Equals(task.TheoryProgramRef, dispatch.TheoryProgramRef, StringComparison.Ordinal)
            || !string.Equals(task.Phase, expectedPhase, StringComparison.Ordinal)
            || !string.Equals(task.AgentRole, profile.AgentRole, StringComparison.Ordinal)
            || !string.Equals(task.ContextMode, profile.ContextMode, StringComparison.Ordinal)
            || !string.Equals(task.RequestedAt, dispatch.RequestedAt, StringComparison.Ordinal)
            || task.ExpectedOutputs.Count != 1
            || !string.Equals(task.ExpectedOutputs[0].Schema, expectedDraft, StringComparison.Ordinal)
            || !string.Equals(task.ExpectedOutputs[0].WorkspaceRelativePath, expectedOutputPath, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Foundation-agent task changed its dispatch-owned phase, role, context, or output contract.");
        }
        PaperAgentInputArtifact[] expectedInputs = TaskInputs(
            dispatch,
            dispatchRef,
            dispatchRelativePath);
        RequireInputSetExactly(task.ExactInputs, expectedInputs);
    }

    private static string RequireRepositoryRootFromTaskPath(string path) =>
        throw new InvalidOperationException(
            $"No repository root can be inferred from repository-relative path {path}.");

    private static void RequireInputSetExactly(
        IReadOnlyList<PaperAgentInputArtifact> actual,
        IReadOnlyList<PaperAgentInputArtifact> expected)
    {
        string[] left = actual.Select(InputKey).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        string[] right = expected.Select(InputKey).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (!left.SequenceEqual(right, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Foundation-agent task changed its exact dispatch input closure.");
        }
    }

    private static string InputKey(PaperAgentInputArtifact input) =>
        $"{input.Schema}\n{input.ArtifactRef}\n{input.RepositoryRelativePath}";

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
        byte[] bytes = ReadBoundedFile(path, MaximumDispatchBytes, "Paper agent cursor");
        PaperAgentTaskCursor cursor =
            PaperResearchInputJson.DeserializeStrict<PaperAgentTaskCursor>(bytes);
        PaperAgentRuntimeService.Validate(cursor, task, taskRef);
        return cursor;
    }

    private static PaperAgentResultWire ReadAgentResult(
        string root,
        PaperAgentTask task,
        string taskRef,
        string resultRef)
    {
        byte[] bytes = ReadImmutable(
            AgentArtifactPath(root, "results", resultRef),
            resultRef,
            "Paper agent result");
        PaperAgentResultWire result =
            PaperResearchInputJson.DeserializeStrict<PaperAgentResultWire>(bytes);
        PaperAgentRuntimeService.Validate(result, task, taskRef);
        return result;
    }

    private static byte[] ReadAgentOutput(string root, string outputRef) =>
        ReadImmutable(
            AgentArtifactPath(root, "outputs", outputRef),
            outputRef,
            "Paper agent output");

    private static void ValidateAgentCursorResult(
        PaperAgentTaskCursor cursor,
        PaperAgentResultWire result)
    {
        if (!string.Equals(cursor.ResultRef, Reference(CanonicalJson.Serialize(result)), StringComparison.Ordinal)
            || !string.Equals(cursor.Status, result.Status, StringComparison.Ordinal)
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

    private static PaperTheoryFoundationAgentAdmissionCursor ReadAdmissionCursor(
        string path)
    {
        byte[] bytes = ReadBoundedFile(
            path,
            MaximumDispatchBytes,
            "Foundation admission cursor");
        PaperTheoryFoundationAgentAdmissionCursor cursor =
            PaperResearchInputJson.DeserializeStrict<PaperTheoryFoundationAgentAdmissionCursor>(
                bytes);
        Validate(cursor);
        return cursor;
    }

    private static void RequireDispatchIdentity(
        PaperTheoryFoundationAgentDispatch dispatch,
        string paperId,
        string theoryProgramRef,
        string requestRef,
        string requestedAt)
    {
        if (!string.Equals(dispatch.PaperId, paperId, StringComparison.Ordinal)
            || !string.Equals(dispatch.TheoryProgramRef, theoryProgramRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.RequestRef, requestRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.RequestedAt, requestedAt, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Theory-foundation dispatch changed its domain request identity or timestamp.");
        }
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
                "Theory-foundation dispatch changed the domain request exact-input closure.");
        }
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
            $"Theory-foundation dispatch is missing the exact {name} content artifact.");
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

    private static string ExpectedDraftSchema(string kind) =>
        kind switch
        {
            ScopeKind => PaperTheoryFoundationAgentSchemas.ScopeDraft,
            InventoryKind => PaperTheoryFoundationAgentSchemas.InventoryDraft,
            _ => throw new InvalidDataException(
                $"Unsupported theory-foundation kind {kind}.")
        };

    private static string ExpectedNextRoute(string kind) =>
        kind switch
        {
            ScopeKind => "theory-inventory",
            InventoryKind => "theory-deepening",
            _ => throw new InvalidDataException(
                $"Unsupported theory-foundation kind {kind}.")
        };

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
            throw new InvalidDataException("Theory-foundation dispatch path is required.");
        }
        string full = Path.GetFullPath(dispatchPath);
        string inbox = Path.GetFullPath(Path.Combine(root, "inbox", "theory-foundation"));
        RequirePathWithin(inbox, full, "Theory-foundation dispatch path");
        if (!File.Exists(full)
            || !string.Equals(Path.GetExtension(full), ".json", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Theory-foundation dispatch must be an existing JSON file in its deployment inbox.");
        }
        RejectReparsePointsBetween(inbox, full, "Theory-foundation dispatch path");
        return full;
    }

    private static string ArtifactPath(
        string root,
        string family,
        string reference,
        string extension)
    {
        string hex = Hex(reference);
        return Path.Combine(
            root,
            "artifacts",
            "paper-theory-foundation",
            family,
            "sha256",
            hex[..2],
            hex + extension);
    }

    private static string AgentArtifactPath(
        string root,
        string family,
        string reference)
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
            "paper-theory-foundation",
            "cursors",
            Hex(taskRef) + ".json");

    private static bool PutImmutable(string path, ReadOnlySpan<byte> bytes)
    {
        if (File.Exists(path))
        {
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
            {
                throw new InvalidDataException(
                    $"Content-address collision at {path}.");
            }
            return true;
        }
        PaperResearchInputStore.WriteAtomic(path, bytes, overwrite: false);
        return false;
    }

    private static byte[] ReadImmutable(
        string path,
        string expectedRef,
        string name)
    {
        RequireDigest(expectedRef, nameof(expectedRef));
        byte[] bytes = ReadBoundedFile(path, MaximumArtifactBytes, name);
        if (!string.Equals(Reference(bytes), expectedRef, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{name} failed content-address verification.");
        }
        return bytes;
    }

    private static byte[] ReadBoundedFile(
        string path,
        int maximumBytes,
        string name)
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

    private static void RejectReparsePointsBetween(
        string boundaryRoot,
        string path,
        string name)
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

    private static void RequireNotBefore(
        string actual,
        string lowerBound,
        string name)
    {
        if (ParseUtc(actual, name) < ParseUtc(lowerBound, "request timestamp"))
        {
            throw new InvalidDataException($"{name} cannot precede the domain request.");
        }
    }
}
