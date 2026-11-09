namespace AutoMapSharp.Configuration;

/// <summary>
/// Which members configuration validation checks for a map.
/// </summary>
public enum MemberList
{
    /// <summary>
    /// Every destination member must be mapped. This is the default.
    /// </summary>
    Destination,

    /// <summary>
    /// Every source member must be used by some destination member.
    /// </summary>
    Source,

    /// <summary>
    /// No member check.
    /// </summary>
    None
}
