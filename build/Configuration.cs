using System.ComponentModel;
using Nuke.Common.Tooling;

/// <summary>Defines the supported build configurations for NUKE parameters.</summary>
[TypeConverter(typeof(TypeConverter<Configuration>))]
// ReSharper disable once CheckNamespace
public class Configuration : Enumeration
{
    /// <summary>Builds with debugging enabled.</summary>
    public static readonly Configuration Debug = new() { Value = nameof(Debug) };

    /// <summary>Builds with release optimizations enabled.</summary>
    public static readonly Configuration Release = new() { Value = nameof(Release) };

    /// <summary>Converts the configuration to its command-line value.</summary>
    public static implicit operator string(Configuration configuration) => configuration.Value;
}
