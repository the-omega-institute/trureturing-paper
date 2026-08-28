using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trureturing.Paper.Core;

public static class PaperResearchInputSchemas
{
    public const string TopologyPublication = "trureturing.topology-publication.v1";
    public const string IntuitionPublication = "trureturing.intuition-publication.v1";
    public const string IntuitionTopologyReceipt =
        "intuition-topology-input-receipt.v1";
    public const string TopologyReceipt = "paper-topology-input-receipt.v1";
    public const string IntuitionReceipt = "paper-intuition-input-receipt.v1";
    public const string InputCursor = "paper-research-input-cursor.v1";
    public const string ResearchInput = "paper-research-input.v1";
    public const string JoinCursor = "paper-research-join-cursor.v1";
}

public sealed record PaperTopologyPublication(
    string Schema,
    string TruthReleaseDigest,
    string TopologyDigest,
    string SourceCommit,
    string SourceTree,
    string AlgorithmProfileDigest,
    string ProducerCommit);

public sealed record PaperIntuitionPublication(
    string Schema,
    string TopologyInputReceiptRef,
    string IntuitionReleaseRef,
    string IntuitionReleaseDigest,
    string ProducerCommit);

public sealed record IntuitionTopologyInputReceiptWire(
    string Schema,
    string PublicationRef,
    string TopologyRef,
    string TruthReleaseDigest,
    string TopologyDigest,
    string SourceCommit,
    string SourceTree,
    string AlgorithmProfileDigest,
    string ProducerCommit);

public sealed record PaperTopologyInputReceipt(
    string Schema,
    string PublicationRef,
    string TopologyRef,
    string TruthReleaseDigest,
    string TopologyDigest,
    string SourceCommit,
    string SourceTree,
    string AlgorithmProfileDigest,
    string ProducerCommit);

public sealed record PaperIntuitionInputReceipt(
    string Schema,
    string PublicationRef,
    string TopologyInputReceiptRef,
    string IntuitionReleaseRef,
    string TruthReleaseDigest,
    string TopologyDigest,
    string SourceCommit,
    string SourceTree,
    string IntuitionProducerCommit);

public sealed record PaperResearchInputCursor(
    string Schema,
    string Kind,
    string ReceiptRef,
    string TruthReleaseDigest,
    string TopologyDigest);

public sealed record PaperResearchInput(
    string Schema,
    string TruthReleaseDigest,
    string TopologyDigest,
    string SourceCommit,
    string SourceTree,
    string TopologyReceiptRef,
    string IntuitionReceiptRef,
    string IntuitionReleaseRef);

public sealed record PaperResearchJoinCursor(
    string Schema,
    string ResearchInputRef,
    string TruthReleaseDigest,
    string TopologyDigest,
    string TopologyReceiptRef,
    string IntuitionReceiptRef);

public sealed record PaperResearchInputRegistration(
    string ReceiptRef,
    string CursorPath,
    string TruthReleaseDigest,
    string TopologyDigest,
    bool Replayed);

public sealed record PaperResearchInputJoinResult(
    string Status,
    string? ResearchInputRef,
    string? CursorPath,
    string? TruthReleaseDigest,
    string? TopologyDigest,
    bool Replayed);

