namespace Roblox.Infrastructure.Diagnostics;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.NetworkManagement.IpHelper;

using EventLog;

/// <summary>
/// Monitor for NIC bandwidth on Linux and Windows.
/// </summary>
public sealed class BandwidthUseMonitor : IDisposable
{
    private const int _TimerIntervalInMilliseconds = 1000;
    private const double _MilisecondsInSecond = 1000;
    private const double _BytesInKilobyte = 1024;
    private const char _CsvSeparator = ',';
    private const char _SpaceSeparator = ' ';

    private readonly ILogger _Logger;
    private readonly IBandwithUseMonitorSettings _Settings;
    private readonly string[] _EmpStringArray = [];

    private double _UploadSpeedKbps;
    private double _DownloadSpeedKbps;
    private Timer _Timer;

    /// <summary>
    /// Construct a new instance of <see cref="BandwidthUseMonitor"/>
    /// </summary>
    /// <param name="logger">The <see cref="ILogger"/></param>
    /// <param name="settings">The <see cref="IBandwithUseMonitorSettings"/></param>
    public BandwidthUseMonitor(ILogger logger, IBandwithUseMonitorSettings settings)
    {
        _Logger = logger;
        _Settings = settings;
    }

    /// <summary>
    /// Start the monitor.
    /// </summary>
    public void Start() => _Timer = new Timer(_ => Update(), null, 0, _TimerIntervalInMilliseconds);

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public void Dispose() => _Timer?.Dispose();

    /// <summary>
    /// Get the network speed in KiB.
    /// </summary>
    /// <returns>The upload and download speeds.</returns>
    public (double, double) GetNetworkSpeedsKbps()
        => (Interlocked.CompareExchange(ref _UploadSpeedKbps, 0, 0), Interlocked.CompareExchange(ref _DownloadSpeedKbps, 0, 0));

    private void Update()
    {
        var (upload, download) = ExtractNetworkSpeeds();

        Interlocked.Exchange(ref _UploadSpeedKbps, upload);
        Interlocked.Exchange(ref _DownloadSpeedKbps, download);
    }

    private (double, double) ExtractNetworkSpeeds()
    {
        var networkInterfacePrefixesToIgnore = _Settings.InterfacePrefixesToIgnoreForNetworkBandwidthCsv == null
            ? _EmpStringArray
            : _Settings.InterfacePrefixesToIgnoreForNetworkBandwidthCsv.Split(_CsvSeparator);

        var sw = Stopwatch.StartNew();
        var (bytesOut, bytesIn) = ExtractBytesTransferredSoFar(networkInterfacePrefixesToIgnore);

        Thread.Sleep(_Settings.NetworkBandwidthSamplePeriod);

        var (bytesOutAfterInterval, bytesInAfterInterval) = ExtractBytesTransferredSoFar(networkInterfacePrefixesToIgnore);
        sw.Stop();

        return (
            CalculateNetworkSpeedInKbps(bytesOutAfterInterval - bytesOut, sw.ElapsedMilliseconds),
            CalculateNetworkSpeedInKbps(bytesInAfterInterval - bytesIn, sw.ElapsedMilliseconds)
        );
    }

    private double CalculateNetworkSpeedInKbps(long bytes, long elapsedMilliseconds) => (double)(bytes / _BytesInKilobyte) / (double)(elapsedMilliseconds / _MilisecondsInSecond);

    private (long bytesIn, long bytesOut) ExtractBytesTransferredSoFar(string[] networkInterfacePrefixesToIgnore)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return ExtractBytesTransferredSoFarWindows(networkInterfacePrefixesToIgnore);

        return ExtractBytesTransferredSoFarLinux(networkInterfacePrefixesToIgnore);
    }

    private (long bytesIn, long bytesOut) ExtractBytesTransferredSoFarLinux(string[] networkInterfacePrefixesToIgnore)
    {
        long bytesOut = 0;
        long bytesIn = 0;

        try
        {
            var netdevices = File.ReadAllLines("/proc/net/dev");

            for (int i = 2; i < netdevices.Length; i++)
            {
                var interfaces = netdevices[i].Split([_CsvSeparator, _SpaceSeparator], StringSplitOptions.RemoveEmptyEntries);
                if (interfaces.Length < 10 || interfaces[0] == null || interfaces[0].Length == 0) continue;

                var interfaceType = interfaces[0].Substring(0, interfaces[0].Length - 1);
                if (networkInterfacePrefixesToIgnore.Any(interfaceType.StartsWith)) continue;

                if (long.TryParse(interfaces[1], out long interfaceBandwithBytesOut))
                    bytesOut += interfaceBandwithBytesOut;

                if (long.TryParse(interfaces[9], out long interfaceBandwithBytesIn))
                    bytesIn += interfaceBandwithBytesIn;
            }

            return (bytesIn, bytesOut);
        }
        catch (Exception ex)
        {
            _Logger.Error("Exception happened in getting bytes transferred from /proc/net/dev. Exception : {0}", ex.Message);

            throw;
        }
    }

    private unsafe (long bytesIn, long bytesOut) ExtractBytesTransferredSoFarWindows(string[] networkInterfacePrefixesToIgnore)
    {
        long bytesOut = 0;
        long bytesIn = 0;

        MIB_IFTABLE* pIfTable = null;
        uint pdwSize = 0;

        static string _GetInterfacePrefix(uint dwType)
        {
            return dwType switch
            {
                PInvoke.IF_TYPE_ETHERNET_CSMACD => "en",
                PInvoke.IF_TYPE_PPP => "ppp",
                PInvoke.IF_TYPE_SOFTWARE_LOOPBACK => "lo",
                PInvoke.IF_TYPE_IEEE80211 => "wl",
                _ => $"unk",
            };
        }

        try
        {

            PInvoke.GetIfTable(null, ref pdwSize, false);

            pIfTable = (MIB_IFTABLE*)Marshal.AllocHGlobal((int)pdwSize);

            if (PInvoke.GetIfTable(pIfTable, ref pdwSize, false) != (uint)WIN32_ERROR.NO_ERROR)
                throw new Exception($"Failed to get IF table: {(WIN32_ERROR)Marshal.GetLastWin32Error()}");

            for (int i = 0; i < pIfTable->dwNumEntries; i++)
            {
                MIB_IFROW pIfRow = pIfTable->table[i];

                if (networkInterfacePrefixesToIgnore.Any(_GetInterfacePrefix(pIfRow.dwType).StartsWith)) continue;

                bytesOut += pIfRow.dwOutOctets;
                bytesIn += pIfRow.dwInOctets;
            }

            return (bytesIn, bytesOut);
        }
        catch (Exception ex)
        {
            _Logger.Error("Exception happened in getting bytes transferred from Win32 API. Exception : {0}", ex.Message);

            throw;
        }
        finally
        {
            if (pIfTable != null)
                Marshal.FreeHGlobal((IntPtr) pIfTable);
        }
    }
}
