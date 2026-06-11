using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;
using ExileCore2;
using ExileCore2.Shared;
using ExileCore2.Shared.Nodes;
using RectangleF = ExileCore2.Shared.RectangleF;

namespace ExileStats
{
    public class ExileStats : BaseSettingsPlugin<Settings>
    {
        private ExileStatsLogic _logic;
        private bool _isExpanded;
        private DateTime _lastClickTime = DateTime.MinValue;
        private const int ClickDebounceMs = 200;

        // Inventory auto-collapse state
        private bool _wasInventoryOpen;
        private bool _expandedBeforeInventory;

        private ExileStatsLogic Logic => _logic ??= new ExileStatsLogic(GameController);

        // Image keys used by DrawImage (filename only, as registered by InitImage)
        private const string ImgMini    = "ui-mini.png";
        private const string ImgFull    = "ui-full.png";
        private const string ImgBtnMini = "but-mini.png";
        private const string ImgBtnFull = "but-full.png";
        private const string ImgDps     = "ui-dps.png";
        private const string ImgBar     = "ui-dps-bar.png";

        // PNG dimensions
        private const float MiniW = 248, MiniH = 64;
        private const float FullW = 250, FullH = 53;
        private const float BtnW  = 16,  BtnH  = 19;
        private const float DpsW  = 313, DpsH  = 69;

        // --- Locked layout baseline (calibrated @ scale 1; everything below scales as a group) ---
        // Timer / mini panel
        private const float BtnOX = 221, BtnOY = 38;
        private const float HoursOX = 45, HoursOY = 19, MinOX = 111, MinOY = 19, SecOX = 174, SecOY = 19;
        private const float TimerTracking = 2f, TimerFont = 2.0f;
        // Stats / full panel
        private const float KillsOX = 32, KillsOY = 8, KminOX = 130, KminOY = 8;
        private const float MapsOX = 43, MapsOY = 33, MhOX = 123, MhOY = 33;
        private const float XphOX = 192, XphOY = 8, DeathsOX = 202, DeathsOY = 33;
        private const float StatsTracking = 0f, StatsFont = 1.0f;
        // DPS gauge
        private const float BarOX = 20, BarOY = 18, BarBaseW = 195, BarBaseH = 46;
        private const float NowOX = 115, NowOY = 29, PeakOX = 253, PeakOY = 24, TotalOX = 253, TotalOY = 41;
        private const float NowFont = 1.4f, StatFont = 0.9f;
        private static readonly Color ValueColor = Color.FromArgb(255, 200, 197, 164); // c8c5a4

        // Cache of single-character widths, keyed by (char, scale). Char glyph widths
        // are constant for a given scale, so we measure each once instead of every frame.
        private readonly Dictionary<(char, float), float> _charWidthCache = new();

        // Cached display strings — rebuilt only when the underlying value changes,
        // not every frame, to avoid per-frame string allocations (GC pressure).
        private string _sKills = "0", _sKmin = "0", _sMaps = "0", _sMh = "0", _sXph = "0", _sDeaths = "0";
        private int _vKills = -1, _vKmin = -1, _vMaps = -1, _vMh = -1, _vDeaths = -1;
        private double _vXph = -1;

        // Cached timer strings — rebuilt only when the displayed second changes.
        private string _sHours = "00", _sMinutes = "00", _sSeconds = "00";
        private int _vTimerSeconds = -1;

        // Standalone DPS tracker — entity HP-delta scanner, no external plugin needed
        private DpsTracker _dpsTracker;
        private string _sDpsNow = "0", _sDpsPeak = "—", _sDpsTotal = "—";
        private float _vDpsNow = -1f, _vDpsPeak = -1f, _vDpsTotal = -1f;

