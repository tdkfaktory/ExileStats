using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;

namespace ExileStats
{
    public class Settings : ISettings
    {
        [Menu("Enabled")]
        public ToggleNode Enable { get; set; } = new ToggleNode(true);

        [Menu("Show DPS Gauge")]
        public ToggleNode ShowDpsGauge { get; set; } = new ToggleNode(true);

        // --- Positions (panels grow from their center when scaled) ---
        [Menu("Stats+Timer Position X")]
        public RangeNode<int> PosX { get; set; } = new RangeNode<int>(1445, 0, 3840);

        [Menu("Stats+Timer Position Y")]
        public RangeNode<int> PosY { get; set; } = new RangeNode<int>(933, 0, 2160);

        [Menu("DPS Gauge Position X")]
        public RangeNode<int> DpsPanelX { get; set; } = new RangeNode<int>(818, 0, 3840);

        [Menu("DPS Gauge Position Y")]
        public RangeNode<int> DpsPanelY { get; set; } = new RangeNode<int>(868, 0, 2160);

        // --- Per-group proportional scale ---
        [Menu("Stats+Timer Scale")]
        public RangeNode<float> GroupScale { get; set; } = new RangeNode<float>(1.0f, 0.5f, 2f);

        [Menu("DPS Bar Scale")]
        public RangeNode<float> DpsScale { get; set; } = new RangeNode<float>(1.0f, 0.5f, 2f);

        [Menu("DPS Max (bar = 100% at this value)")]
        public RangeNode<int> DpsMax { get; set; } = new RangeNode<int>(1_000_000, 1_000, 10_000_000);

        [Menu("Reset positions")]
        public ButtonNode ResetPositions { get; set; } = new ButtonNode();
    }
}
