using System;

namespace AutoMapSharp.Exceptions;

/// <summary>
/// Base exception for all AutoMapSharp runtime and configuration errors.
/// </summary>
public class AutoMapSharpException : Exception
{
    /// <summary>Creates an exception with the given message.</summary>
    /// <param name="message">A description of the error.</param>
    public AutoMapSharpException(string message) : base(message) { }
    /// <summary>Creates an exception with the given message and the exception that caused it.</summary>
    /// <param name="message">A description of the error.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public AutoMapSharpException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when an error occurs during runtime object mapping execution.
/// </summary>
public class AutoMapSharpMappingException : AutoMapSharpException
{
    /// <summary>The source type of the map that failed, when known.</summary>
    public Type? SourceType { get; }
    /// <summary>The destination type of the map that failed, when known.</summary>
    public Type? DestinationType { get; }

    /// <summary>Creates a mapping exception.</summary>
    /// <param name="message">A description of the error.</param>
    /// <param name="sourceType">The source type of the map that failed, when known.</param>
    /// <param name="destinationType">The destination type of the map that failed, when known.</param>
    /// <param name="innerException">The exception that caused this one, if any.</param>
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
    /// <summary>Creates a configuration exception with the given message.</summary>
    /// <param name="message">A description of the configuration error.</param>
    public AutoMapSharpConfigurationException(string message) : base(message) { }
    /// <summary>Creates a configuration exception with the given message and the exception that caused it.</summary>
    /// <param name="message">A description of the configuration error.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public AutoMapSharpConfigurationException(string message, Exception innerException) : base(message, innerException) { }
}