        public override bool Initialise()
        {
            _logic = new ExileStatsLogic(GameController);
            _dpsTracker = new DpsTracker(GameController);

            Graphics.InitImage(Path.Combine(DirectoryFullName, "images\\ui-mini.png").Replace('\\', '/'), false);
            Graphics.InitImage(Path.Combine(DirectoryFullName, "images\\ui-full.png").Replace('\\', '/'), false);
            Graphics.InitImage(Path.Combine(DirectoryFullName, "images\\but-mini.png").Replace('\\', '/'), false);
            Graphics.InitImage(Path.Combine(DirectoryFullName, "images\\but-full.png").Replace('\\', '/'), false);
            Graphics.InitImage(Path.Combine(DirectoryFullName, "images\\ui-dps.png").Replace('\\', '/'), false);
            Graphics.InitImage(Path.Combine(DirectoryFullName, "images\\ui-dps-bar.png").Replace('\\', '/'), false);

            Settings.ResetPositions.OnPressed += () =>
            {
                Settings.PosX.Value = 1445;
                Settings.PosY.Value = 933;
                Settings.DpsPanelX.Value = 818;
                Settings.DpsPanelY.Value = 868;
                Settings.GroupScale.Value = 1.0f;
                Settings.DpsScale.Value = 1.0f;
            };

            return true;
        }

        public override void AreaChange(AreaInstance area)
        {
            Logic.OnAreaChange(area);
            _dpsTracker.Reset();

            // Reset DPS display so the bar/numbers start from zero in the new area
            // (avoids residual lerp value carrying over from the previous map).
            _vDpsNow = _vDpsPeak = _vDpsTotal = -1f;
            _sDpsNow = "0"; _sDpsPeak = "—"; _sDpsTotal = "—";
        }

        public override void Tick()
        {
            // Freeze all stats while the ESC / "Game Paused" menu is open.
            bool gameIsPaused = GameController?.Game?.IsEscapeState ?? false;
            if (!gameIsPaused)
            {
                if (Logic.IsInMap) _dpsTracker.Update();
                Logic.Update();
                RefreshStatStrings();
            }
            HandleInventoryAutoCollapse();
            HandleToggleClick();
        }

        // Rebuild the cached display strings only when a value actually changes.
        private void RefreshStatStrings()
        {
            int kills = Logic.MapKills;
            if (kills != _vKills) { _vKills = kills; _sKills = kills.ToString(); }

            int kmin = (int)Math.Round(Logic.KillsPerMin);
            if (kmin != _vKmin) { _vKmin = kmin; _sKmin = kmin.ToString(); }

            int maps = Logic.MapsCompleted;
            if (maps != _vMaps) { _vMaps = maps; _sMaps = maps.ToString(); }

            int mh = (int)Math.Round(Logic.MapsPerHour);
            if (mh != _vMh) { _vMh = mh; _sMh = mh.ToString(); }

            double xph = Logic.XpPerHour;
            if (Math.Abs(xph - _vXph) > 0.5) { _vXph = xph; _sXph = ExileStatsLogic.FormatXpPerHour(xph); }

            int deaths = Logic.SessionDeaths;
            if (deaths != _vDeaths) { _vDeaths = deaths; _sDeaths = deaths.ToString(); }

            // Timer — rebuild only when the whole-second value changes
            var elapsed = Logic.MapElapsed;
            int totalSecs = (int)elapsed.TotalSeconds;
            if (totalSecs != _vTimerSeconds)
            {
                _vTimerSeconds = totalSecs;
                _sHours = elapsed.Hours.ToString("00");
                _sMinutes = elapsed.Minutes.ToString("00");
                _sSeconds = elapsed.Seconds.ToString("00");
            }

            // DPS — read from the built-in HP-delta tracker
            float dNow   = _dpsTracker.DpsNow;
            float dPeak  = _dpsTracker.DpsPeak;
            float dTotal = _dpsTracker.DpsTotal;

            // Smooth NOW display with lerp — bar and number animate fluidly
            const float lerpSpeed = 0.08f;
            if (_vDpsNow < 0f) _vDpsNow = dNow; // first frame: snap
            else _vDpsNow += (dNow - _vDpsNow) * lerpSpeed;
            _sDpsNow = FormatDps(_vDpsNow);

            if (Math.Abs(dPeak  - _vDpsPeak)  > 0.5f) { _vDpsPeak  = dPeak;  _sDpsPeak  = FormatDps(dPeak);  }
            if (Math.Abs(dTotal - _vDpsTotal) > 0.5f) { _vDpsTotal = dTotal; _sDpsTotal = FormatDps(dTotal); }
        }

