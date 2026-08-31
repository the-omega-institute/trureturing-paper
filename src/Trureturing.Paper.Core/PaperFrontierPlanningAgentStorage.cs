namespace Trureturing.Paper.Core;

public static partial class PaperFrontierPlanningAgentService
{
    private static PaperFrontierPlanningStoredArtifact StoreDomain<TContent, TEnvelope>(
        string root,
        string family,
        string schema,
        string artifactRef,
        TContent content,
        TEnvelope envelope)
    {
        byte[] contentBytes = CanonicalJson.Serialize(content);
        if (!string.Equals(ByteReference(contentBytes), artifactRef, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{schema} content identity does not match its canonical bytes.");
        }
        string contentPath = DomainArtifactPath(
            root,
            family,
            "content",
            artifactRef);
        _ = PutImmutable(contentPath, contentBytes);
        byte[] envelopeBytes = CanonicalJson.Serialize(envelope);
        string envelopeRef = ByteReference(envelopeBytes);
        string envelopePath = DomainArtifactPath(
            root,
            family,
            "envelopes",
            envelopeRef);
        _ = PutImmutable(envelopePath, envelopeBytes);
        return new PaperFrontierPlanningStoredArtifact(
            schema,
            artifactRef,
            RelativePath(root, contentPath),
            envelopeRef,
            RelativePath(root, envelopePath));
    }

    private static T ReadStoredEnvelope<T>(
        string root,
        PaperFrontierPlanningStoredArtifact stored)
    {
        ValidateStoredArtifact(stored, stored.Schema);
        byte[] bytes = ReadRepositoryArtifact(
            root,
            stored.EnvelopePath,
            stored.EnvelopeRef,
            "Frontier-planning stored envelope");
        return PaperResearchInputJson.DeserializeStrict<T>(bytes);
    }

    private static void ValidateStoredArtifact(
        PaperFrontierPlanningStoredArtifact stored,
        string expectedSchema)
    {
        ArgumentNullException.ThrowIfNull(stored);
        RequireExact(stored.Schema, expectedSchema, nameof(stored.Schema));
        RequireDigest(stored.ArtifactRef, nameof(stored.ArtifactRef));
        RequireRepositoryRelativePath(stored.ContentPath, nameof(stored.ContentPath));
        RequireDigest(stored.EnvelopeRef, nameof(stored.EnvelopeRef));
        RequireRepositoryRelativePath(stored.EnvelopePath, nameof(stored.EnvelopePath));
    }

    private static PaperAgentTask ReadRegisteredTask(string root, string taskRef)
    {
        byte[] bytes = ReadImmutable(
            GenericAgentArtifactPath(root, "tasks", taskRef),
            taskRef,
            "Registered frontier-planning task");
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
        PaperAgentTaskCursor cursor =
            PaperResearchInputJson.DeserializeStrict<PaperAgentTaskCursor>(
                ReadBoundedFile(
                    GenericAgentCursorPath(root, taskRef),
                    MaximumControlBytes,
                    "Frontier-planning generic agent cursor"));
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
            GenericAgentArtifactPath(root, "results", resultRef),
            resultRef,
            "Frontier-planning generic agent result");
        PaperAgentResultWire result =
            PaperResearchInputJson.DeserializeStrict<PaperAgentResultWire>(bytes);
        PaperAgentRuntimeService.Validate(result, task, taskRef);
        return result;
    }

