namespace OmniChannel.Catalog.Host.Infrastructure;

public sealed class SimulatorSwitch(bool enabled)
{
    private int _enabled = enabled ? 1 : 0;

    public bool Enabled => Volatile.Read(ref _enabled) == 1;

    public void Set(bool value) => Volatile.Write(ref _enabled, value ? 1 : 0);
}