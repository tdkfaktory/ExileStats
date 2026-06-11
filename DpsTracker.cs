using System;
using System.Collections.Generic;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.Shared.Enums;

namespace ExileStats
{
    // Standalone DPS tracker: sums HP decreases across visible monsters between
    // ticks (entity HP-delta). Damage feeds a decaying accumulator (half-life 5s)
    // so the gauge behaviour and DpsMax calibration match the previous AimCore bridge.
    public class DpsTracker
    {
        private readonly GameController _gc;

        // Per-entity HP cache: Id -> (last known HP, last seen timestamp)
        private readonly Dictionary<uint, (int Hp, DateTime Seen)> _hpCache = new();
        private readonly List<uint> _evictBuffer = new();

        // Decaying accumulator — continuous decay: acc *= e^(-dt * ln2 / half_life)
        private const float DmgHalfLifeS = 5.0f;
        private float _dmgAccumulator;
        private DateTime _accumLastTick = DateTime.MinValue;

        public float DpsPeak  { get; private set; }
        public float DpsTotal { get; private set; }

        // Throttle: HP-delta sampling at ~10Hz is plenty and cheap.
        private DateTime _lastScan = DateTime.MinValue;
        private const double ScanIntervalMs = 100;

        // Only monsters near the player count (on-screen combat range).
        private const float ScanRange = 150f;

        // Cache entries not refreshed for this long are dropped without counting
        // (despawn/off-screen, NOT a confirmed kill).
        private const double EvictAfterSeconds = 3.0;

        public DpsTracker(GameController gc) { _gc = gc; }

        // DpsNow with passive decay applied — falls to 0 when idle.
        public float DpsNow
        {
            get
            {
                if (_accumLastTick == DateTime.MinValue || _dmgAccumulator <= 0f) return 0f;
                double dt = (DateTime.Now - _accumLastTick).TotalSeconds;
                return _dmgAccumulator * (float)Math.Exp(-dt * 0.693147f / DmgHalfLifeS);
            }
        }

        public void Reset()
        {
            _hpCache.Clear();
            _dmgAccumulator = 0f;
            _accumLastTick = DateTime.MinValue;
            DpsPeak = 0f;
            DpsTotal = 0f;
        }

        public void Update()
        {
            var now = DateTime.Now;
            if ((now - _lastScan).TotalMilliseconds < ScanIntervalMs) return;
            _lastScan = now;

            float damageThisTick = 0f;

            var monsters = _gc?.EntityListWrapper?.ValidEntitiesByType[EntityType.Monster];
            if (monsters == null) return;

            foreach (var entity in monsters)
            {
                try
                {
                    // Null + IsValid BEFORE any other property read — reading IsAlive or
                    // components on a freed entity is a native access violation (uncatchable).
                    if (entity == null || !entity.IsValid) continue;
                    if (entity.DistancePlayer > ScanRange) continue;
                    if (!entity.IsHostile) continue;

                    uint id = entity.Id;
                    int hp = GetHp(entity);
                    bool alive = entity.IsAlive && hp > 0;

                    if (_hpCache.TryGetValue(id, out var prev))
                    {
                        if (!alive)
                        {
                            // Confirmed death while tracked: remaining HP counts as damage.
                            if (prev.Hp > 0) damageThisTick += prev.Hp;
                            _hpCache.Remove(id);
                        }
                        else if (hp < prev.Hp)
                        {
                            damageThisTick += prev.Hp - hp;
                            _hpCache[id] = (hp, now);
                        }
                        else
                        {
                            // Healed or unchanged: rebase without counting.
                            _hpCache[id] = (hp, now);
                        }
                    }
                    else if (alive)
                    {
                        _hpCache[id] = (hp, now); // first sighting: baseline only
                    }
                }
                catch { /* stale entity mid-read */ }
            }

            // Evict entries not refreshed recently (despawn/off-screen — not counted).
            _evictBuffer.Clear();
            foreach (var kv in _hpCache)
                if ((now - kv.Value.Seen).TotalSeconds > EvictAfterSeconds)
                    _evictBuffer.Add(kv.Key);
            foreach (var id in _evictBuffer)
                _hpCache.Remove(id);

            if (damageThisTick > 0f)
            {
                DpsTotal += damageThisTick;
                AddToAccumulator(damageThisTick, now);
            }
        }

        private void AddToAccumulator(float damage, DateTime now)
        {
            if (_accumLastTick != DateTime.MinValue)
            {
                double dt = (now - _accumLastTick).TotalSeconds;
                _dmgAccumulator *= (float)Math.Exp(-dt * 0.693147f / DmgHalfLifeS);
            }
            _dmgAccumulator += damage;
            _accumLastTick = now;
            if (_dmgAccumulator > DpsPeak) DpsPeak = _dmgAccumulator;
        }

        private static int GetHp(Entity entity)
        {
            try
            {
                if (entity.TryGetComponent<Life>(out var life) && life != null)
                    return life.CurHP;
            }
            catch { }
            return 0;
        }
    }
}
