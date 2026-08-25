using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trureturing.Paper.Core;

public static class CandidateArtifactSchemas
{
    public const string CandidatePaper = "candidate-paper.v1";
    public const string LiteratureResearch = "literature-research.v1";
    public const string CandidateJournal = "candidate-journal.v1";
}

public sealed record CandidateGrounding(
    [property: JsonRequired] IReadOnlyList<string> CertifiedNodeIds,
    [property: JsonRequired] IReadOnlyList<string> ProvedBridgeRefs);

public sealed record CandidateKeyClaim(
    [property: JsonRequired] string Claim,
    [property: JsonRequired] string Kind,
    [property: JsonRequired] string SourceRef);

public sealed record CandidatePaperArtifact(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string Title,
    [property: JsonRequired] string Thesis,
    [property: JsonRequired] CandidateGrounding GroundedOn,
    [property: JsonRequired] IReadOnlyList<string> Outline,
    [property: JsonRequired] IReadOnlyList<CandidateKeyClaim> KeyClaims,
    [property: JsonRequired] string Abstract);

public sealed record RelatedWork(
    [property: JsonRequired] string Title,
    [property: JsonRequired] IReadOnlyList<string> Authors,
    [property: JsonRequired] string Venue,
    [property: JsonRequired] int Year,
    [property: JsonRequired] string Url,
    [property: JsonRequired] string Relation,
    [property: JsonRequired] string VerificationStatus);

public sealed record LiteratureResearchArtifact(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string Claim,
    [property: JsonRequired] IReadOnlyList<string> QueriesRun,
    [property: JsonRequired] IReadOnlyList<RelatedWork> RelatedWork,
    [property: JsonRequired] string NoveltyAssessment,
    [property: JsonRequired] string Rationale);

public sealed record CandidateVenue(
    [property: JsonRequired] string Name,
    [property: JsonRequired] string ScopeFitRationale,
    [property: JsonRequired] IReadOnlyList<string> TypicalTopics,
    [property: JsonRequired] string FitScoreOpenOrMeasured);

public sealed record CandidateJournalArtifact(
    [property: JsonRequired] string Schema,
    [property: JsonRequired] string CandidatePaperRef,
    [property: JsonRequired] IReadOnlyList<CandidateVenue> Venues);

public sealed record CandidateProposalArtifacts(
    CandidatePaperArtifact CandidatePaper,
    LiteratureResearchArtifact LiteratureResearch,
    IReadOnlyList<CandidateVenue> CandidateVenues);

public static class CanonicalJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static byte[] Serialize<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        using JsonDocument document = JsonSerializer.SerializeToDocument(
            value,
            SerializerOptions);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteElement(writer, document.RootElement);
        }

        return stream.ToArray();
    }

    public static string Sha256Reference(ReadOnlySpan<byte> bytes) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void WriteElement(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in element.EnumerateObject()
                    .OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteElement(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in element.EnumerateArray())
                {
                    WriteElement(writer, item);
                }
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText());
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new JsonException(
                    $"Unsupported JSON value kind {element.ValueKind}.");
        }
    }
}
