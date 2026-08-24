using System.Reflection;
using System.Text.Json;
using Trureturing.Paper.Core;
using Xunit;

namespace Trureturing.Paper.Tests;

public sealed class PaperPortContractTests
{
    private static readonly NullabilityInfoContext Nullability = new();

    [Fact]
    public void PublishedSchemasMatchCSharpPortRecords()
    {
        AssertSchemaMatchesRecord(
            "paper-truth-release-port.v1.schema.json",
            typeof(PaperTruthReleasePort));
        AssertSchemaMatchesRecord(
            "paper-intuition-port.v1.schema.json",
            typeof(PaperIntuitionPort));
    }

    private static void AssertSchemaMatchesRecord(string fileName, Type recordType)
    {
        string path = Path.Combine(FindRoot(), "contracts", fileName);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));

        AssertObjectShape(document.RootElement, recordType);
    }

    private static void AssertObjectShape(JsonElement schema, Type recordType)
    {
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());

        PropertyInfo[] recordProperties = recordType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToArray();
        string[] expectedNames = recordProperties
            .Select(property => JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] schemaNames = schema.GetProperty("properties")
            .EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] requiredNames = schema.GetProperty("required")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedNames, schemaNames);
        Assert.Equal(expectedNames, requiredNames);

        JsonElement schemaProperties = schema.GetProperty("properties");
        foreach (PropertyInfo property in recordProperties)
        {
            string jsonName = JsonNamingPolicy.SnakeCaseLower.ConvertName(property.Name);
            JsonElement propertySchema = schemaProperties.GetProperty(jsonName);
            AssertPropertyShape(property, propertySchema);
        }
    }

    private static void AssertPropertyShape(
        PropertyInfo property,
        JsonElement propertySchema)
    {
        Type propertyType = property.PropertyType;
        Type? nullableType = Nullable.GetUnderlyingType(propertyType);
        string[] expectedTypes;

        if (propertyType.IsGenericType &&
            propertyType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
        {
            expectedTypes = new[] { "array" };
            Type itemType = propertyType.GetGenericArguments()[0];
            JsonElement itemSchema = propertySchema.GetProperty("items");
            if (itemType == typeof(string))
            {
                Assert.Contains("string", SchemaTypes(itemSchema));
            }
            else
            {
                AssertObjectShape(itemSchema, itemType);
            }
        }
        else if (nullableType == typeof(double))
        {
            expectedTypes = new[] { "null", "number" };
        }
        else if (propertyType == typeof(string))
        {
            expectedTypes = Nullability.Create(property).ReadState == NullabilityState.Nullable
                ? new[] { "null", "string" }
                : new[] { "string" };
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported contract property type {propertyType}.");
        }

        Assert.Equal(
            expectedTypes.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            SchemaTypes(propertySchema).OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }

    private static IReadOnlySet<string> SchemaTypes(JsonElement schema)
    {
        var types = new HashSet<string>(StringComparer.Ordinal);

        if (schema.TryGetProperty("type", out JsonElement type))
        {
            if (type.ValueKind == JsonValueKind.String)
            {
                types.Add(type.GetString()!);
            }
            else
            {
                foreach (JsonElement item in type.EnumerateArray())
                {
                    types.Add(item.GetString()!);
                }
            }
        }

        if (schema.TryGetProperty("oneOf", out JsonElement oneOf))
        {
            foreach (JsonElement alternative in oneOf.EnumerateArray())
            {
                types.UnionWith(SchemaTypes(alternative));
            }
        }

        if (schema.TryGetProperty("const", out JsonElement constant))
        {
            types.Add(JsonType(constant));
        }

        if (schema.TryGetProperty("enum", out JsonElement values))
        {
            foreach (JsonElement value in values.EnumerateArray())
            {
                types.Add(JsonType(value));
            }
        }

        return types;
    }

    private static string JsonType(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => "string",
        JsonValueKind.Number => "number",
        JsonValueKind.Null => "null",
        _ => throw new InvalidOperationException(
            $"Unsupported schema literal type {value.ValueKind}.")
    };

    private static string FindRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Trureturing.Paper.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
