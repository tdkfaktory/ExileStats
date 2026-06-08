using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;
using System.Drawing;

namespace ExileStats
{
    public class Settings : ISettings
    {
        [Menu("Enabled")]
        public ToggleNode Enable { get; set; } = new ToggleNode(true);

        [Menu("Debug: log area info on entry")]
        public ToggleNode DebugArea { get; set; } = new ToggleNode(false);

        [Menu("Panel Position X")]
        public RangeNode<int> PosX { get; set; } = new RangeNode<int>(1445, 0, 3840);

        [Menu("Panel Position Y")]
        public RangeNode<int> PosY { get; set; } = new RangeNode<int>(864, 0, 2160);

        [Menu("Timer Scale (≈20pt)")]
        public RangeNode<float> TimerScale { get; set; } = new RangeNode<float>(2.0f, 0.5f, 4f);

        [Menu("Timer Letter Spacing")]
        public RangeNode<float> TimerTracking { get; set; } = new RangeNode<float>(2f, 0f, 10f);

        [Menu("Stats Scale")]
        public RangeNode<float> StatsScale { get; set; } = new RangeNode<float>(1.0f, 0.5f, 3f);

        [Menu("Stats Letter Spacing")]
        public RangeNode<float> StatsTracking { get; set; } = new RangeNode<float>(0f, 0f, 10f);

        [Menu("Value Color")]
        public ColorNode ValueColor { get; set; } = new ColorNode(Color.FromArgb(255, 200, 197, 164));

        // --- Button offset ---
        [Menu("Button Offset X")]
        public RangeNode<int> BtnOffX { get; set; } = new RangeNode<int>(232, 0, 300);

        [Menu("Button Offset Y")]
        public RangeNode<int> BtnOffY { get; set; } = new RangeNode<int>(22, 0, 100);

        // --- Timer: Hours, Minutes, Seconds ---
        [Menu("Hours Offset X")]
        public RangeNode<int> OffHoursX { get; set; } = new RangeNode<int>(59, -50, 300);

        [Menu("Hours Offset Y")]
        public RangeNode<int> OffHoursY { get; set; } = new RangeNode<int>(38, -50, 100);

        [Menu("Minutes Offset X")]
        public RangeNode<int> OffMinutesX { get; set; } = new RangeNode<int>(124, -50, 300);

        [Menu("Minutes Offset Y")]
        public RangeNode<int> OffMinutesY { get; set; } = new RangeNode<int>(38, -50, 100);

        [Menu("Seconds Offset X")]
        public RangeNode<int> OffSecondsX { get; set; } = new RangeNode<int>(188, -50, 300);

        [Menu("Seconds Offset Y")]
        public RangeNode<int> OffSecondsY { get; set; } = new RangeNode<int>(38, -50, 100);

        // --- Stats panel offsets ---
        [Menu("Kills Offset X")]
        public RangeNode<int> OffKillsX { get; set; } = new RangeNode<int>(66, -50, 300);

        [Menu("Kills Offset Y")]
        public RangeNode<int> OffKillsY { get; set; } = new RangeNode<int>(13, -50, 100);

        [Menu("K/min Offset X")]
        public RangeNode<int> OffKminX { get; set; } = new RangeNode<int>(185, -50, 300);

        [Menu("K/min Offset Y")]
        public RangeNode<int> OffKminY { get; set; } = new RangeNode<int>(13, -50, 100);

        [Menu("Maps Offset X")]
        public RangeNode<int> OffMapsX { get; set; } = new RangeNode<int>(13, -50, 300);

        [Menu("Maps Offset Y")]
        public RangeNode<int> OffMapsY { get; set; } = new RangeNode<int>(38, -50, 100);

        [Menu("M/h Offset X")]
        public RangeNode<int> OffMhX { get; set; } = new RangeNode<int>(73, -50, 300);

        [Menu("M/h Offset Y")]
        public RangeNode<int> OffMhY { get; set; } = new RangeNode<int>(38, -50, 100);

        [Menu("XP/h Offset X")]
        public RangeNode<int> OffXphX { get; set; } = new RangeNode<int>(147, -50, 300);

        [Menu("XP/h Offset Y")]
        public RangeNode<int> OffXphY { get; set; } = new RangeNode<int>(38, -50, 100);

        [Menu("Deaths Offset X")]
        public RangeNode<int> OffDeathsX { get; set; } = new RangeNode<int>(222, -50, 300);

        [Menu("Deaths Offset Y")]
        public RangeNode<int> OffDeathsY { get; set; } = new RangeNode<int>(38, -50, 100);
    }
}
