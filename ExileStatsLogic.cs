using System;
using System.Collections.Generic;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.Shared.Enums;

namespace ExileStats
{
    public class ExileStatsLogic
    {
        private readonly GameController _gc;

        // Map state
        public bool IsInMap { get; private set; }
        public DateTime AreaEnterTime { get; private set; }
        private uint _areaStartXp;
        private int _killsAtAreaEnter;

        // Baseline settling: after an area change the game's memory may still hold
        // the previous area's stats for a moment, producing absurd deltas. We wait
        // a short window before locking the baseline, and re-read it until then.
        private bool _baselineLocked;
        private const double BaselineSettleSeconds = 1.5;

        // Session state
        private DateTime _sessionStart;
        private bool _sessionStarted;
        private bool _wasDead;

        // Throttle memory reads: stats don't need 60Hz updates. ~4Hz is plenty.
        private DateTime _lastStatsRead = DateTime.MinValue;
        private const double StatsReadIntervalMs = 250;

        // Map-counting state
        private uint _lastCountedMapHash;
        private DateTime _mapClockStart;
        private bool _mapClockStarted;

        // Public stats — map
        public TimeSpan MapElapsed => IsInMap ? DateTime.Now - AreaEnterTime : TimeSpan.Zero;
        public int MapKills { get; private set; }
        public double KillsPerMin => MapElapsed.TotalSeconds >= 10
            ? MapKills / (MapElapsed.TotalSeconds / 60.0)
            : 0;
        public double XpPerHour { get; private set; }

        // Public stats — session
        public int MapsCompleted { get; private set; }
        // Maps/hour: based on time since the first real map was entered, and only
        // meaningful after at least one full map (>= 1) and a few minutes elapsed.
        public double MapsPerHour
        {
            get
            {
                if (!_mapClockStarted || MapsCompleted < 1) return 0;
                var hours = (DateTime.Now - _mapClockStart).TotalHours;
                if (hours < (5.0 / 60.0)) return 0; // need >= 5 min before showing a rate
                return MapsCompleted / hours;
            }
        }
        public int SessionDeaths { get; private set; }

        public ExileStatsLogic(GameController gc)
        {
            _gc = gc;
        }

        // Endgame Waystone maps (PoE2 0.5): always Act 10, area level 65 (T1) to ~82 (T16).
        // Campaign trials/areas sit in Acts 1-9 at lower levels (e.g. Well of Souls = Act 2, lvl 22).
        // Confirmed via in-game debug: real maps report Act=10, Level=78-80.
        private const int EndgameAct = 10;
        private const int EndgameMinLevel = 65;

        private static bool IsEndgameMap(AreaInstance area)
        {
            if (area.IsPeaceful || area.IsTown || area.IsHideout) return false;
            return area.Act == EndgameAct && area.RealLevel >= EndgameMinLevel;
        }

        public void OnAreaChange(AreaInstance area)
        {
            if (!_sessionStarted)
            {
                _sessionStart = DateTime.Now;
                _sessionStarted = true;
            }

            // Timer runs in any hostile area (so the clock works in campaign too)
            bool isHostile = !area.IsPeaceful && !area.IsTown && !area.IsHideout;
            bool isEndgame = IsEndgameMap(area);

            if (isHostile)
            {
                // Only count NEW endgame maps (T1-T16), by unique instance hash.
                // Re-entering the same map (same hash) does not count again.
                if (isEndgame && area.Hash != _lastCountedMapHash)
                {
                    MapsCompleted++;
                    _lastCountedMapHash = area.Hash;

                    if (!_mapClockStarted)
                    {
                        _mapClockStart = DateTime.Now;
                        _mapClockStarted = true;
                    }
                }

                AreaEnterTime = DateTime.Now;
                // Provisional baseline; locked in after the settle window in Update()
                _areaStartXp = _gc.Player?.GetComponent<Player>()?.XP ?? 0;
                _killsAtAreaEnter = TryGetStat(GameStat.CharacterKillCount);
                _baselineLocked = false;
                IsInMap = true;
            }
            else
            {
                IsInMap = false;
            }

            MapKills = 0;
            XpPerHour = 0;
            _wasDead = false;
        }

        public void Update()
        {
            if (!_sessionStarted) return;

            // Throttle: only read game memory a few times per second.
            var now = DateTime.Now;
            if ((now - _lastStatsRead).TotalMilliseconds < StatsReadIntervalMs) return;
            _lastStatsRead = now;

            // Track deaths via Life component — detect transition to 0 HP
            var life = _gc.Player?.GetComponent<Life>();
            if (life != null)
            {
                bool isDead = life.CurHP <= 0 && life.MaxHP > 0;
                if (isDead && !_wasDead)
                    SessionDeaths++;
                _wasDead = isDead;
            }

            if (!IsInMap) return;

            var currentKills = TryGetStat(GameStat.CharacterKillCount);
            var currentXp = _gc.Player?.GetComponent<Player>()?.XP ?? 0;

            // During the settle window keep re-reading the baseline so it reflects
            // the new area's true starting values (memory may lag the area change).
            if (!_baselineLocked)
            {
                _killsAtAreaEnter = currentKills;
                _areaStartXp = currentXp;
                MapKills = 0;
                XpPerHour = 0;

                if (MapElapsed.TotalSeconds >= BaselineSettleSeconds)
                    _baselineLocked = true;
                return;
            }

            // Kills (clamp to avoid negative/absurd deltas if memory hiccups)
            MapKills = Math.Max(0, currentKills - _killsAtAreaEnter);

            // XP/hour — measured from when the baseline locked
            var xpGained = (long)currentXp - _areaStartXp;
            var elapsed = MapElapsed.TotalSeconds - BaselineSettleSeconds;
            if (elapsed >= 10 && xpGained > 0)
                XpPerHour = xpGained / elapsed * 3600.0;
        }

        private int TryGetStat(GameStat stat)
        {
            var stats = _gc?.Player?.Stats;
            if (stats == null) return 0;
            stats.TryGetValue(stat, out var value);
            return value;
        }

        public static string FormatXpPerHour(double xph)
        {
            if (xph <= 0) return "0";
            if (xph >= 1_000_000_000) return $"{(int)Math.Round(xph / 1_000_000_000.0)}B";
            if (xph >= 1_000_000) return $"{(int)Math.Round(xph / 1_000_000.0)}M";
            if (xph >= 1_000) return $"{(int)Math.Round(xph / 1_000.0)}K";
            return $"{(int)Math.Round(xph)}";
        }
    }
}
