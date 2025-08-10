namespace Roblox.Infrastructure.Diagnostics;

using System;

/// <summary>
/// Settings for the <see cref="BandwidthUseMonitor"/>
/// </summary>
public interface IBandwithUseMonitorSettings
{
    /// <summary>
    /// Gets the interface prefixes to ignore.
    /// </summary>
    string InterfacePrefixesToIgnoreForNetworkBandwidthCsv { get; set; }

    /// <summary>
    /// Gets or sets the period to sample network band width stats.
    /// </summary>
    TimeSpan NetworkBandwidthSamplePeriod { get; set; }
}