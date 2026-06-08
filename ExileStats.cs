using System;
using System.Collections.Generic;
using System.IO;
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

        // PNG dimensions
        private const float MiniW = 248, MiniH = 64;
        private const float FullW = 250, FullH = 53;
        private const float BtnW  = 16,  BtnH  = 19;

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

        public override bool Initialise()
        {
            _logic = new ExileStatsLogic(GameController);

            Graphics.InitImage(Path.Combine(DirectoryFullName, "images\\ui-mini.png").Replace('\\', '/'), false);
            Graphics.InitImage(Path.Combine(DirectoryFullName, "images\\ui-full.png").Replace('\\', '/'), false);
            Graphics.InitImage(Path.Combine(DirectoryFullName, "images\\but-mini.png").Replace('\\', '/'), false);
            Graphics.InitImage(Path.Combine(DirectoryFullName, "images\\but-full.png").Replace('\\', '/'), false);

            return true;
        }

        public override void AreaChange(AreaInstance area)
        {
            if (Settings.DebugArea)
            {
                DebugWindow.LogMsg(
                    $"[ExileStats] Area='{area.Name}' Act={area.Act} Level={area.RealLevel} " +
                    $"Peaceful={area.IsPeaceful} Town={area.IsTown} Hideout={area.IsHideout} Hash={area.Hash}");
            }
            Logic.OnAreaChange(area);
        }

        public override void Tick()
        {
            Logic.Update();
            RefreshStatStrings();
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

        // The mini (timer) panel top-left, anchored so the whole stack's bottom stays at PosY.
        // Minimised: only mini, bottom at PosY  →  mini top = PosY - MiniH
        // Expanded:  full sits at the bottom, mini rises above it
        //            full top = PosY - FullH ; mini top = PosY - FullH - MiniH
        private Vector2 MiniPos => new Vector2(
            Settings.PosX,
            _isExpanded ? Settings.PosY - FullH - MiniH : Settings.PosY - MiniH);

        private Vector2 FullPos => new Vector2(Settings.PosX, Settings.PosY - FullH);

        private void HandleToggleClick()
        {
            // Don't allow manual toggle while the inventory forces minimised view
            if (_wasInventoryOpen) return;
            if (!Input.IsKeyDown(Keys.LButton)) return;
            if ((DateTime.Now - _lastClickTime).TotalMilliseconds < ClickDebounceMs) return;

            var mini = MiniPos;
            var btnRect = new RectangleF(
                mini.X + Settings.BtnOffX,
                mini.Y + Settings.BtnOffY,
                BtnW, BtnH);

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

            var pos = MiniPos;

            // 1. Draw ui-mini background
            Graphics.DrawImage(ImgMini, new RectangleF(pos.X, pos.Y, MiniW, MiniH));

            // 2. Timer digits (HH MM SS, centred in each block, with letter-spacing)
            var ts = Settings.TimerScale.Value;
            using (Graphics.SetTextScale(ts))
            {
                var tk = Settings.TimerTracking.Value;
                DrawCentred(_sHours,   pos, Settings.OffHoursX,   Settings.OffHoursY,   tk, ts);
                DrawCentred(_sMinutes, pos, Settings.OffMinutesX, Settings.OffMinutesY, tk, ts);
                DrawCentred(_sSeconds, pos, Settings.OffSecondsX, Settings.OffSecondsY, tk, ts);
            }

            // 3. Toggle button
            var btnImg = _isExpanded ? ImgBtnFull : ImgBtnMini;
            Graphics.DrawImage(btnImg, new RectangleF(
                pos.X + Settings.BtnOffX,
                pos.Y + Settings.BtnOffY,
                BtnW, BtnH));

            // 4. Stats panel (anchored at the bottom; timer rises above it)
            if (!_isExpanded) return;

            var fp = FullPos;
            Graphics.DrawImage(ImgFull, new RectangleF(fp.X, fp.Y, FullW, FullH));

            var ss = Settings.StatsScale.Value;
            using (Graphics.SetTextScale(ss))
            {
                var sk = Settings.StatsTracking.Value;
                DrawCentred(_sKills,  fp, Settings.OffKillsX,  Settings.OffKillsY,  sk, ss);
                DrawCentred(_sKmin,   fp, Settings.OffKminX,   Settings.OffKminY,   sk, ss);
                DrawCentred(_sMaps,   fp, Settings.OffMapsX,   Settings.OffMapsY,   sk, ss);
                DrawCentred(_sMh,     fp, Settings.OffMhX,     Settings.OffMhY,     sk, ss);
                DrawCentred(_sXph,    fp, Settings.OffXphX,    Settings.OffXphY,    sk, ss);
                DrawCentred(_sDeaths, fp, Settings.OffDeathsX, Settings.OffDeathsY, sk, ss);
            }
        }

        // Draws text left-aligned at (origin + offset), with optional per-character letter spacing.
        private void DrawCentred(string text, Vector2 origin, RangeNode<int> offX, RangeNode<int> offY, float tracking, float scale)
        {
            var color = Settings.ValueColor.Value;
            var startX = origin.X + offX.Value;
            var startY = origin.Y + offY.Value;

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
    }
}
