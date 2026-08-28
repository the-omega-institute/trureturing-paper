using System.Text.Json;
using Trureturing.Paper.Core;

try
{
    if (args.Length == 0)
    {
        return Usage();
    }

    Dictionary<string, string> options = Parse(args[1..]);
    switch (args[0])
    {
        case "register-topology":
        {
            PaperTopologyPublication publication =
                PaperResearchInputJson.DeserializeStrict<PaperTopologyPublication>(
                    File.ReadAllBytes(Required(options, "publication")));
            PaperResearchInputRegistration result =
                PaperResearchInputRegistry.RegisterTopology(
                    Required(options, "root"),
                    publication,
                    File.ReadAllBytes(Required(options, "topology")),
                    Required(options, "cursor"));
            Write(new Dictionary<string, object?>
            {
                ["status"] = "registered",
                ["receipt_ref"] = result.ReceiptRef,
                ["cursor_path"] = result.CursorPath,
                ["truth_release_digest"] = result.TruthReleaseDigest,
                ["topology_digest"] = result.TopologyDigest,
                ["replayed"] = result.Replayed
            });
            return 0;
        }
        case "register-intuition":
        {
            PaperIntuitionPublication publication =
                PaperResearchInputJson.DeserializeStrict<PaperIntuitionPublication>(
                    File.ReadAllBytes(Required(options, "publication")));
            PaperResearchInputRegistration result =
                PaperResearchInputRegistry.RegisterIntuition(
                    Required(options, "root"),
                    publication,
                    File.ReadAllBytes(Required(options, "topology-receipt")),
                    File.ReadAllBytes(Required(options, "intuition-release")),
                    Required(options, "cursor"));
            Write(new Dictionary<string, object?>
            {
                ["status"] = "registered",
                ["receipt_ref"] = result.ReceiptRef,
                ["cursor_path"] = result.CursorPath,
                ["truth_release_digest"] = result.TruthReleaseDigest,
                ["topology_digest"] = result.TopologyDigest,
                ["replayed"] = result.Replayed
            });
            return 0;
        }
        case "join":
        {
            PaperResearchInputJoinResult result =
                PaperResearchInputRegistry.Join(
                    Required(options, "root"),
                    Required(options, "topology-cursor"),
                    Required(options, "intuition-cursor"),
                    Required(options, "cursor"));
            Write(new Dictionary<string, object?>
            {
                ["status"] = result.Status,
                ["research_input_ref"] = result.ResearchInputRef,
                ["cursor_path"] = result.CursorPath,
                ["truth_release_digest"] = result.TruthReleaseDigest,
                ["topology_digest"] = result.TopologyDigest,
                ["replayed"] = result.Replayed
            });
            return 0;
        }
        default:
            return Usage();
    }
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}

static Dictionary<string, string> Parse(string[] values)
{
    if (values.Length % 2 != 0)
    {
        throw new ArgumentException("CLI options must be --name value pairs.");
    }
    var result = new Dictionary<string, string>(StringComparer.Ordinal);
    for (int index = 0; index < values.Length; index += 2)
    {
        string name = values[index];
        if (!name.StartsWith("--", StringComparison.Ordinal)
            || !result.TryAdd(name[2..], values[index + 1]))
        {
            throw new ArgumentException($"Invalid or duplicate option '{name}'.");
        }
    }
    return result;
}

static string Required(IReadOnlyDictionary<string, string> values, string name) =>
    values.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new ArgumentException($"Missing --{name}.");

static void Write(IReadOnlyDictionary<string, object?> value)
{
    Console.WriteLine(JsonSerializer.Serialize(
        value,
        new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        }));
}

static int Usage()
{
    Console.Error.WriteLine(
        "usage:\n" +
        "  register-topology --root <dir> --publication <json> " +
        "--topology <json> --cursor <json>\n" +
        "  register-intuition --root <dir> --publication <json> " +
        "--topology-receipt <json> --intuition-release <json> --cursor <json>\n" +
        "  join --root <dir> --topology-cursor <json> " +
        "--intuition-cursor <json> --cursor <json>");
    return 2;
}
