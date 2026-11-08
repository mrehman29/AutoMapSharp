using System;

namespace AutoMapSharp.Exceptions;

/// <summary>
/// Base exception for all AutoMapSharp runtime and configuration errors.
/// </summary>
public class AutoMapSharpException : Exception
{
    public AutoMapSharpException(string message) : base(message) { }
    public AutoMapSharpException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when an error occurs during runtime object mapping execution.
/// </summary>
public class AutoMapSharpMappingException : AutoMapSharpException
{
    public Type? SourceType { get; }
    public Type? DestinationType { get; }

    public AutoMapSharpMappingException(string message, Type? sourceType = null, Type? destinationType = null, Exception? innerException = null)
        : base(message, innerException!)
    {
        SourceType = sourceType;
        DestinationType = destinationType;
    }
}

/// <summary>
/// Thrown when mapping configurations or modules contain invalid or duplicate definitions.
/// </summary>
public class AutoMapSharpConfigurationException : AutoMapSharpException
{
    public AutoMapSharpConfigurationException(string message) : base(message) { }
    public AutoMapSharpConfigurationException(string message, Exception innerException) : base(message, innerException) { }
}
