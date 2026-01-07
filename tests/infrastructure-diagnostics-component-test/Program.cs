using Roblox.Infrastructure.Diagnostics;

var bandwidthMonitor = new BandwidthUseMonitor(Roblox.EventLog.Logger.Singleton, new BandwithUseMonitorSettings());

bandwidthMonitor.Start();

// wait some time to gather data
System.Threading.Thread.Sleep(25000); // 25 seconds

Console.WriteLine(bandwidthMonitor.GetNetworkSpeedsKbps());

class BandwithUseMonitorSettings : IBandwithUseMonitorSettings
{
    public string InterfacePrefixesToIgnoreForNetworkBandwidthCsv { get => "lo,docker,veth,br-";  set { } }
    public TimeSpan NetworkBandwidthSamplePeriod { get => TimeSpan.FromSeconds(10);  set { } }
}