using System.Globalization;
using System.Text.Json;

namespace Trureturing.Paper.Core;

public static partial class PaperManuscriptAuthoringAgentService
{
    private static PaperAgentTask ReadRegisteredTask(
        string root,
        string taskRef)
    {
        byte[] bytes = ReadImmutable(
            AgentArtifactPath(root, "tasks", taskRef),
            taskRef,
            "Registered manuscript-authoring task");
        PaperAgentTask task =
            PaperResearchInputJson.DeserializeStrict<PaperAgentTask>(bytes);
        PaperAgentRuntimeService.Validate(task);
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
                ReadBoundedFile(
                    path,
                    MaximumControlBytes,
                    "Manuscript-authoring generic agent cursor"));
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
            "Manuscript-authoring generic agent result");
        PaperAgentResultWire result =
            PaperResearchInputJson.DeserializeStrict<PaperAgentResultWire>(bytes);
        PaperAgentRuntimeService.Validate(result, task, taskRef);
        return result;
    }

    private static byte[] ReadAgentOutput(string root, string outputRef) =>
        ReadImmutable(
            AgentArtifactPath(root, "outputs", outputRef),
            outputRef,
            "Manuscript-authoring agent output");

    private static byte[] ReadExactInput(
        string root,
        PaperAgentInputArtifact input)
    {
        RequireSchema(input.Schema, nameof(input.Schema));
        RequireDigest(input.ArtifactRef, nameof(input.ArtifactRef));
        RequireRelativePath(
            input.RepositoryRelativePath,
            nameof(input.RepositoryRelativePath));
        string full = ResolveRepositoryFile(
            root,
            input.RepositoryRelativePath,
            "Manuscript-authoring exact input");
        return ReadImmutable(
            full,
            input.ArtifactRef,
            "Manuscript-authoring exact input");
    }

    private static void ValidateTaskBinding(
        string root,
        PaperAgentTask task,
        PaperManuscriptAuthoringAgentDispatch dispatch,
        string dispatchRef,
        string dispatchPath,
        PaperManuscriptAuthoringContext context)
    {
        PaperAgentRuntimeService.Validate(task);
        PaperAgentProfile profile =
            PaperAgentRuntimeService.GetProfile("manuscript-authoring");
        if (!string.Equals(task.PaperId, dispatch.PaperId, StringComparison.Ordinal)
            || !string.Equals(
                task.TheoryProgramRef,
                dispatch.TheoryProgramRef,
                StringComparison.Ordinal)
            || !string.Equals(task.Phase, profile.Phase, StringComparison.Ordinal)
            || !string.Equals(task.AgentRole, profile.AgentRole, StringComparison.Ordinal)
            || !string.Equals(
                task.ContextMode,
                profile.ContextMode,
                StringComparison.Ordinal)
            || !string.Equals(
                task.RequestedAt,
                dispatch.RequestedAt,
                StringComparison.Ordinal)
            || task.ExpectedOutputs.Count != 1
            || !string.Equals(
                task.ExpectedOutputs[0].Schema,
                PaperManuscriptAuthoringAgentSchemas.Draft,
                StringComparison.Ordinal)
            || !string.Equals(
                task.ExpectedOutputs[0].WorkspaceRelativePath,
                "outputs/scientific-manuscript-draft.json",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Manuscript-authoring task changed its phase, paper, program, timestamp, or output contract.");
        }
        string[] expectedRoutes =
            ["blocked", "manuscript-authoring", "scientific-editing"];
        if (!task.AllowedNextRoutes
                .OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(expectedRoutes, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Manuscript-authoring task changed its closed route set.");
        }
        PaperAgentInputArtifact[] expectedInputs = dispatch.ExactInputs
            .Append(new PaperAgentInputArtifact(
                PaperManuscriptAuthoringAgentSchemas.Dispatch,
                dispatchRef,
                dispatchPath))
            .OrderBy(value => value.Schema, StringComparer.Ordinal)
            .ThenBy(value => value.ArtifactRef, StringComparer.Ordinal)
            .ToArray();
        if (task.ExactInputs.Count != expectedInputs.Length)
        {
            throw new InvalidDataException(
                "Manuscript-authoring task changed its exact input count.");
        }
        for (int index = 0; index < expectedInputs.Length; index++)
        {
            PaperAgentInputArtifact expected = expectedInputs[index];
            PaperAgentInputArtifact actual = task.ExactInputs[index];
            if (!string.Equals(actual.Schema, expected.Schema, StringComparison.Ordinal)
                || !string.Equals(
                    actual.ArtifactRef,
                    expected.ArtifactRef,
                    StringComparison.Ordinal)
                || !string.Equals(
                    actual.RepositoryRelativePath,
                    expected.RepositoryRelativePath,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Manuscript-authoring task changed its exact evidence closure.");
            }
            _ = ReadExactInput(root, actual);
        }
        if (!string.Equals(dispatch.ManuscriptPlanRef, context.Evaluation.ManuscriptPlanRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.CompletionRef, context.CompletionCursor.CompletionRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.FrontierRef, context.CompletionCursor.FrontierRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.ScopeRef, context.Planning.Scope.ScopeId, StringComparison.Ordinal)
            || !string.Equals(dispatch.InventoryRef, context.Planning.Inventory.InventoryId, StringComparison.Ordinal)
            || !string.Equals(dispatch.TheoremPackageRef, context.Planning.TheoremPackage.TheoremPackageId, StringComparison.Ordinal)
            || !string.Equals(dispatch.TheoryAuditRef, context.Planning.Audit.AuditId, StringComparison.Ordinal)
            || !string.Equals(dispatch.SelectedReleaseRef, context.Plan.ManuscriptTruthReleaseRef, StringComparison.Ordinal)
            || !string.Equals(dispatch.SelectedReleaseDigest, context.SelectedRelease.ReleaseDigest, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Manuscript-authoring task dispatch changed its completed frontier lineage.");
        }
    }

    private static void RequireCursorMatchesResult(
        PaperAgentTaskCursor cursor,
        PaperAgentResultWire result)
    {
        if (!string.Equals(cursor.ResultRef, Reference(CanonicalJson.Serialize(result)), StringComparison.Ordinal)
            || !string.Equals(cursor.TaskRef, result.TaskRef, StringComparison.Ordinal)
            || !string.Equals(cursor.PaperId, result.PaperId, StringComparison.Ordinal)
            || !string.Equals(cursor.TheoryProgramRef, result.TheoryProgramRef, StringComparison.Ordinal)
            || !string.Equals(cursor.Phase, result.Phase, StringComparison.Ordinal)
            || !string.Equals(cursor.AgentRole, result.AgentRole, StringComparison.Ordinal)
            || !string.Equals(cursor.ContextMode, result.ContextMode, StringComparison.Ordinal)
            || !string.Equals(cursor.Status, result.Status, StringComparison.Ordinal)
            || !string.Equals(cursor.Summary, result.Summary, StringComparison.Ordinal)
            || !string.Equals(cursor.NextRoute, result.NextRoute, StringComparison.Ordinal)
            || !string.Equals(cursor.BlockerCode, result.BlockerCode, StringComparison.Ordinal)
            || !string.Equals(cursor.CompletedAt, result.CompletedAt, StringComparison.Ordinal)
            || cursor.Outputs.Count != result.Outputs.Count)
        {
            throw new InvalidDataException(
                "Manuscript-authoring generic cursor does not match its immutable result.");
        }
        for (int index = 0; index < cursor.Outputs.Count; index++)
        {
            if (!string.Equals(
                    cursor.Outputs[index].Schema,
                    result.Outputs[index].Schema,
                    StringComparison.Ordinal)
                || !string.Equals(
                    cursor.Outputs[index].WorkspaceRelativePath,
                    result.Outputs[index].WorkspaceRelativePath,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Manuscript-authoring generic cursor changed an output coordinate.");
            }
        }
    }

    private static PaperManuscriptSourceFile StoreSource(
        string root,
        string role,
        string mediaType,
        string extension,
        ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 1 || bytes.Length > MaximumArtifactBytes)
        {
            throw new InvalidDataException(
                "Manuscript source size is outside the bounded artifact policy.");
        }
        string reference = Reference(bytes);
        string path = DomainArtifactPath(
            root,
            "sources",
            role,
            reference,
            extension);
        _ = PutImmutable(path, bytes);
        var coordinate = new PaperManuscriptSourceFile(
            role,
            mediaType,
            reference,
            RelativePath(root, path),
            bytes.Length);
        ValidateSourceCoordinate(coordinate, role, mediaType);
        return coordinate;
    }

    private static PaperManuscriptAuthoringStoredArtifact StoreDomain<TContent, TEnvelope>(
        string root,
        string family,
        string schema,
        string artifactRef,
        TContent content,
        TEnvelope envelope)
    {
        byte[] contentBytes = CanonicalJson.Serialize(content);
        if (!string.Equals(Reference(contentBytes), artifactRef, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Stored {schema} content does not match its semantic identity.");
        }
        string contentPath = DomainArtifactPath(
            root,
            family,
            "content",
            artifactRef,
            ".json");
        _ = PutImmutable(contentPath, contentBytes);
        byte[] envelopeBytes = CanonicalJson.Serialize(envelope);
        string envelopeRef = Reference(envelopeBytes);
        string envelopePath = DomainArtifactPath(
            root,
            family,
            "envelopes",
            envelopeRef,
            ".json");
        _ = PutImmutable(envelopePath, envelopeBytes);
        var stored = new PaperManuscriptAuthoringStoredArtifact(
            schema,
            artifactRef,
            RelativePath(root, contentPath),
            envelopeRef,
            RelativePath(root, envelopePath));
        ValidateStoredArtifact(stored, schema);
        return stored;
    }

    private static PaperManuscriptAuthoringAgentResultAdmitted ReplayAdmission(
        string root,
        PaperManuscriptAuthoringAgentAdmissionCursor cursor,
        string taskRef,
        PaperAgentTaskCursor agentCursor,
        PaperManuscriptAuthoringAgentDispatch dispatch,
        string dispatchRef,
        PaperManuscriptAuthoringContext context)
    {
        Validate(cursor);
        if (!string.Equals(cursor.TaskRef, taskRef, StringComparison.Ordinal)
            || !string.Equals(cursor.ResultRef, agentCursor.ResultRef, StringComparison.Ordinal)
            || !string.Equals(cursor.DispatchRef, dispatchRef, StringComparison.Ordinal)
            || !string.Equals(cursor.PaperId, dispatch.PaperId, StringComparison.Ordinal)
            || !string.Equals(cursor.TheoryProgramRef, dispatch.TheoryProgramRef, StringComparison.Ordinal)
            || !string.Equals(cursor.CompletionRef, dispatch.CompletionRef, StringComparison.Ordinal)
            || !string.Equals(cursor.EvaluationRef, dispatch.EvaluationRef, StringComparison.Ordinal)
            || !string.Equals(cursor.ClaimManifestRef, dispatch.ClaimManifestRef, StringComparison.Ordinal)
            || !string.Equals(cursor.EligibilityRef, dispatch.EligibilityRef, StringComparison.Ordinal)
            || !string.Equals(cursor.ManuscriptPlanRef, dispatch.ManuscriptPlanRef, StringComparison.Ordinal)
            || !string.Equals(cursor.RunId, agentCursor.RunId, StringComparison.Ordinal)
            || !string.Equals(cursor.Provenance, agentCursor.Provenance, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Manuscript-authoring replay changed its task, result, evidence, or run identity.");
        }
        byte[] mainTex = ReadSource(root, cursor.MainTex);
        byte[] bibliography = ReadSource(root, cursor.Bibliography);
        byte[] envelopeBytes = ReadImmutable(
            ResolveRepositoryFile(
                root,
                cursor.Manuscript.EnvelopePath,
                "Scientific manuscript envelope"),
            cursor.Manuscript.EnvelopeRef,
            "Scientific manuscript envelope");
        PaperScientificManuscript manuscript =
            PaperResearchInputJson.DeserializeStrict<PaperScientificManuscript>(
                envelopeBytes);
        if (!string.Equals(
                manuscript.ManuscriptId,
                cursor.Manuscript.ArtifactRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Manuscript-authoring replay changed the stored manuscript identity.");
        }
        Validate(manuscript, context, mainTex, bibliography);
        if (!string.Equals(
                manuscript.ManuscriptContent.MainTex.ArtifactRef,
                cursor.MainTex.ArtifactRef,
                StringComparison.Ordinal)
            || !string.Equals(
                manuscript.ManuscriptContent.Bibliography.ArtifactRef,
                cursor.Bibliography.ArtifactRef,
                StringComparison.Ordinal)
            || manuscript.ManuscriptContent.FormalClaimCount
                != cursor.FormalClaimCount
            || manuscript.ManuscriptContent.InformalItemCount
                != cursor.InformalItemCount)
        {
            throw new InvalidDataException(
                "Manuscript-authoring replay changed source or item counts.");
        }
        return Recorded(cursor, replayed: true);
    }

    private static PaperManuscriptAuthoringAgentResultAdmitted Recorded(
        PaperManuscriptAuthoringAgentAdmissionCursor cursor,
        bool replayed) =>
        new(
            PaperManuscriptAuthoringAgentSchemas.ResultAdmitted,
            cursor.TaskRef,
            cursor.ResultRef,
            cursor.DispatchRef,
            cursor.PaperId,
            cursor.TheoryProgramRef,
            cursor.CompletionRef,
            cursor.EvaluationRef,
            cursor.ClaimManifestRef,
            cursor.EligibilityRef,
            cursor.ManuscriptPlanRef,
            cursor.Manuscript,
            cursor.MainTex,
            cursor.Bibliography,
            cursor.FormalClaimCount,
            cursor.InformalItemCount,
            cursor.NextRoute,
            cursor.RunId,
            cursor.Provenance,
            cursor.AdmittedAt,
            replayed);

    private static PaperManuscriptAuthoringAgentAdmissionCursor ReadAdmissionCursor(
        string path)
    {
        PaperManuscriptAuthoringAgentAdmissionCursor cursor =
            PaperResearchInputJson.DeserializeStrict<
                PaperManuscriptAuthoringAgentAdmissionCursor>(
                    ReadBoundedFile(
                        path,
                        MaximumControlBytes,
                        "Manuscript-authoring admission cursor"));
        Validate(cursor);
        return cursor;
    }

    private static byte[] ReadSource(
        string root,
        PaperManuscriptSourceFile source)
    {
        ValidateSourceCoordinate(source, source.Role, source.MediaType);
        return ReadImmutable(
            ResolveRepositoryFile(
                root,
                source.RepositoryRelativePath,
                $"Manuscript source {source.Role}"),
            source.ArtifactRef,
            $"Manuscript source {source.Role}");
    }

    private static void ValidateSourceFile(
        PaperManuscriptSourceFile source,
        ReadOnlySpan<byte> bytes,
        string role,
        string mediaType)
    {
        ValidateSourceCoordinate(source, role, mediaType);
        if (!string.Equals(
                Reference(bytes),
                source.ArtifactRef,
                StringComparison.Ordinal)
            || source.SizeBytes != bytes.Length)
        {
            throw new InvalidDataException(
                $"Manuscript source {role} no longer matches its stored bytes.");
        }
    }

    private static void ValidateSourceCoordinate(
        PaperManuscriptSourceFile source,
        string role,
        string mediaType)
    {
        ArgumentNullException.ThrowIfNull(source);
        RequireExact(source.Role, role, nameof(source.Role));
        RequireExact(source.MediaType, mediaType, nameof(source.MediaType));
        RequireDigest(source.ArtifactRef, nameof(source.ArtifactRef));
        RequireRelativePath(
            source.RepositoryRelativePath,
            nameof(source.RepositoryRelativePath));
        if (source.SizeBytes < 1 || source.SizeBytes > MaximumArtifactBytes)
        {
            throw new InvalidDataException(
                "Manuscript source coordinate has an invalid size.");
        }
    }

    private static void ValidateStoredArtifact(
        PaperManuscriptAuthoringStoredArtifact stored,
        string schema)
    {
        ArgumentNullException.ThrowIfNull(stored);
        RequireExact(stored.Schema, schema, nameof(stored.Schema));
        RequireDigest(stored.ArtifactRef, nameof(stored.ArtifactRef));
        RequireDigest(stored.EnvelopeRef, nameof(stored.EnvelopeRef));
        RequireRelativePath(stored.ContentPath, nameof(stored.ContentPath));
        RequireRelativePath(stored.EnvelopePath, nameof(stored.EnvelopePath));
    }

    private static string AdmissionCursorPath(string root, string taskRef) =>
        Path.Combine(
            root,
            "work",
            "paper-manuscript-authoring",
            "cursors",
            Hex(taskRef) + ".json");

    private static string AgentArtifactPath(
        string root,
        string family,
        string reference)
    {
        RequireDigest(reference, nameof(reference));
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

    private static string DomainArtifactPath(
        string root,
        string family,
        string representation,
        string reference,
        string extension)
    {
        RequireDigest(reference, nameof(reference));
        string hex = Hex(reference);
        return Path.Combine(
            root,
            "artifacts",
            "paper-manuscript-authoring",
            family,
            representation,
            "sha256",
            hex[..2],
            hex + extension);
    }

    private static string RequireRepositoryRoot(string repositoryRoot)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            throw new InvalidDataException(
                "Paper repository root is required.");
        }
        string root = Path.GetFullPath(repositoryRoot);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException(
                $"Paper repository root does not exist: {root}");
        }
        return root;
    }

    private static string ResolveRepositoryFile(
        string root,
        string relativePath,
        string name)
    {
        RequireRelativePath(relativePath, name);
        string full = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string normalizedRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!full.StartsWith(normalizedRoot, StringComparison.Ordinal)
            || !File.Exists(full))
        {
            throw new InvalidDataException(
                $"{name} is outside the repository or missing.");
        }
        RejectReparsePoints(root, full, name);
        return full;
    }

    private static void RejectReparsePoints(
        string root,
        string full,
        string name)
    {
        string current = Path.GetFullPath(root);
        if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                $"{name} cannot traverse a symbolic link.");
        }
        string relative = Path.GetRelativePath(current, full);
        foreach (string segment in relative.Split(
                     Path.DirectorySeparatorChar,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if ((Directory.Exists(current) || File.Exists(current))
                && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException(
                    $"{name} cannot traverse a symbolic link.");
            }
        }
    }

    private static string RelativePath(string root, string path)
    {
        string relative = Path.GetRelativePath(root, path)
            .Replace(Path.DirectorySeparatorChar, '/');
        RequireRelativePath(relative, nameof(relative));
        return relative;
    }

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
        byte[] bytes = ReadBoundedFile(path, MaximumArtifactBytes, name);
        if (!string.Equals(
                Reference(bytes),
                expectedRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{name} failed content-address verification.");
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
        var information = new FileInfo(path);
        if (information.Length < 1 || information.Length > maximumBytes)
        {
            throw new InvalidDataException(
                $"{name} must contain between one and {maximumBytes} bytes.");
        }
        return File.ReadAllBytes(path);
    }

    private static string Reference(ReadOnlySpan<byte> bytes) =>
        PaperResearchInputStore.Reference(bytes);

    private static string Hex(string reference) =>
        reference["sha256:".Length..];

    private static void RequireIdentity<T>(
        string reference,
        T content,
        string name)
    {
        RequireDigest(reference, name);
        if (!string.Equals(
                reference,
                Reference(CanonicalJson.Serialize(content)),
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{name} does not address canonical content bytes.");
        }
    }

    private static void RequireSchema(string value, string name)
    {
        if (!SchemaPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException(
                $"{name} is not a versioned schema name.");
        }
    }

    private static void RequireRelativePath(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 4096
            || Path.IsPathRooted(value)
            || value.Contains('\\')
            || !RelativePathPattern.IsMatch(value))
        {
            throw new InvalidDataException(
                $"{name} is not a canonical repository-relative path.");
        }
        string first = value.Split('/')[0];
        if (first is not (
                "artifacts" or "Papers" or "work" or "contracts"
                or "docs" or "src" or "tools" or "tests"))
        {
            throw new InvalidDataException(
                $"{name} is outside approved Paper evidence roots.");
        }
        if (value.Split('/').Any(segment =>
                segment is "." or ".."
                || segment.All(character => character == '.')))
        {
            throw new InvalidDataException(
                $"{name} contains an unsafe path segment.");
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

    private static void RequirePaperId(string value)
    {
        if (!PaperIdPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException(
                "paper_id is not a canonical identifier.");
        }
    }

    private static void RequireRunId(string value)
    {
        if (value is null
            || value.Length > 512
            || value.Contains('\n')
            || value.Contains('\r'))
        {
            throw new InvalidDataException(
                "Manuscript-authoring run_id is invalid.");
        }
    }

    private static void RequireExact(
        string actual,
        string expected,
        string name)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{name} must be '{expected}'.");
        }
    }

    private static void RequireText(
        string value,
        string name,
        int minimumLength,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length < minimumLength
            || value.Length > maximumLength)
        {
            throw new InvalidDataException(
                $"{name} must contain between {minimumLength} and {maximumLength} characters.");
        }
    }

    private static void RequireStringList(
        IReadOnlyList<string>? values,
        string name,
        int minimum,
        int maximum,
        int maximumItemLength)
    {
        if (values is null || values.Count < minimum || values.Count > maximum)
        {
            throw new InvalidDataException(
                $"{name} must contain between {minimum} and {maximum} values.");
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            RequireText(value, name, 1, maximumItemLength);
            if (!seen.Add(value))
            {
                throw new InvalidDataException(
                    $"{name} must contain unique values.");
            }
        }
    }

    private static DateTimeOffset ParseUtc(string value, string name)
    {
        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal
                    | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset parsed))
        {
            throw new InvalidDataException(
                $"{name} must be an RFC 3339 timestamp.");
        }
        return parsed;
    }
}