public static class PaperResearchInputRegistry
{
    public static PaperResearchInputRegistration RegisterTopology(
        string durableRoot,
        PaperTopologyPublication publication,
        ReadOnlySpan<byte> topologyBytes,
        string cursorPath)
    {
        PaperResearchInputValidation.Validate(publication);
        string topologyRef = PaperResearchInputStore.Reference(topologyBytes);
        if (!string.Equals(
                topologyRef,
                publication.TopologyDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Topology bytes do not match publication.topology_digest.");
        }

        _ = CertifiedTopologyReader.Read(
            topologyBytes,
            new CertifiedTopologyBinding(
                publication.TruthReleaseDigest,
                publication.AlgorithmProfileDigest,
                publication.ProducerCommit));

        var store = new PaperResearchInputStore(durableRoot);
        string publicationRef = store.Put(publication);
        store.PutBlob(topologyRef, topologyBytes);
        var receipt = new PaperTopologyInputReceipt(
            PaperResearchInputSchemas.TopologyReceipt,
            publicationRef,
            topologyRef,
            publication.TruthReleaseDigest,
            topologyRef,
            publication.SourceCommit,
            publication.SourceTree,
            publication.AlgorithmProfileDigest,
            publication.ProducerCommit);
        PaperResearchInputValidation.Validate(receipt);
        string receiptRef = store.Put(receipt);
        return WriteInputCursor(
            store,
            cursorPath,
            "topology",
            receiptRef,
            receipt.TruthReleaseDigest,
            receipt.TopologyDigest);
    }

    public static PaperResearchInputRegistration RegisterIntuition(
        string durableRoot,
        PaperIntuitionPublication publication,
        ReadOnlySpan<byte> topologyReceiptBytes,
        ReadOnlySpan<byte> intuitionReleaseBytes,
        string cursorPath)
    {
        PaperResearchInputValidation.Validate(publication);
        string topologyReceiptRef =
            PaperResearchInputStore.Reference(topologyReceiptBytes);
        if (!string.Equals(
                topologyReceiptRef,
                publication.TopologyInputReceiptRef,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Intuition topology receipt bytes do not match the publication coordinate.");
        }

        IntuitionTopologyInputReceiptWire topologyReceipt =
            PaperResearchInputJson.DeserializeStrict<IntuitionTopologyInputReceiptWire>(
                topologyReceiptBytes);
        PaperResearchInputValidation.Validate(topologyReceipt);

        string intuitionReleaseRef =
            PaperResearchInputStore.Reference(intuitionReleaseBytes);
        if (!string.Equals(
                intuitionReleaseRef,
                publication.IntuitionReleaseRef,
                StringComparison.Ordinal)
            || !string.Equals(
                intuitionReleaseRef,
                publication.IntuitionReleaseDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Intuition release bytes do not match the publication coordinate.");
        }
        RequireIntuitionRelease(intuitionReleaseBytes);

        var store = new PaperResearchInputStore(durableRoot);
        string publicationRef = store.Put(publication);
        store.PutBlob(topologyReceiptRef, topologyReceiptBytes);
        store.PutBlob(intuitionReleaseRef, intuitionReleaseBytes);
        var receipt = new PaperIntuitionInputReceipt(
            PaperResearchInputSchemas.IntuitionReceipt,
            publicationRef,
            topologyReceiptRef,
            intuitionReleaseRef,
            topologyReceipt.TruthReleaseDigest,
            topologyReceipt.TopologyDigest,
            topologyReceipt.SourceCommit,
            topologyReceipt.SourceTree,
            publication.ProducerCommit);
        PaperResearchInputValidation.Validate(receipt);
        string receiptRef = store.Put(receipt);
        return WriteInputCursor(
            store,
            cursorPath,
            "intuition",
            receiptRef,
            receipt.TruthReleaseDigest,
            receipt.TopologyDigest);
    }

    public static PaperResearchInputJoinResult Join(
        string durableRoot,
        string topologyCursorPath,
        string intuitionCursorPath,
        string outputCursorPath)
    {
        if (!File.Exists(topologyCursorPath) || !File.Exists(intuitionCursorPath))
        {
            return new PaperResearchInputJoinResult(
                "waiting",
                null,
                null,
                null,
                null,
                false);
        }

        PaperResearchInputCursor topologyCursor =
            ReadInputCursor(topologyCursorPath, "topology");
        PaperResearchInputCursor intuitionCursor =
            ReadInputCursor(intuitionCursorPath, "intuition");
        if (!string.Equals(
                topologyCursor.TruthReleaseDigest,
                intuitionCursor.TruthReleaseDigest,
                StringComparison.Ordinal)
            || !string.Equals(
                topologyCursor.TopologyDigest,
                intuitionCursor.TopologyDigest,
                StringComparison.Ordinal))
        {
            return new PaperResearchInputJoinResult(
                "waiting",
                null,
                null,
                null,
                null,
                false);
        }

        var store = new PaperResearchInputStore(durableRoot);
        PaperTopologyInputReceipt topology =
            store.Get<PaperTopologyInputReceipt>(topologyCursor.ReceiptRef);
        PaperIntuitionInputReceipt intuition =
            store.Get<PaperIntuitionInputReceipt>(intuitionCursor.ReceiptRef);
        PaperResearchInputValidation.Validate(topology);
        PaperResearchInputValidation.Validate(intuition);

        if (!string.Equals(
                topology.TruthReleaseDigest,
                intuition.TruthReleaseDigest,
                StringComparison.Ordinal)
            || !string.Equals(
                topology.TopologyDigest,
                intuition.TopologyDigest,
                StringComparison.Ordinal)
            || !string.Equals(
                topology.SourceCommit,
                intuition.SourceCommit,
                StringComparison.Ordinal)
            || !string.Equals(
                topology.SourceTree,
                intuition.SourceTree,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Topology and Intuition receipts do not describe one exact research state.");
        }

        var input = new PaperResearchInput(
            PaperResearchInputSchemas.ResearchInput,
            topology.TruthReleaseDigest,
            topology.TopologyDigest,
            topology.SourceCommit,
            topology.SourceTree,
            topologyCursor.ReceiptRef,
            intuitionCursor.ReceiptRef,
            intuition.IntuitionReleaseRef);
        PaperResearchInputValidation.Validate(input);
        string inputRef = store.Put(input);

        string fullOutputCursor = Path.GetFullPath(outputCursorPath);
        if (File.Exists(fullOutputCursor))
        {
            PaperResearchJoinCursor current =
                PaperResearchInputJson.DeserializeStrict<PaperResearchJoinCursor>(
                    File.ReadAllBytes(fullOutputCursor));
            PaperResearchInputValidation.Validate(current);
            if (string.Equals(
                    current.TruthReleaseDigest,
                    input.TruthReleaseDigest,
                    StringComparison.Ordinal))
            {
                if (!string.Equals(
                        current.ResearchInputRef,
                        inputRef,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "One truth release cannot be rebound to different Paper research inputs.");
                }
                return new PaperResearchInputJoinResult(
                    "ready",
                    inputRef,
                    fullOutputCursor,
                    input.TruthReleaseDigest,
                    input.TopologyDigest,
                    true);
            }
        }

        var joinCursor = new PaperResearchJoinCursor(
            PaperResearchInputSchemas.JoinCursor,
            inputRef,
            input.TruthReleaseDigest,
            input.TopologyDigest,
            input.TopologyReceiptRef,
            input.IntuitionReceiptRef);
        PaperResearchInputValidation.Validate(joinCursor);
        PaperResearchInputStore.WriteAtomic(
            fullOutputCursor,
            CanonicalJson.Serialize(joinCursor));
        return new PaperResearchInputJoinResult(
            "ready",
            inputRef,
            fullOutputCursor,
            input.TruthReleaseDigest,
            input.TopologyDigest,
            false);
    }

    private static PaperResearchInputRegistration WriteInputCursor(
        PaperResearchInputStore store,
        string cursorPath,
        string kind,
        string receiptRef,
        string truthReleaseDigest,
        string topologyDigest)
    {
        string fullCursorPath = Path.GetFullPath(cursorPath);
        var cursor = new PaperResearchInputCursor(
            PaperResearchInputSchemas.InputCursor,
            kind,
            receiptRef,
            truthReleaseDigest,
            topologyDigest);
        PaperResearchInputValidation.Validate(cursor);

        if (File.Exists(fullCursorPath))
        {
            PaperResearchInputCursor current =
                PaperResearchInputJson.DeserializeStrict<PaperResearchInputCursor>(
                    File.ReadAllBytes(fullCursorPath));
            PaperResearchInputValidation.Validate(current);
            if (string.Equals(
                    current.TruthReleaseDigest,
                    truthReleaseDigest,
                    StringComparison.Ordinal))
            {
                if (!string.Equals(
                        current.ReceiptRef,
                        receiptRef,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"One truth release cannot be rebound to a different {kind} receipt.");
                }
                return new PaperResearchInputRegistration(
                    receiptRef,
                    fullCursorPath,
                    truthReleaseDigest,
                    topologyDigest,
                    true);
            }
        }

        PaperResearchInputStore.WriteAtomic(
            fullCursorPath,
            CanonicalJson.Serialize(cursor));
        return new PaperResearchInputRegistration(
            receiptRef,
            fullCursorPath,
            truthReleaseDigest,
            topologyDigest,
            false);
    }

    private static PaperResearchInputCursor ReadInputCursor(
        string path,
        string expectedKind)
    {
        PaperResearchInputCursor cursor =
            PaperResearchInputJson.DeserializeStrict<PaperResearchInputCursor>(
                File.ReadAllBytes(path));
        PaperResearchInputValidation.Validate(cursor);
        if (!string.Equals(cursor.Kind, expectedKind, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Expected a {expectedKind} research-input cursor.");
        }
        return cursor;
    }

    private static void RequireIntuitionRelease(ReadOnlySpan<byte> bytes)
    {
        using JsonDocument document = JsonDocument.Parse(bytes.ToArray());
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("schema", out JsonElement schema)
            || !string.Equals(
                schema.GetString(),
                "intuition-release.v1",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Intuition artifact is not intuition-release.v1.");
        }
    }
}

public sealed class PaperResearchInputStore
{
    private readonly string _root;

    public PaperResearchInputStore(string root) =>
        _root = Path.GetFullPath(
            root ?? throw new ArgumentNullException(nameof(root)));

    public string Put<T>(T artifact)
    {
        byte[] bytes = CanonicalJson.Serialize(artifact);
        string reference = Reference(bytes);
        string path = JsonPath(reference);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path))
        {
            if (!File.ReadAllBytes(path).SequenceEqual(bytes))
            {
                throw new InvalidDataException(
                    $"Content-address collision at {reference}.");
            }
            return reference;
        }
        WriteAtomic(path, bytes, overwrite: false);
        return reference;
    }

    public T Get<T>(string reference)
    {
        PaperResearchInputValidation.RequireDigest(reference, nameof(reference));
        string path = JsonPath(reference);
        byte[] bytes = File.ReadAllBytes(path);
        if (!string.Equals(Reference(bytes), reference, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Artifact {reference} failed digest verification.");
        }
        return PaperResearchInputJson.DeserializeStrict<T>(bytes);
    }

    public void PutBlob(string reference, ReadOnlySpan<byte> bytes)
    {
        PaperResearchInputValidation.RequireDigest(reference, nameof(reference));
        if (!string.Equals(Reference(bytes), reference, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Blob reference does not match its bytes.");
        }
        string path = BlobPath(reference);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path))
        {
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
            {
                throw new InvalidDataException(
                    $"Content-address collision at {reference}.");
            }
            return;
        }
        WriteAtomic(path, bytes, overwrite: false);
    }

    public static string Reference(ReadOnlySpan<byte> bytes) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));

    public static void WriteAtomic(
        string path,
        ReadOnlySpan<byte> bytes,
        bool overwrite = true)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllBytes(temporary, bytes.ToArray());
            File.Move(temporary, path, overwrite);
        }
        catch (IOException) when (!overwrite && File.Exists(path))
        {
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
            {
                throw;
            }
            File.Delete(temporary);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private string JsonPath(string reference) =>
        PathFor("sha256", reference, ".json");

    private string BlobPath(string reference) =>
        PathFor("blobs", reference, ".json");

    private string PathFor(string family, string reference, string extension)
    {
        string hex = reference["sha256:".Length..];
        return Path.Combine(_root, family, hex[..2], hex + extension);
    }
}

public static class PaperResearchInputJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static T DeserializeStrict<T>(ReadOnlySpan<byte> bytes)
    {
        using JsonDocument document = JsonDocument.Parse(
            bytes.ToArray(),
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
        RejectDuplicates(document.RootElement, "$");
        return JsonSerializer.Deserialize<T>(bytes, Options)
            ?? throw new JsonException("Research-input artifact is empty.");
    }

