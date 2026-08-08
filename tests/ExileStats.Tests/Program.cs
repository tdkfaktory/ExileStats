using System;
using ExileStats;

var cases = new (double Value, string Expected)[]
{
    (0, "0"),
    (999, "999"),
    (1_499, "1K"),
    (1_500_000, "2M"),
    (1_500_000_000, "2B"),
};

foreach (var (value, expected) in cases)
{
    var actual = ExileStatsLogic.FormatXpPerHour(value);
    if (actual != expected)
        throw new InvalidOperationException($"FormatXpPerHour({value}) expected '{expected}', got '{actual}'.");
}

Console.WriteLine($"{cases.Length} ExileStats formatting tests passed.");