        // While the inventory (right panel) is open, force the minimised view.
        // When it closes, restore whatever state the user had before.
        private void HandleInventoryAutoCollapse()
        {
            var ingameUi = GameController?.IngameState?.IngameUi;
            bool inventoryOpen = ingameUi?.OpenRightPanel?.IsVisible ?? false;

            if (inventoryOpen && !_wasInventoryOpen)
            {
                _expandedBeforeInventory = _isExpanded;
                _isExpanded = false;
            }
            else if (!inventoryOpen && _wasInventoryOpen)
            {
                _isExpanded = _expandedBeforeInventory;
            }

            _wasInventoryOpen = inventoryOpen;
        }

        // Stats+Timer group scale.
        private float GroupScale => Settings.GroupScale.Value;

        // Stats+Timer stack. At scale 1 the layout matches the old anchor exactly:
        // left = PosX, bottom of the stack = PosY (mini rises above full). For scale > 1
        // the stack grows symmetrically from its center so it doesn't drift.
        private Vector2 StackTopLeft
        {
            get
            {
                float g = GroupScale;
                float baseLeft = Settings.PosX;
                float baseTop  = _isExpanded ? Settings.PosY - FullH - MiniH : Settings.PosY - MiniH;
                float baseW = Math.Max(MiniW, FullW);
                float baseH = _isExpanded ? (MiniH + FullH) : MiniH;
                float cx = baseLeft + baseW / 2f;
                float cy = baseTop  + baseH / 2f;
                return new Vector2(cx - baseW * g / 2f, cy - baseH * g / 2f);
            }
        }

        // Mini (timer) panel top-left within the centered stack.
        private Vector2 MiniPos => StackTopLeft;

        // Full (stats) panel top-left: directly below the mini panel.
        private Vector2 FullPos
        {
            get
            {
                var tl = StackTopLeft;
                return new Vector2(tl.X, tl.Y + MiniH * GroupScale);
            }
        }

        private void HandleToggleClick()
        {
            // Don't allow manual toggle while the inventory forces minimised view
            if (_wasInventoryOpen) return;
            if (!Input.IsKeyDown(Keys.LButton)) return;
            if ((DateTime.Now - _lastClickTime).TotalMilliseconds < ClickDebounceMs) return;

            float g = GroupScale;
            var mini = MiniPos;
            var btnRect = new RectangleF(
                mini.X + BtnOX * g,
                mini.Y + BtnOY * g,
                BtnW * g, BtnH * g);

            var mouse = Input.MousePosition;
            if (mouse.X >= btnRect.Left && mouse.X <= btnRect.Right &&
                mouse.Y >= btnRect.Top  && mouse.Y <= btnRect.Bottom)
            {
                _isExpanded = !_isExpanded;
                _lastClickTime = DateTime.Now;
            }
        }

        public override void Render()
        {
            if (!Settings.Enable) return;

            float g = GroupScale;
            var pos = MiniPos;

            // 1. Draw ui-mini background (scaled)
            Graphics.DrawImage(ImgMini, new RectangleF(pos.X, pos.Y, MiniW * g, MiniH * g));

            // 2. Timer digits (HH MM SS, centred in each block, with letter-spacing)
            using (Graphics.SetTextScale(TimerFont * g))
            {
                float tk = TimerTracking * g;
                DrawCentred(_sHours,   pos, HoursOX * g, HoursOY * g, tk, TimerFont * g);
                DrawCentred(_sMinutes, pos, MinOX   * g, MinOY   * g, tk, TimerFont * g);
                DrawCentred(_sSeconds, pos, SecOX   * g, SecOY   * g, tk, TimerFont * g);
            }

            // 3. Toggle button (scaled)
            var btnImg = _isExpanded ? ImgBtnFull : ImgBtnMini;
            Graphics.DrawImage(btnImg, new RectangleF(
                pos.X + BtnOX * g,
                pos.Y + BtnOY * g,
                BtnW * g, BtnH * g));

            RenderDpsGauge();

            // 5. Stats panel (below the mini panel within the centered stack)
            if (!_isExpanded) return;

            var fp = FullPos;
            Graphics.DrawImage(ImgFull, new RectangleF(fp.X, fp.Y, FullW * g, FullH * g));

            using (Graphics.SetTextScale(StatsFont * g))
            {
                float sk = StatsTracking * g;
                DrawCentred(_sKills,  fp, KillsOX  * g, KillsOY  * g, sk, StatsFont * g);
                DrawCentred(_sKmin,   fp, KminOX   * g, KminOY   * g, sk, StatsFont * g);
                DrawCentred(_sMaps,   fp, MapsOX   * g, MapsOY   * g, sk, StatsFont * g);
                DrawCentred(_sMh,     fp, MhOX     * g, MhOY     * g, sk, StatsFont * g);
                DrawCentred(_sXph,    fp, XphOX    * g, XphOY    * g, sk, StatsFont * g);
                DrawCentred(_sDeaths, fp, DeathsOX * g, DeathsOY * g, sk, StatsFont * g);
            }
        }