    private static void RejectDuplicates(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new JsonException(
                        $"Duplicate property '{property.Name}' at {path}.");
                }
                RejectDuplicates(property.Value, path + "." + property.Name);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray())
            {
                RejectDuplicates(item, $"{path}[{index}]");
                index++;
            }
        }
    }
}

public static class PaperResearchInputValidation
{
    public static void Validate(PaperTopologyPublication value)
    {
        RequireSchema(value.Schema, PaperResearchInputSchemas.TopologyPublication);
        RequireDigest(value.TruthReleaseDigest, nameof(value.TruthReleaseDigest));
        RequireDigest(value.TopologyDigest, nameof(value.TopologyDigest));
        RequireDigest(
            value.AlgorithmProfileDigest,
            nameof(value.AlgorithmProfileDigest));
        RequireGit(value.SourceCommit, nameof(value.SourceCommit));
        RequireGit(value.SourceTree, nameof(value.SourceTree));
        RequireGit(value.ProducerCommit, nameof(value.ProducerCommit));
        RequireSameGitFormat(value.SourceCommit, value.SourceTree);
    }

    public static void Validate(PaperIntuitionPublication value)
    {
        RequireSchema(value.Schema, PaperResearchInputSchemas.IntuitionPublication);
        RequireDigest(
            value.TopologyInputReceiptRef,
            nameof(value.TopologyInputReceiptRef));
        RequireDigest(value.IntuitionReleaseRef, nameof(value.IntuitionReleaseRef));
        RequireDigest(
            value.IntuitionReleaseDigest,
            nameof(value.IntuitionReleaseDigest));
        RequireGit(value.ProducerCommit, nameof(value.ProducerCommit));
        if (!string.Equals(
                value.IntuitionReleaseRef,
                value.IntuitionReleaseDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "intuition_release_ref must equal intuition_release_digest.");
        }
    }

