using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace K42.Schema;

/// <summary>
/// Parses and validates K42 YAML specifications.
/// 
/// Rules:
/// - YAML must be valid
/// - All required fields must be present
/// - Invalid YAML = refused, no execution
/// - No best-effort parsing
/// </summary>
public sealed class SpecParser
{
    private readonly IDeserializer _deserializer;

    public SpecParser()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Extracts YAML content from a K42 script file.
    /// The YAML is embedded between markers or is the entire file.
    /// </summary>
    public string ExtractYaml(string fileContent)
    {
        // Look for shebang line and skip it
        var lines = fileContent.Split('\n');
        var yamlLines = new List<string>();
        var inYaml = false;
        var foundMarker = false;

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();

            // Skip shebang
            if (trimmed.StartsWith("#!") && yamlLines.Count == 0)
                continue;

            // Check for YAML start marker
            if (trimmed == "# ---K42-START---")
            {
                inYaml = true;
                foundMarker = true;
                continue;
            }

            // Check for YAML end marker
            if (trimmed == "# ---K42-END---")
            {
                inYaml = false;
                continue;
            }

            // If using markers, only include content between them
            if (foundMarker)
            {
                if (inYaml)
                {
                    // Strip leading "# " from YAML lines if present
                    if (trimmed.StartsWith("# "))
                        yamlLines.Add(line.TrimStart().Substring(2));
                    else if (trimmed.StartsWith("#"))
                        yamlLines.Add(line.TrimStart().Substring(1));
                    else
                        yamlLines.Add(line);
                }
            }
            else
            {
                // No markers found - treat entire file (minus shebang) as YAML
                yamlLines.Add(line);
            }
        }

        return string.Join('\n', yamlLines).Trim();
    }

    /// <summary>
    /// Parse and validate a K42 specification from YAML.
    /// Throws on any validation failure.
    /// </summary>
    public K42Spec Parse(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
            throw new SpecValidationException("YAML content is empty");

        K42Spec spec;
        try
        {
            spec = _deserializer.Deserialize<K42Spec>(yaml)
                ?? throw new SpecValidationException("YAML deserialized to null");
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new SpecValidationException($"Invalid YAML syntax: {ex.Message}");
        }

        Validate(spec);
        return spec;
    }

    /// <summary>
    /// Parse a K42 script file (extracts YAML and parses).
    /// </summary>
    public K42Spec ParseFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new SpecValidationException($"File not found: {filePath}");

        var content = File.ReadAllText(filePath);
        var yaml = ExtractYaml(content);
        return Parse(yaml);
    }

    private void Validate(K42Spec spec)
    {
        var errors = new List<string>();

        // Required: name
        if (string.IsNullOrWhiteSpace(spec.Name))
            errors.Add("'name' is required and cannot be empty");
        else if (!IsValidContainerName(spec.Name))
            errors.Add("'name' must contain only lowercase letters, numbers, hyphens, and underscores");

        // Required: image
        if (string.IsNullOrWhiteSpace(spec.Image))
            errors.Add("'image' is required and cannot be empty");

        // Validate ports
        if (spec.ContainerPort < 0 || spec.ContainerPort > 65535)
            errors.Add("'container-port' must be between 0 and 65535");

        if (spec.HostPort < 0 || spec.HostPort > 65535)
            errors.Add("'host-port' must be between 0 and 65535");

        // Validate storage size
        if (!string.IsNullOrEmpty(spec.StorageSize) && !IsValidStorageSize(spec.StorageSize))
            errors.Add("'storage-size' must be in format: 500MB, 1GB, 10GB, etc.");

        if (errors.Count > 0)
        {
            throw new SpecValidationException(
                "YAML validation failed:\n  - " + string.Join("\n  - ", errors));
        }
    }

    private static bool IsValidContainerName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        foreach (var c in name)
        {
            // Only allow lowercase letters, digits, hyphens, and underscores
            if (!char.IsAsciiLetterLower(c) && !char.IsAsciiDigit(c) && c != '-' && c != '_')
                return false;
        }

        // Must start with a letter or digit (not hyphen or underscore)
        return char.IsAsciiLetterLower(name[0]) || char.IsAsciiDigit(name[0]);
    }

    private static bool IsValidStorageSize(string size)
    {
        size = size.Trim().ToUpperInvariant();

        if (size.EndsWith("GB"))
        {
            var num = size[..^2];
            return int.TryParse(num, out var val) && val > 0;
        }

        if (size.EndsWith("MB"))
        {
            var num = size[..^2];
            return int.TryParse(num, out var val) && val > 0;
        }

        return false;
    }

    /// <summary>
    /// Parse storage size string to bytes.
    /// </summary>
    public static long ParseStorageSize(string size)
    {
        size = size.Trim().ToUpperInvariant();

        if (size.EndsWith("GB"))
        {
            var num = size[..^2];
            if (long.TryParse(num, out var val))
                return val * 1024 * 1024 * 1024;
        }

        if (size.EndsWith("MB"))
        {
            var num = size[..^2];
            if (long.TryParse(num, out var val))
                return val * 1024 * 1024;
        }

        // Default: 1GB
        return 1024L * 1024 * 1024;
    }
}

/// <summary>
/// Thrown when YAML validation fails.
/// </summary>
public class SpecValidationException : Exception
{
    public SpecValidationException(string message) : base(message) { }
}
