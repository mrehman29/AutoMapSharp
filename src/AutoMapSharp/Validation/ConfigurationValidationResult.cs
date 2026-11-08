using System;
using System.Collections.Generic;
using System.Text;

namespace AutoMapSharp.Validation;

/// <summary>
/// Details a configuration error for a specific source and destination type mapping.
/// </summary>
public sealed class ConfigurationError
{
    /// <summary>
    /// Gets the source type being mapped from.
    /// </summary>
    public Type SourceType { get; }

    /// <summary>
    /// Gets the destination type being mapped to.
    /// </summary>
    public Type DestinationType { get; }

    /// <summary>
    /// Gets the list of destination property names that are unmapped.
    /// </summary>
    public IReadOnlyList<string> UnmappedProperties { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ConfigurationError"/>.
    /// </summary>
    public ConfigurationError(Type sourceType, Type destinationType, IEnumerable<string> unmappedProperties)
        : this(sourceType, destinationType, unmappedProperties, null, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ConfigurationError"/> with every kind of problem.
    /// </summary>
    public ConfigurationError(
        Type sourceType,
        Type destinationType,
        IEnumerable<string>? unmappedProperties,
        IEnumerable<string>? unmappedSourceMembers,
        IEnumerable<string>? problems,
        string? context)
    {
        SourceType = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
        DestinationType = destinationType ?? throw new ArgumentNullException(nameof(destinationType));
        UnmappedProperties = new List<string>(unmappedProperties ?? Array.Empty<string>()).AsReadOnly();
        UnmappedSourceMembers = new List<string>(unmappedSourceMembers ?? Array.Empty<string>()).AsReadOnly();
        Problems = new List<string>(problems ?? Array.Empty<string>()).AsReadOnly();
        Context = context;
    }

    /// <summary>
    /// Source members that no destination member uses. Only checked for maps validated with <c>MemberList.Source</c>.
    /// </summary>
    public IReadOnlyList<string> UnmappedSourceMembers { get; }

    /// <summary>
    /// Other problems, such as members whose types cannot be converted or constructor parameters that cannot be resolved.
    /// </summary>
    public IReadOnlyList<string> Problems { get; }

    /// <summary>
    /// Where an automatic nested map is used, when this error is about one.
    /// </summary>
    public string? Context { get; }

    /// <inheritdoc/>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Mapping: {SourceType.FullName} -> {DestinationType.FullName}");
        if (Context != null)
        {
            sb.AppendLine($"  {Context}");
        }

        if (UnmappedProperties.Count > 0)
        {
            sb.AppendLine("  Unmapped destination properties:");
            foreach (var prop in UnmappedProperties)
            {
                sb.AppendLine($"    - {prop}");
            }

            sb.AppendLine("  Fix: Use .ForProperty(d => d.<Property>, opt => opt.Skip()) or configure a mapping rule.");
        }

        if (UnmappedSourceMembers.Count > 0)
        {
            sb.AppendLine("  Unused source members:");
            foreach (var member in UnmappedSourceMembers)
            {
                sb.AppendLine($"    - {member}");
            }

            sb.AppendLine("  Fix: Map them, or use .ForSourceMember(s => s.<Member>, opt => opt.DoNotValidate()).");
        }

        if (Problems.Count > 0)
        {
            sb.AppendLine("  Problems:");
            foreach (var problem in Problems)
            {
                sb.AppendLine($"    - {problem}");
            }
        }

        return sb.ToString();
    }
}

/// <summary>
/// Represents the result of an engine configuration validation check.
/// </summary>
public sealed class ConfigurationValidationResult
{
    /// <summary>
    /// Gets whether the entire mapping configuration is valid with zero unmapped properties.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Gets the list of configuration errors detected during validation.
    /// </summary>
    public IReadOnlyList<ConfigurationError> Errors { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ConfigurationValidationResult"/>.
    /// </summary>
    public ConfigurationValidationResult(IEnumerable<ConfigurationError> errors)
    {
        Errors = new List<ConfigurationError>(errors ?? Array.Empty<ConfigurationError>()).AsReadOnly();
    }

    /// <summary>
    /// Generates a comprehensive, human-readable error report of all validation errors.
    /// </summary>
    public string GenerateReport()
    {
        if (IsValid)
        {
            return "Configuration is valid. All registered mappings have complete destination property coverage.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"AutoMapSharp Configuration Validation Failed with {Errors.Count} error(s):");
        sb.AppendLine(new string('=', 70));
        for (int i = 0; i < Errors.Count; i++)
        {
            sb.AppendLine($"[Error #{i + 1}]");
            sb.Append(Errors[i]);
            sb.AppendLine();
        }
        sb.AppendLine(new string('=', 70));
        return sb.ToString();
    }
}
