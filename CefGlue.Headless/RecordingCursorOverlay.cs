using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Xilium.CefGlue.Headless
{
    internal sealed class RecordingCursorOverlay : IDisposable
    {
        private const int RippleMs = 450;
        private const int PumpMs = 16;

        private enum Shape { Arrow, IBeam, Hand }

        private static readonly string[] ArrowSprite =
        {
            "X",
            "XX",
            "X.X",
            "X..X",
            "X...X",
            "X....X",
            "X.....X",
            "X......X",
            "X.......X",
            "X........X",
            "X.........X",
            "X......XXXXX",
            "X...X..X",
            "X..XX..X",
            "X.X  X..X",
            "XX   X..X",
            "X     X..X",
            "      X..X",
            "       XX",
        };

        private static readonly string[] HandSprite =
        {
            "    XX",
            "   X..X",
            "   X..X",
            "   X..X",
            "   X..X",
            "   X..XXX",
            "   X..X..XXX",
            "   X..X..X..XX",
            "XX X..X..X..X.X",
            "X..X........X..X",
            "X...X..........X",
            " X.............X",
            "  X............X",
            "  X...........X",
            "   X..........X",
            "   X.........X",
            "    X........X",
            "    X........X",
            "    XXXXXXXXXX",
        };

        private const int HandHotspotX = 5;

        private static readonly string[] IBeamSprite =
        {
            "XXX XXX",
            "   X",
            "   X",
            "   X",
            "   X",
            "   X",
            "   X",
            "   X",
            "   X",
            "   X",
            "   X",
            "   X",
            "   X",
            "   X",
            "   X",
            "XXX XXX",
        };

        private readonly object _gate = new object();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly List<(double X, double Y, long StartMs)> _ripples = new List<(double, double, long)>();
        private readonly Action _recomposite;
        private Timer _pump;
        private bool _enabled;
        private bool _hasPosition;
        private double _x, _y;
        private int _viewWidth, _viewHeight;
        private Shape _shape = Shape.Arrow;
        private CefCursorType _cursorType = CefCursorType.Pointer;

        private double _displayScale;
        private bool _dirty;
        private long _dirtySinceMs;
        private long _lastRealFrameMs = -1;

        public RecordingCursorOverlay(Action recomposite)
        {
            _recomposite = recomposite;
        }

        public bool Enabled
        {
            get { lock (_gate) return _enabled; }
            set
            {
                lock (_gate)
                {
                    if (_enabled == value) return;
                    _enabled = value;
                    _ripples.Clear();
                    MarkDirtyLocked();
                }
            }
        }

        public void Move(int x, int y, int viewWidth, int viewHeight)
        {
            lock (_gate)
            {
                _x = x; _y = y; _viewWidth = viewWidth; _viewHeight = viewHeight; _hasPosition = true;
                if (_enabled) MarkDirtyLocked();
            }
        }

        public void Click(int x, int y, int viewWidth, int viewHeight)
        {
            lock (_gate)
            {
                _x = x; _y = y; _viewWidth = viewWidth; _viewHeight = viewHeight; _hasPosition = true;
                if (!_enabled) return;
                _ripples.Add((x, y, _clock.ElapsedMilliseconds));
                MarkDirtyLocked();
            }
        }

        public void SetDisplayScale(double scale)
        {
            if (double.IsNaN(scale) || scale <= 0) return;
            lock (_gate)
            {
                _displayScale = Math.Clamp(scale, 0.25, 8);
                if (_enabled) MarkDirtyLocked();
            }
        }

        public void SetCursorType(CefCursorType type)
        {
            var shape = type switch
            {
                CefCursorType.IBeam or CefCursorType.VerticalText => Shape.IBeam,
                CefCursorType.Hand => Shape.Hand,
                _ => Shape.Arrow,
            };

            lock (_gate)
            {
                if (_shape == shape && _cursorType == type) return;
                _shape = shape;
                _cursorType = type;
                if (_enabled) MarkDirtyLocked();
            }
        }

        public void NoteRealFrame()
        {
            lock (_gate) _lastRealFrameMs = _clock.ElapsedMilliseconds;
        }

        public void Draw(byte[] buffer, int width, int height)
        {
            double x, y, scale, sizeScale;
            Shape shape;
            CefCursorType cursorType;
            (double X, double Y, long StartMs)[] ripples;
            long now;

            lock (_gate)
            {
                if (!_enabled || !_hasPosition || _viewWidth <= 0) return;
                now = _clock.ElapsedMilliseconds;
                _ripples.RemoveAll(r => now - r.StartMs > RippleMs);
                ripples = _ripples.ToArray();
                scale = width / (double)_viewWidth;
                x = _x * scale;
                y = _y * scale;
                sizeScale = _displayScale > 0 ? _displayScale : scale;
                shape = _shape;
                cursorType = _cursorType;
            }

            foreach (var r in ripples)
            {
                DrawRipple(buffer, width, height, r.X * scale, r.Y * scale, (now - r.StartMs) / (double)RippleMs, sizeScale);
            }

            var systemId = SystemCursorBitmaps.SystemCursorId(cursorType);
            if (systemId == 0)
            {
                return;
            }

            var system = SystemCursorBitmaps.Load(systemId, Math.Max(16, (int)Math.Round(SystemCursorBitmaps.BaseSize * sizeScale)));
            if (system != null)
            {
                DrawBitmap(buffer, width, height, system, (int)Math.Round(x) - system.HotspotX, (int)Math.Round(y) - system.HotspotY);
                return;
            }

            var pixel = Math.Max(1, (int)Math.Round(sizeScale));
            switch (shape)
            {
                case Shape.IBeam:
                    DrawSprite(buffer, width, height, IBeamSprite, (int)x - 3 * pixel, (int)y - 8 * pixel, pixel);
                    break;
                case Shape.Hand:
                    DrawSprite(buffer, width, height, HandSprite, (int)x - HandHotspotX * pixel, (int)y, pixel);
                    break;
                default:
                    DrawSprite(buffer, width, height, ArrowSprite, (int)x, (int)y, pixel);
                    break;
            }
        }

        private void MarkDirtyLocked()
        {
            if (!_dirty)
            {
                _dirty = true;
                _dirtySinceMs = _clock.ElapsedMilliseconds;
            }
            _pump ??= new Timer(_ => Tick(), null, PumpMs, PumpMs);
        }

        private void Tick()
        {
            bool recomposite;
            lock (_gate)
            {
                var now = _clock.ElapsedMilliseconds;
                _ripples.RemoveAll(r => now - r.StartMs > RippleMs);
                var animating = _enabled && _ripples.Count > 0;

                var realFrameSince = _lastRealFrameMs >= _dirtySinceMs;
                var realFrameRecent = now - _lastRealFrameMs < PumpMs;
                recomposite = (_dirty && !realFrameSince) || (animating && !realFrameRecent);
                _dirty = false;
                _dirtySinceMs = now;

                if (!animating && !recomposite)
                {
                    _pump?.Dispose();
                    _pump = null;
                    return;
                }
            }

            if (recomposite)
            {
                try { _recomposite(); }
                catch (Exception ex) { Console.WriteLine($"[RecordingCursorOverlay] recomposite failed: {ex.Message}"); }
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _pump?.Dispose();
                _pump = null;
                _enabled = false;
            }
        }

        private static void DrawSprite(byte[] buf, int width, int height, string[] sprite, int left, int top, int pixel)
        {
            for (var row = 0; row < sprite.Length; row++)
            {
                var line = sprite[row];
                for (var col = 0; col < line.Length; col++)
                {
                    var c = line[col];
                    if (c != 'X' && c != '.') continue;
                    var v = c == 'X' ? (byte)0 : (byte)255;
                    for (var dy = 0; dy < pixel; dy++)
                    {
                        var py = top + row * pixel + dy;
                        if (py < 0 || py >= height) continue;
                        for (var dx = 0; dx < pixel; dx++)
                        {
                            var px = left + col * pixel + dx;
                            if (px < 0 || px >= width) continue;
                            var o = (py * width + px) * 4;
                            buf[o] = v; buf[o + 1] = v; buf[o + 2] = v; buf[o + 3] = 255;
                        }
                    }
                }
            }
        }

        private static void DrawBitmap(byte[] buf, int width, int height, SystemCursorBitmaps.CursorBitmap bmp, int left, int top)
        {
            for (var row = 0; row < bmp.Height; row++)
            {
                var py = top + row;
                if (py < 0 || py >= height) continue;
                for (var col = 0; col < bmp.Width; col++)
                {
                    var px = left + col;
                    if (px < 0 || px >= width) continue;
                    var i = row * bmp.Width + col;
                    var o = (py * width + px) * 4;
                    if (bmp.Invert[i] != 0)
                    {
                        buf[o] = (byte)(255 - buf[o]); buf[o + 1] = (byte)(255 - buf[o + 1]); buf[o + 2] = (byte)(255 - buf[o + 2]);
                        continue;
                    }

                    var s = i * 4;
                    var a = bmp.Pixels[s + 3];
                    if (a == 0) continue;
                    if (a == 255)
                    {
                        buf[o] = bmp.Pixels[s]; buf[o + 1] = bmp.Pixels[s + 1]; buf[o + 2] = bmp.Pixels[s + 2];
                        continue;
                    }

                    var f = a / 255.0;
                    buf[o] = (byte)(buf[o] * (1 - f) + bmp.Pixels[s] * f);
                    buf[o + 1] = (byte)(buf[o + 1] * (1 - f) + bmp.Pixels[s + 1] * f);
                    buf[o + 2] = (byte)(buf[o + 2] * (1 - f) + bmp.Pixels[s + 2] * f);
                }
            }
        }

        private static void DrawRipple(byte[] buf, int width, int height, double cx, double cy, double t, double scale)
        {
            if (t < 0 || t > 1) return;
            var radius = (6 + 22 * t) * scale;
            var thickness = Math.Max(1.5, 2.5 * scale);
            var ringAlpha = 0.85 * (1 - t);
            var fillAlpha = t < 0.35 ? 0.30 * (1 - t / 0.35) : 0;

            var r0 = (int)Math.Ceiling(radius + thickness);
            var minX = Math.Max(0, (int)(cx - r0)); var maxX = Math.Min(width - 1, (int)(cx + r0));
            var minY = Math.Max(0, (int)(cy - r0)); var maxY = Math.Min(height - 1, (int)(cy + r0));
            for (var py = minY; py <= maxY; py++)
            {
                for (var px = minX; px <= maxX; px++)
                {
                    var d = Math.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                    var ring = Math.Max(0, 1 - Math.Abs(d - radius) / thickness);
                    var a = ring * ringAlpha;
                    if (d < radius) a = Math.Max(a, fillAlpha);
                    if (a <= 0.01) continue;
                    var o = (py * width + px) * 4;
                    buf[o] = (byte)(buf[o] * (1 - a) + 0xF6 * a);
                    buf[o + 1] = (byte)(buf[o + 1] * (1 - a) + 0x82 * a);
                    buf[o + 2] = (byte)(buf[o + 2] * (1 - a) + 0x3B * a);
                }
            }
        }
    }
}
