namespace DesktopDashboard.Core.Models;

[Flags]
public enum MetricKind
{
    None = 0,
    Cpu = 1 << 0,
    Memory = 1 << 1,
    Gpu = 1 << 2,
    Battery = 1 << 3,
    Network = 1 << 4,
    All = Cpu | Memory | Gpu | Battery | Network,
}