        // Draws text left-aligned at (origin + offset), with optional per-character letter spacing.
        // offX/offY/tracking/scale are already multiplied by the group scale by the caller.
        private void DrawCentred(string text, Vector2 origin, float offX, float offY, float tracking, float scale)
        {
            var color = ValueColor;
            var startX = origin.X + offX;
            var startY = origin.Y + offY;

            if (tracking <= 0f)
            {
                Graphics.DrawText(text, new Vector2(startX, startY), color);
                return;
            }

            float cursorX = startX;
            foreach (var ch in text)
            {
                Graphics.DrawText(ch.ToString(), new Vector2(cursorX, startY), color);
                cursorX += GetCharWidth(ch, scale) + tracking;
            }
        }

        // Returns the rendered width of a single character at the given scale, cached.
        private float GetCharWidth(char ch, float scale)
        {
            var key = (ch, scale);
            if (_charWidthCache.TryGetValue(key, out var w)) return w;
            w = Graphics.MeasureText(ch.ToString()).X;
            _charWidthCache[key] = w;
            return w;
        }

        private void RenderDpsGauge()
        {
            if (!Settings.ShowDpsGauge.Value) return;
            if (!Logic.IsInMap) return;

            var s = Settings;
            float sc = s.DpsScale.Value;
            // At scale 1 origin == (DpsPanelX,DpsPanelY) top-left (matches calibration);
            // for scale > 1 the gauge grows symmetrically from its center.
            float dcx = s.DpsPanelX.Value + DpsW / 2f;
            float dcy = s.DpsPanelY.Value + DpsH / 2f;
            var origin = new Vector2(dcx - DpsW * sc / 2f, dcy - DpsH * sc / 2f);

            // 1. PNG background first
            Graphics.DrawImage(ImgDps, new RectangleF(origin.X, origin.Y, DpsW * sc, DpsH * sc));

            // 2. Bar fill on top of PNG — Now relative to user-configured DpsMax.
            //    Full bar (fill=1) == BarBaseW (fixed width, matches the UI frame).
            float dpsMax = s.DpsMax.Value < 1 ? 1 : s.DpsMax.Value;
            float fill   = Math.Min(_vDpsNow / dpsMax, 1f);
            if (fill < 0f) fill = 0f;

            float barX = origin.X + BarOX * sc;
            float barY = origin.Y + BarOY * sc;
            float barH = BarBaseH * sc;
            float barW = BarBaseW * sc * fill;
            if (barW > 0.5f)
            {
                // Reveal: show the left `fill` fraction of the gradient 1:1 (no stretch)
                var uv = new RectangleF(0f, 0f, fill, 1f);
                Graphics.DrawImage(ImgBar, new RectangleF(barX, barY, barW, barH), uv, Color.White);
            }

            // 3. Text on top of everything
            // NOW — centered horizontally on the fixed NowOX point.
            using (Graphics.SetTextScale(NowFont * sc))
            {
                float centerX = origin.X + NowOX * sc;
                float halfW = Graphics.MeasureText(_sDpsNow).X / 2f;
                Graphics.DrawText(_sDpsNow, new Vector2(centerX - halfW, origin.Y + NowOY * sc), ValueColor);
            }

            using (Graphics.SetTextScale(StatFont * sc))
            {
                Graphics.DrawText(_sDpsPeak,  new Vector2(origin.X + PeakOX  * sc, origin.Y + PeakOY  * sc), ValueColor);
                Graphics.DrawText(_sDpsTotal, new Vector2(origin.X + TotalOX * sc, origin.Y + TotalOY * sc), ValueColor);
            }
        }

        private static string FormatDps(float v)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            if (v >= 1_000_000f) return (v / 1_000_000f).ToString("0.00", ci) + "M";
            if (v >= 1_000f)     return (v / 1_000f).ToString("0.0", ci) + "k";
            return ((int)v).ToString(ci);
        }
    }
}
