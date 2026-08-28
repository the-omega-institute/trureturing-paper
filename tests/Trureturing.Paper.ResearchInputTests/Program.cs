using System.Security.Cryptography;
using System.Text;
using Trureturing.Paper.Core;

var tests = new (string Name, Action Run)[]
{
    ("joins topology and intuition on one exact release", JoinsExactRelease),
    ("waits when topology and intuition releases differ", WaitsOnMixedRelease),
    ("rejects same-release intuition rebinding", RejectsSameReleaseRebinding)
};

int failed = 0;
foreach ((string name, Action run) in tests)
{
    try
    {
        run();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
    }
}
Console.WriteLine($"{tests.Length - failed} passed, {failed} failed");
return failed == 0 ? 0 : 1;

static byte[] Topology() => File.ReadAllBytes(Path.Combine(
    AppContext.BaseDirectory,
    "fixtures",
    "certified-topology.v1.json"));

static string Digest(ReadOnlySpan<byte> bytes) =>
    "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));

static string ReleaseDigest() =>
    "sha256:1fde9beb7d1999d06c042d24b57e18caea67612f209d725b5eb931addccb0e46";

static PaperTopologyPublication TopologyPublication(byte[] topology) => new(
    PaperResearchInputSchemas.TopologyPublication,
    ReleaseDigest(),
    Digest(topology),
    new string('1', 40),
    new string('2', 40),
    "sha256:" + new string('a', 64),
    new string('c', 40));

static byte[] TopologyReceiptBytes(
    byte[] topology,
    string? releaseDigest = null)
{
    var receipt = new IntuitionTopologyInputReceiptWire(
        PaperResearchInputSchemas.IntuitionTopologyReceipt,
        "sha256:" + new string('3', 64),
        Digest(topology),
        releaseDigest ?? ReleaseDigest(),
        Digest(topology),
        new string('1', 40),
        new string('2', 40),
        "sha256:" + new string('a', 64),
        new string('c', 40));
    return CanonicalJson.Serialize(receipt);
}

static byte[] IntuitionRelease(string marker = "one") => Encoding.UTF8.GetBytes(
    $"{{\"schema\":\"intuition-release.v1\",\"marker\":\"{marker}\"}}\n");

static PaperIntuitionPublication IntuitionPublication(
    byte[] topologyReceipt,
    byte[] intuitionRelease) => new(
        PaperResearchInputSchemas.IntuitionPublication,
        Digest(topologyReceipt),
        Digest(intuitionRelease),
        Digest(intuitionRelease),
        new string('d', 40));

static void JoinsExactRelease()
{
    using var temp = new TempDirectory();
    byte[] topology = Topology();
    byte[] topologyReceipt = TopologyReceiptBytes(topology);
    byte[] intuitionRelease = IntuitionRelease();
    string topologyCursor = Path.Combine(temp.Path, "topology-cursor.json");
    string intuitionCursor = Path.Combine(temp.Path, "intuition-cursor.json");
    string joinCursor = Path.Combine(temp.Path, "join-cursor.json");

    PaperResearchInputRegistration topologyRegistration =
        PaperResearchInputRegistry.RegisterTopology(
            temp.Path,
            TopologyPublication(topology),
            topology,
            topologyCursor);
    PaperResearchInputRegistration intuitionRegistration =
        PaperResearchInputRegistry.RegisterIntuition(
            temp.Path,
            IntuitionPublication(topologyReceipt, intuitionRelease),
            topologyReceipt,
            intuitionRelease,
            intuitionCursor);
    PaperResearchInputJoinResult joined = PaperResearchInputRegistry.Join(
        temp.Path,
        topologyCursor,
        intuitionCursor,
        joinCursor);

    Assert.Equal("ready", joined.Status);
    Assert.True(joined.ResearchInputRef is not null);
    Assert.Equal(ReleaseDigest(), joined.TruthReleaseDigest);
    Assert.Equal(topologyRegistration.TopologyDigest, joined.TopologyDigest);

    var store = new PaperResearchInputStore(temp.Path);
    PaperResearchInput input = store.Get<PaperResearchInput>(joined.ResearchInputRef!);
    Assert.Equal(topologyRegistration.ReceiptRef, input.TopologyReceiptRef);
    Assert.Equal(intuitionRegistration.ReceiptRef, input.IntuitionReceiptRef);

    PaperResearchInputJoinResult replay = PaperResearchInputRegistry.Join(
        temp.Path,
        topologyCursor,
        intuitionCursor,
        joinCursor);
    Assert.True(replay.Replayed);
    Assert.Equal(joined.ResearchInputRef, replay.ResearchInputRef);
}

static void WaitsOnMixedRelease()
{
    using var temp = new TempDirectory();
    byte[] topology = Topology();
    byte[] topologyReceipt = TopologyReceiptBytes(
        topology,
        "sha256:" + new string('f', 64));
    byte[] intuitionRelease = IntuitionRelease();
    string topologyCursor = Path.Combine(temp.Path, "topology-cursor.json");
    string intuitionCursor = Path.Combine(temp.Path, "intuition-cursor.json");

    _ = PaperResearchInputRegistry.RegisterTopology(
        temp.Path,
        TopologyPublication(topology),
        topology,
        topologyCursor);
    _ = PaperResearchInputRegistry.RegisterIntuition(
        temp.Path,
        IntuitionPublication(topologyReceipt, intuitionRelease),
        topologyReceipt,
        intuitionRelease,
        intuitionCursor);

    PaperResearchInputJoinResult result = PaperResearchInputRegistry.Join(
        temp.Path,
        topologyCursor,
        intuitionCursor,
        Path.Combine(temp.Path, "join.json"));
    Assert.Equal("waiting", result.Status);
    Assert.True(result.ResearchInputRef is null);
}

static void RejectsSameReleaseRebinding()
{
    using var temp = new TempDirectory();
    byte[] topology = Topology();
    byte[] topologyReceipt = TopologyReceiptBytes(topology);
    byte[] firstRelease = IntuitionRelease("one");
    byte[] secondRelease = IntuitionRelease("two");
    string cursor = Path.Combine(temp.Path, "intuition-cursor.json");

    _ = PaperResearchInputRegistry.RegisterIntuition(
        temp.Path,
        IntuitionPublication(topologyReceipt, firstRelease),
        topologyReceipt,
        firstRelease,
        cursor);
    Assert.Throws(() => PaperResearchInputRegistry.RegisterIntuition(
        temp.Path,
        IntuitionPublication(topologyReceipt, secondRelease),
        topologyReceipt,
        secondRelease,
        cursor));
}

sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "trureturing-paper-input-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

static class Assert
{
    public static void True(bool value)
    {
        if (!value) throw new InvalidOperationException("Expected true.");
    }

    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"Expected '{expected}', got '{actual}'.");
        }
    }

    public static void Throws(Action action)
    {
        try
        {
            action();
        }
        catch
        {
            return;
        }
        throw new InvalidOperationException("Expected an exception.");
    }
}
