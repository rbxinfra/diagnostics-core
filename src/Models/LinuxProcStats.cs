namespace Roblox.Infrastructure.Diagnostics;

using System;

/// <summary>
/// Linux processor stats.
/// </summary>
public class LinuxProcStats
{
    private static readonly char[] _Separators = new[] { ' ' };

    /// <summary>
    /// User stat.
    /// </summary>
    public long User { get; set; }

    /// <summary>
    /// Nice stat.
    /// </summary>
    public long Nice { get; set; }

    /// <summary>
    /// System stat.
    /// </summary>
    public long System { get; set; }

    /// <summary>
    /// Idle stat.
    /// </summary>
    public long Idle { get; set; }

    /// <summary>
    /// I/O Wait stat.
    /// </summary>
    public long IoWait { get; set; }

    /// <summary>
    /// IRQ stat.
    /// </summary>
    public long Irq { get; set; }

    /// <summary>
    /// Soft IRQ stat.
    /// </summary>
    public long SoftIrq { get; set; }

    /// <summary>
    /// Steal stat.
    /// </summary>
    public long Steal { get; set; }

    /// <summary>
    /// Guest stat.
    /// </summary>
    public long Guest { get; set; }

    /// <summary>
    /// Guest nice stat.
    /// </summary>
    public long GuestNice { get; set; }

    /// <summary>
    /// Total stats.
    /// </summary>
    public long TotalJiffies => User + Nice + System + Idle + IoWait + Irq + SoftIrq + Steal + Guest + GuestNice;

    /// <summary>
    /// Work stats.
    /// </summary>
    public long WorkJiffies => TotalJiffies - Idle - IoWait;

    /// <summary>
    /// Construct a new instance of <see cref="LinuxProcStats"/>
    /// </summary>
    /// <param name="line">The /proc/stat line.</param>
    /// <exception cref="ArgumentNullException"><paramref name="line"/> cannot be null.</exception>
    /// <exception cref="ArgumentException">Unable to parse cpu stats</exception>
    public LinuxProcStats(string line)
    {
        if (line == null) throw new ArgumentNullException(nameof(line));

        var columns = line.Split(_Separators, StringSplitOptions.RemoveEmptyEntries);
        if (columns[0] != "cpu" && columns.Length < 11)
            throw new ArgumentException($"Unable to parse cpu stats: {line}", nameof(line));

        User = long.Parse(columns[1]);
        Nice = long.Parse(columns[2]);
        System = long.Parse(columns[3]);
        Idle = long.Parse(columns[4]);
        IoWait = long.Parse(columns[5]);
        Irq = long.Parse(columns[6]);
        SoftIrq = long.Parse(columns[7]);
        Steal = long.Parse(columns[8]);
        Guest = long.Parse(columns[9]);
        GuestNice = long.Parse(columns[10]);
        User -= Guest;
        Nice -= GuestNice;
    }
}
