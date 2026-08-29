using System.Text.Json;
using Trureturing.Paper.Core;

namespace Trureturing.Paper.ResearchSelection.Cli;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            return args.FirstOrDefault() switch
            {
                "select" => Select(args),
                _ => throw new ArgumentException(Usage)
            };
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or ArgumentException
            or InvalidDataException)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
    }

    private static int Select(string[] args)
    {
        Dictionary<string, string> values = ParseValues(
            args,
            "select",
            "--content",
            "--selection-out",
            "--request-out");

        PaperResearchSelectionContent content =
            PaperResearchSelectionJson.ReadContent(
                File.ReadAllBytes(values["--content"]));
        PaperResearchSelection selection =
            PaperResearchSelectionService.Create(content);
        FormalizationRequest request =
            PaperResearchSelectionService.BuildFormalizationRequest(selection);

        byte[] selectionBytes = PaperResearchSelectionJson.Write(selection);
        byte[] requestBytes = PaperResearchSelectionJson.Write(request);
        string selectionPath = WriteFile(
            values["--selection-out"],
            selectionBytes);
        string requestPath = WriteFile(
            values["--request-out"],
            requestBytes);

        byte[] result = CanonicalJson.Serialize(new SelectionCliResult(
            "paper-formalization-handoff.v1",
            selection.SelectionId,
            request.RequestId,
            selectionPath,
            requestPath));
        Console.WriteLine(System.Text.Encoding.UTF8.GetString(result));
        return 0;
    }

    private static Dictionary<string, string> ParseValues(
        string[] args,
        string verb,
        params string[] expectedOptions)
    {
        if (args.Length != 1 + (expectedOptions.Length * 2)
            || !string.Equals(args[0], verb, StringComparison.Ordinal))
        {
            throw new ArgumentException(Usage);
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 1; index < args.Length; index += 2)
        {
            if (!values.TryAdd(args[index], args[index + 1]))
            {
                throw new ArgumentException(
                    $"Duplicate CLI option '{args[index]}'.");
            }
        }
        if (values.Count != expectedOptions.Length
            || expectedOptions.Any(option => !values.ContainsKey(option)))
        {
            throw new ArgumentException(
                "CLI options are incomplete or unknown.\n" + Usage);
        }
        return values;
    }

    private static string WriteFile(string path, byte[] bytes)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string temporary = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
        return fullPath;
    }

    private sealed record SelectionCliResult(
        string Schema,
        string SelectionRef,
        string FormalizationRequestRef,
        string SelectionPath,
        string FormalizationRequestPath);

    private const string Usage = """
Usage:
  trureturing-paper-research-selection select --content <paper-selection-content.json> --selection-out <paper-research-selection.v1.json> --request-out <formalization-request.v1.json>
""";
}
