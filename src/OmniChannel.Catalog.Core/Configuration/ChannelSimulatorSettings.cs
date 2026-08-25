namespace OmniChannel.Catalog.Core.Configuration;

public class ChannelSimulatorSettings
{
    public bool Enabled { get; set; } = true;
    public int IntervalMs { get; set; } = 2000;
    public int BatchSize { get; set; } = 200;
}