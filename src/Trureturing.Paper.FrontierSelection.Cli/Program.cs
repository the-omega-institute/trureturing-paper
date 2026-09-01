using System.Text;
using Trureturing.Paper.Core;

namespace Trureturing.Paper.FrontierSelection.Cli;

internal static class Program
{
    private const string Usage = """
        Usage:
          admit-frontier-node-selection --repository-root <path> --frontier-task-ref <sha256:...> --node-id <sha256:...>
        """;

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 7
                || !string.Equals(
                    args[0],
                    "admit-frontier-node-selection",
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(Usage);
            }
            Dictionary<string, string> values = ParseValues(
                args,
                "--repository-root",
                "--frontier-task-ref",
                "--node-id");
            PaperFrontierNodeSelectionAdmitted admitted =
                PaperFrontierNodeSelectionService.Admit(
                    values["--repository-root"],
                    values["--frontier-task-ref"],
                    values["--node-id"]);
            Console.WriteLine(
                Encoding.UTF8.GetString(CanonicalJson.Serialize(admitted)));
            return 0;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or InvalidDataException
            or InvalidOperationException)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
    }

    private static Dictionary<string, string> ParseValues(
        string[] args,
        params string[] expectedOptions)
    {
        var expected = expectedOptions.ToHashSet(StringComparer.Ordinal);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 1; index < args.Length; index += 2)
        {
            if (!expected.Contains(args[index])
                || !values.TryAdd(args[index], args[index + 1]))
            {
                throw new ArgumentException(Usage);
            }
        }
        if (values.Count != expected.Count)
        {
            throw new ArgumentException(Usage);
        }
        return values;
    }
}