    private static void RequireCursorMatchesResult(
        PaperAgentTaskCursor cursor,
        PaperAgentResultWire result)
    {
        if (!string.Equals(cursor.Status, result.Status, StringComparison.Ordinal)
            || !string.Equals(cursor.Summary, result.Summary, StringComparison.Ordinal)
            || !string.Equals(cursor.NextRoute, result.NextRoute, StringComparison.Ordinal)
            || !string.Equals(cursor.BlockerCode, result.BlockerCode, StringComparison.Ordinal)
            || !string.Equals(cursor.CompletedAt, result.CompletedAt, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Frontier-planning generic cursor does not match its immutable result.");
        }
    }

    private static byte[] ReadAgentOutput(string root, string outputRef) =>
        ReadImmutable(
            GenericAgentArtifactPath(root, "outputs", outputRef),
            outputRef,
            "Frontier-planning draft output");

    private static T ReadContent<T>(
        string root,
        PaperAgentInputArtifact input) =>
        PaperResearchInputJson.DeserializeStrict<T>(ReadExactInput(root, input));

    private static byte[] ReadExactInput(
        string root,
        PaperAgentInputArtifact input) =>
        ReadRepositoryArtifact(
            root,
            input.RepositoryRelativePath,
            input.ArtifactRef,
            $"Exact input {input.Schema}");

    private static PaperAgentInputArtifact FindInput(
        IReadOnlyList<PaperAgentInputArtifact> inputs,
        string schema,
        string reference) =>
        inputs.SingleOrDefault(input =>
                string.Equals(input.Schema, schema, StringComparison.Ordinal)
                && string.Equals(input.ArtifactRef, reference, StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                $"Frontier planning is missing exact input {schema} at {reference}.");

    private static void ValidateInputSources(
        string root,
        IReadOnlyList<PaperAgentInputArtifact> inputs)
    {
        foreach (PaperAgentInputArtifact input in inputs)
        {
            _ = ReadExactInput(root, input);
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

    private static byte[] ReadRepositoryArtifact(
        string root,
        string relativePath,
        string expectedRef,
        string name)
    {
        RequireRepositoryRelativePath(relativePath, name);
        string first = relativePath.Split('/')[0];
        if (!AllowedEvidenceRoots.Contains(first))
        {
            throw new InvalidDataException(
                $"{name} is outside approved evidence roots.");
        }
        string full = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        RequirePathWithin(root, full, name);
        RejectReparsePointsBetween(root, full, name);
        return ReadImmutable(full, expectedRef, name);
    }

    private static string PortfolioAdmissionCursorPath(
        string root,
        string taskRef) =>
        Path.Combine(
            root,
            "work",
            "paper-portfolio-judgments",
            "cursors",
            Hex(taskRef) + ".json");

    private static string PortfolioDispatchPath(
        string root,
        string dispatchRef) =>
        PortfolioDomainArtifactPath(
            root,
            "dispatches",
            "raw",
            dispatchRef);

    private static string PortfolioDomainArtifactPath(
        string root,
        string family,
        string kind,
        string reference)
    {
        RequireDigest(reference, nameof(reference));
        string hex = Hex(reference);
        return Path.Combine(
            root,
            "artifacts",
            "paper-portfolio-judgments",
            family,
            kind,
            "sha256",
            hex[..2],
            hex + ".json");
    }

    private static string DomainArtifactPath(
        string root,
        string family,
        string kind,
        string reference)
    {
        RequireDigest(reference, nameof(reference));
        string hex = Hex(reference);
        return Path.Combine(
            root,
            "artifacts",
            "paper-frontier-planning",
            family,
            kind,
            "sha256",
            hex[..2],
            hex + ".json");
    }

    private static string GenericAgentArtifactPath(
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

    private static string GenericAgentCursorPath(string root, string taskRef) =>
        Path.Combine(
            root,
            "work",
            "paper-agents",
            "cursors",
            Hex(taskRef) + ".json");

    private static string AdmissionCursorPath(string root, string taskRef) =>
        Path.Combine(
            root,
            "work",
            "paper-frontier-planning",
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
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        PaperResearchInputStore.WriteAtomic(path, bytes, overwrite: false);
        return false;
    }

    private static byte[] ReadImmutable(
        string path,
        string expectedRef,
        string name)
    {
        byte[] bytes = ReadBoundedFile(path, MaximumArtifactBytes, name);
        string actual = ByteReference(bytes);
        if (!string.Equals(actual, expectedRef, StringComparison.Ordinal))
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
        var info = new FileInfo(path);
        if (info.Length < 1 || info.Length > maximumBytes)
        {
            throw new InvalidDataException(
                $"{name} must contain between one and {maximumBytes} bytes.");
        }
        return File.ReadAllBytes(path);
    }

    private static string RelativePath(string root, string path) =>
        Path.GetRelativePath(root, path)
            .Replace(Path.DirectorySeparatorChar, '/');

    private static void RequirePathWithin(
        string root,
        string path,
        string name)
    {
        string normalizedRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string normalizedPath = Path.GetFullPath(path);
        if (!normalizedPath.StartsWith(normalizedRoot, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{name} escapes its owned filesystem boundary.");
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
            throw new InvalidDataException(
                $"{name} cannot traverse a symbolic link.");
        }
    }

    private static string ByteReference(ReadOnlySpan<byte> bytes) =>
        PaperResearchInputStore.Reference(bytes);

    private static string Hex(string reference) =>
        reference["sha256:".Length..];

    private static void RequireSameSet(
        IReadOnlyList<string> actual,
        IReadOnlyList<string> expected,
        string name)
    {
        if (actual.Count != expected.Count
            || !actual.OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(
                    expected.OrderBy(value => value, StringComparer.Ordinal),
                    StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                $"{name} changed its exact evidence set.");
        }
    }

    private static void RequireTextList(
        IReadOnlyList<string>? values,
        string name,
        int maximumItemLength,
        int minimum,
        int maximum)
    {
        if (values is null || values.Count < minimum || values.Count > maximum)
        {
            throw new InvalidDataException($"{name} has an invalid count.");
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
        {
            RequireText(value, name, maximumItemLength, 1);
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
        int minimumLength)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length < minimumLength
            || value.Length > maximumLength)
        {
            throw new InvalidDataException(
                $"{name} must contain between {minimumLength} and {maximumLength} characters.");
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

    private static void RequireDigest(string value, string name)
    {
        if (!DigestPattern.IsMatch(value ?? string.Empty))
        {
            throw new InvalidDataException(
                $"{name} must be sha256:<64 lowercase hex>.");
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

    private static void RequireRepositoryRelativePath(
        string value,
        string name)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 4096
            || Path.IsPathRooted(value)
            || value.Contains('\\')
            || !RelativePathPattern.IsMatch(value))
        {
            throw new InvalidDataException(
                $"{name} is not a canonical relative path.");
        }
        foreach (string segment in value.Split('/'))
        {
            if (segment is "." or ".."
                || segment.All(character => character == '.'))
            {
                throw new InvalidDataException(
                    $"{name} contains an unsafe path segment.");
            }
        }
    }

    private static void RequireExact(
        string actual,
        string expected,
        string name)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{name} must be {expected}.");
        }
    }

    private static void RequireRunId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 512
            || value.Contains('\n')
            || value.Contains('\r'))
        {
            throw new InvalidDataException(
                "Frontier-planning run_id is invalid.");
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
            throw new InvalidDataException(
                $"{name} must be an RFC 3339 timestamp.");
        }
        return parsed;
    }
}