    public static void Validate(IntuitionTopologyInputReceiptWire value)
    {
        RequireSchema(
            value.Schema,
            PaperResearchInputSchemas.IntuitionTopologyReceipt);
        RequireDigest(value.PublicationRef, nameof(value.PublicationRef));
        RequireDigest(value.TopologyRef, nameof(value.TopologyRef));
        RequireDigest(value.TruthReleaseDigest, nameof(value.TruthReleaseDigest));
        RequireDigest(value.TopologyDigest, nameof(value.TopologyDigest));
        RequireDigest(
            value.AlgorithmProfileDigest,
            nameof(value.AlgorithmProfileDigest));
        RequireGit(value.SourceCommit, nameof(value.SourceCommit));
        RequireGit(value.SourceTree, nameof(value.SourceTree));
        RequireGit(value.ProducerCommit, nameof(value.ProducerCommit));
        RequireSameGitFormat(value.SourceCommit, value.SourceTree);
        if (!string.Equals(
                value.TopologyRef,
                value.TopologyDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Intuition topology receipt is not bound to its topology bytes.");
        }
    }

    public static void Validate(PaperTopologyInputReceipt value)
    {
        RequireSchema(value.Schema, PaperResearchInputSchemas.TopologyReceipt);
        RequireDigest(value.PublicationRef, nameof(value.PublicationRef));
        RequireDigest(value.TopologyRef, nameof(value.TopologyRef));
        RequireDigest(value.TruthReleaseDigest, nameof(value.TruthReleaseDigest));
        RequireDigest(value.TopologyDigest, nameof(value.TopologyDigest));
        RequireDigest(
            value.AlgorithmProfileDigest,
            nameof(value.AlgorithmProfileDigest));
        RequireGit(value.SourceCommit, nameof(value.SourceCommit));
        RequireGit(value.SourceTree, nameof(value.SourceTree));
        RequireGit(value.ProducerCommit, nameof(value.ProducerCommit));
        RequireSameGitFormat(value.SourceCommit, value.SourceTree);
        if (!string.Equals(
                value.TopologyRef,
                value.TopologyDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Paper topology receipt is not bound to its topology bytes.");
        }
    }

    public static void Validate(PaperIntuitionInputReceipt value)
    {
        RequireSchema(value.Schema, PaperResearchInputSchemas.IntuitionReceipt);
        RequireDigest(value.PublicationRef, nameof(value.PublicationRef));
        RequireDigest(
            value.TopologyInputReceiptRef,
            nameof(value.TopologyInputReceiptRef));
        RequireDigest(value.IntuitionReleaseRef, nameof(value.IntuitionReleaseRef));
        RequireDigest(value.TruthReleaseDigest, nameof(value.TruthReleaseDigest));
        RequireDigest(value.TopologyDigest, nameof(value.TopologyDigest));
        RequireGit(value.SourceCommit, nameof(value.SourceCommit));
        RequireGit(value.SourceTree, nameof(value.SourceTree));
        RequireGit(
            value.IntuitionProducerCommit,
            nameof(value.IntuitionProducerCommit));
        RequireSameGitFormat(value.SourceCommit, value.SourceTree);
    }

    public static void Validate(PaperResearchInputCursor value)
    {
        RequireSchema(value.Schema, PaperResearchInputSchemas.InputCursor);
        if (value.Kind is not ("topology" or "intuition"))
        {
            throw new InvalidOperationException("Unknown Paper research input kind.");
        }
        RequireDigest(value.ReceiptRef, nameof(value.ReceiptRef));
        RequireDigest(value.TruthReleaseDigest, nameof(value.TruthReleaseDigest));
        RequireDigest(value.TopologyDigest, nameof(value.TopologyDigest));
    }

    public static void Validate(PaperResearchInput value)
    {
        RequireSchema(value.Schema, PaperResearchInputSchemas.ResearchInput);
        RequireDigest(value.TruthReleaseDigest, nameof(value.TruthReleaseDigest));
        RequireDigest(value.TopologyDigest, nameof(value.TopologyDigest));
        RequireDigest(value.TopologyReceiptRef, nameof(value.TopologyReceiptRef));
        RequireDigest(value.IntuitionReceiptRef, nameof(value.IntuitionReceiptRef));
        RequireDigest(value.IntuitionReleaseRef, nameof(value.IntuitionReleaseRef));
        RequireGit(value.SourceCommit, nameof(value.SourceCommit));
        RequireGit(value.SourceTree, nameof(value.SourceTree));
        RequireSameGitFormat(value.SourceCommit, value.SourceTree);
    }

    public static void Validate(PaperResearchJoinCursor value)
    {
        RequireSchema(value.Schema, PaperResearchInputSchemas.JoinCursor);
        RequireDigest(value.ResearchInputRef, nameof(value.ResearchInputRef));
        RequireDigest(value.TruthReleaseDigest, nameof(value.TruthReleaseDigest));
        RequireDigest(value.TopologyDigest, nameof(value.TopologyDigest));
        RequireDigest(value.TopologyReceiptRef, nameof(value.TopologyReceiptRef));
        RequireDigest(value.IntuitionReceiptRef, nameof(value.IntuitionReceiptRef));
    }

    public static void RequireDigest(string? value, string name)
    {
        if (value is null
            || value.Length != 71
            || !value.StartsWith("sha256:", StringComparison.Ordinal)
            || !IsLowerHex(value.AsSpan(7)))
        {
            throw new InvalidOperationException(
                $"{name} must be sha256:<64 lowercase hex>.");
        }
    }

    private static void RequireSchema(string actual, string expected)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Expected schema {expected}, got {actual}.");
        }
    }

    private static void RequireGit(string value, string name)
    {
        if (value.Length is not (40 or 64) || !IsLowerHex(value.AsSpan()))
        {
            throw new InvalidOperationException(
                $"{name} is not a canonical lowercase Git object id.");
        }
    }

    private static void RequireSameGitFormat(string commit, string tree)
    {
        if (commit.Length != tree.Length)
        {
            throw new InvalidOperationException(
                "source_commit and source_tree must use the same Git object format.");
        }
    }

    private static bool IsLowerHex(ReadOnlySpan<char> value)
    {
        foreach (char character in value)
        {
            if (character is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }
        return true;
    }
}
