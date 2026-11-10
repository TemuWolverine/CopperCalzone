using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using CopperCalzone.Models;
using CopperCalzone.Parsing;
using System.Diagnostics;

namespace CopperCalzone.Rendering;

public sealed class PcbCanvasControl : Control
{
    // Multi-touch & pointer tracking
    private readonly Dictionary<int, Point> _activePointers = [];

    private readonly Stopwatch _flyStopwatch = new();
    private readonly HashSet<string> _highlightedFootprintRefs = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _placedFootprintRefs = new(StringComparer.OrdinalIgnoreCase);
    private readonly SpatialIndex _spatialIndex = new();

    private readonly HashSet<string> _visibleLayers = new(StringComparer.Ordinal)
    {
        "Edge.Cuts", "F.Cu", "B.Cu", "In1.Cu", "In2.Cu", "In3.Cu", "In4.Cu",
        "F.SilkS", "B.SilkS", "F.Mask", "B.Mask"
    };

    private string? _activeFootprintRef;
    private int? _activeNetId;
    private PcbBoard? _board;
    private double _flyDurationMs = 380;
    private double _flyStartPanX, _flyEndPanX;
    private double _flyStartPanY, _flyEndPanY;
    private double _flyStartScale, _flyEndScale;

    // Smooth camera animation (FlyTo)
    private DispatcherTimer? _flyTimer;

    private PcbPad? _hoveredPad;
    private bool _isFlipped;
    private double _lastPinchAngle;
    private double _lastPinchDistance;
    private Point _lastPinchMidpoint;
    private PointerType _lastPointerType = PointerType.Mouse;
    private bool _movedBeyondTapThreshold;
    private double _panX;
    private double _panY;
    private Point _pointerDownPos;
    private double _rotation;
    private double _scale = 1.0;
    private PcbPad? _selectedPad;
    private bool _showPadNetNames = true;
    private ZoneDisplayMode _zoneMode = ZoneDisplayMode.Solid;

    public event Action<string>? CursorCoordinatesChanged;

    public event Action<PcbPad?, Point>? HoveredPadChanged;

    public event Action<PcbPad?>? PadClicked;

    public event Action<int>? ZoomPercentageChanged;

    public PcbBoard? Board => _board;

    public bool IsFlipped => _isFlipped;

    public double Rotation => _rotation;

    public bool ShowPadNetNames => _showPadNetNames;

    public IReadOnlySet<string> VisibleLayers => _visibleLayers;

    public ZoneDisplayMode ZoneMode => _zoneMode;

    public PcbCanvasControl()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    public void FitBoardToView()
    {
        StopAnimation();
        if (_board?.Bbox is null) return;

        double width = Bounds.Width > 10 ? Bounds.Width : 900;
        double height = Bounds.Height > 10 ? Bounds.Height : 650;

        var bbox = _board.Bbox;
        const double padPx = 40.0;
        double availW = Math.Max(100.0, width - padPx * 2.0);
        double availH = Math.Max(100.0, height - padPx * 2.0);

        double bw = Math.Max(1.0, bbox.Width);
        double bh = Math.Max(1.0, bbox.Height);

        double rad = _rotation * (Math.PI / 180.0);
        double cos = Math.Abs(Math.Cos(rad));
        double sin = Math.Abs(Math.Sin(rad));
        double effBw = bw * cos + bh * sin;
        double effBh = bw * sin + bh * cos;

        _scale = Math.Min(availW / effBw, availH / effBh);

        double effCx = _isFlipped ? -bbox.CenterX : bbox.CenterX;
        double x2 = effCx * _scale;
        double y2 = bbox.CenterY * _scale;

        double rx = x2 * Math.Cos(rad) - y2 * Math.Sin(rad);
        double ry = x2 * Math.Sin(rad) + y2 * Math.Cos(rad);

        _panX = width / 2.0 - rx;
        _panY = height / 2.0 - ry;

        NotifyZoomChanged();
        InvalidateVisual();
    }

    public void FlyTo(double worldX, double worldY, double? targetScale = null, double durationMs = 380)
    {
        StopAnimation();

        double width = Bounds.Width > 10 ? Bounds.Width : 900;
        double height = Bounds.Height > 10 ? Bounds.Height : 650;

        _flyStartScale = _scale;
        _flyStartPanX = _panX;
        _flyStartPanY = _panY;

        // Ensure close enough zoom to inspect the pin clearly
        double minPinScale = _board?.Bbox is { } b
            ? Math.Max(14.0, Math.Min(width, height) / Math.Max(Math.Min(b.Width, b.Height) * 0.22, 1.0))
            : 18.0;

        _flyEndScale = targetScale ?? Math.Max(_scale, minPinScale);
        double effWorldX = _isFlipped ? -worldX : worldX;
        double x2 = effWorldX * _flyEndScale;
        double y2 = worldY * _flyEndScale;
        double rad = _rotation * (Math.PI / 180.0);
        double rx = x2 * Math.Cos(rad) - y2 * Math.Sin(rad);
        double ry = x2 * Math.Sin(rad) + y2 * Math.Cos(rad);
        _flyEndPanX = width / 2.0 - rx;
        _flyEndPanY = height / 2.0 - ry;
        _flyDurationMs = Math.Max(50, durationMs);

        _flyStopwatch.Restart();
        _flyTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, OnFlyTick);
        _flyTimer.Start();
    }

    public void FlyToBounds(double minX, double minY, double maxX, double maxY, double durationMs = 380)
    {
        StopAnimation();

        double cx = (minX + maxX) / 2.0;
        double cy = (minY + maxY) / 2.0;
        double spanX = Math.Max(maxX - minX + 8.0, 6.0);
        double spanY = Math.Max(maxY - minY + 8.0, 6.0);

        double width = Bounds.Width > 10 ? Bounds.Width : 900;
        double height = Bounds.Height > 10 ? Bounds.Height : 650;

        double fitScale = Math.Min((width * 0.70) / spanX, (height * 0.70) / spanY);
        fitScale = Math.Clamp(fitScale, 4.0, 45.0);

        FlyTo(cx, cy, targetScale: fitScale, durationMs: durationMs);
    }

    public void FlyToHighlightedFootprints()
    {
        if (_board is null || _highlightedFootprintRefs.Count == 0) return;

        var targetFootprints = _board.Footprints
            .Where(f => _highlightedFootprintRefs.Contains(f.Ref))
            .ToList();

        if (targetFootprints.Count == 0) return;

        if (targetFootprints.Count == 1 || (!string.IsNullOrEmpty(_activeFootprintRef) && targetFootprints.Any(f => f.Ref.Equals(_activeFootprintRef, StringComparison.OrdinalIgnoreCase))))
        {
            var single = targetFootprints.FirstOrDefault(f => f.Ref.Equals(_activeFootprintRef, StringComparison.OrdinalIgnoreCase)) ?? targetFootprints[0];
            FlyTo(single.X, single.Y);
            return;
        }

        double minX = targetFootprints.Min(f => f.X);
        double maxX = targetFootprints.Max(f => f.X);
        double minY = targetFootprints.Min(f => f.Y);
        double maxY = targetFootprints.Max(f => f.Y);

        FlyToBounds(minX, minY, maxX, maxY);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        context.Custom(new PcbSkiaDrawOperation(bounds, CreateRenderState()));
    }

    public void RotateBy(double deltaDegrees, double? centerScreenX = null, double? centerScreenY = null)
    {
        if (Math.Abs(deltaDegrees) < 0.0001) return;
        StopAnimation();

        double cx = centerScreenX ?? (Bounds.Width / 2.0);
        double cy = centerScreenY ?? (Bounds.Height / 2.0);

        var (wx, wy) = ScreenToWorld(cx, cy);

        _rotation = ((_rotation + deltaDegrees) % 360.0 + 360.0) % 360.0;

        double effWx = _isFlipped ? -wx : wx;
        double x2 = effWx * _scale;
        double y2 = wy * _scale;
        double rad = _rotation * (Math.PI / 180.0);
        double rx = x2 * Math.Cos(rad) - y2 * Math.Sin(rad);
        double ry = x2 * Math.Sin(rad) + y2 * Math.Cos(rad);

        _panX = cx - rx;
        _panY = cy - ry;

        InvalidateVisual();
    }

    public void RotateClockwise90()
    {
        double target = (Math.Floor((_rotation + 0.5) / 90.0) + 1.0) * 90.0;
        RotateBy(target - _rotation);
    }

    public void RotateCounterClockwise90()
    {
        double target = (Math.Ceiling((_rotation - 0.5) / 90.0) - 1.0) * 90.0;
        RotateBy(target - _rotation);
    }

    public (double X, double Y) ScreenToWorld(double sx, double sy)
    {
        double dx = sx - _panX;
        double dy = sy - _panY;

        double x2, y2;
        if (Math.Abs(_rotation) > 0.001)
        {
            double rad = _rotation * (Math.PI / 180.0);
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);
            x2 = dx * cos + dy * sin;
            y2 = -dx * sin + dy * cos;
        }
        else
        {
            x2 = dx;
            y2 = dy;
        }

        double effWx = x2 / _scale;
        double wy = y2 / _scale;
        return (_isFlipped ? -effWx : effWx, wy);
    }

    public void SetActiveNet(int? netId)
    {
        _activeNetId = netId;
        if (netId is not null)
        {
            _highlightedFootprintRefs.Clear();
            _activeFootprintRef = null;
        }
        else
        {
            _selectedPad = null;
        }
        InvalidateVisual();
    }

    public void SetBoard(PcbBoard? board)
    {
        StopAnimation();
        _board = board;
        _rotation = 0;
        _activeNetId = null;
        _hoveredPad = null;
        _selectedPad = null;
        _highlightedFootprintRefs.Clear();
        _placedFootprintRefs.Clear();
        _activeFootprintRef = null;
        _spatialIndex.Build(board?.Pads);

        if (board is not null)
        {
            foreach (var layer in board.Layers)
            {
                if (layer.Name.EndsWith(".Cu", StringComparison.Ordinal))
                    _visibleLayers.Add(layer.Name);
            }
        }

        FitBoardToView();
        InvalidateVisual();
    }

    public void SetFlip(bool flipped)
    {
        if (_isFlipped == flipped) return;
        StopAnimation();
        _isFlipped = flipped;

        if (_board?.Bbox is { } bbox)
        {
            double width = Bounds.Width > 10 ? Bounds.Width : 900;
            double height = Bounds.Height > 10 ? Bounds.Height : 650;
            double effCx = _isFlipped ? -bbox.CenterX : bbox.CenterX;
            double x2 = effCx * _scale;
            double y2 = bbox.CenterY * _scale;
            double rad = _rotation * (Math.PI / 180.0);
            double rx = x2 * Math.Cos(rad) - y2 * Math.Sin(rad);
            double ry = x2 * Math.Sin(rad) + y2 * Math.Cos(rad);
            _panX = width / 2.0 - rx;
            _panY = height / 2.0 - ry;
        }

        InvalidateVisual();
    }

    public void SetHighlightedFootprints(IEnumerable<string>? refs, string? activeRef = null, bool flyTo = true)
    {
        _highlightedFootprintRefs.Clear();
        if (refs is not null)
        {
            foreach (var r in refs)
            {
                if (!string.IsNullOrWhiteSpace(r))
                    _highlightedFootprintRefs.Add(r);
            }
        }

        _activeFootprintRef = activeRef ?? _highlightedFootprintRefs.FirstOrDefault();
        if (_highlightedFootprintRefs.Count > 0)
        {
            _activeNetId = null;
            _selectedPad = null;
        }

        if (flyTo && _board is not null && _highlightedFootprintRefs.Count > 0)
        {
            FlyToHighlightedFootprints();
        }
        else
        {
            InvalidateVisual();
        }
    }

    public void SetPlacedFootprints(IEnumerable<string>? refs)
    {
        _placedFootprintRefs.Clear();
        if (refs is not null)
        {
            foreach (var r in refs)
            {
                if (!string.IsNullOrWhiteSpace(r))
                    _placedFootprintRefs.Add(r);
            }
        }
        InvalidateVisual();
    }

    public void SetLayerVisibility(string layerName, bool visible)
    {
        if (visible) _visibleLayers.Add(layerName);
        else _visibleLayers.Remove(layerName);
        InvalidateVisual();
    }

    public void SetSelectedPad(PcbPad? pad, bool flyToPad = true)
    {
        _selectedPad = pad;
        if (pad is not null && flyToPad)
        {
            FlyTo(pad.X, pad.Y);
        }
        else
        {
            InvalidateVisual();
        }
    }

    public void SetShowPadNetNames(bool show)
    {
        _showPadNetNames = show;
        InvalidateVisual();
    }

    public void SetZoneMode(ZoneDisplayMode mode)
    {
        _zoneMode = mode;
        InvalidateVisual();
    }

    public void ToggleFlip()
    {
        SetFlip(!_isFlipped);
    }

    public (double X, double Y) WorldToScreen(double worldX, double worldY)
    {
        double effX = _isFlipped ? -worldX : worldX;
        double x2 = effX * _scale;
        double y2 = worldY * _scale;

        if (Math.Abs(_rotation) > 0.001)
        {
            double rad = _rotation * (Math.PI / 180.0);
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);
            double rx = x2 * cos - y2 * sin;
            double ry = x2 * sin + y2 * cos;
            return (_panX + rx, _panY + ry);
        }

        return (_panX + x2, _panY + y2);
    }

    public void ZoomBy(double factor, double? centerScreenX = null, double? centerScreenY = null)
    {
        StopAnimation();
        double cx = centerScreenX ?? (Bounds.Width / 2.0);
        double cy = centerScreenY ?? (Bounds.Height / 2.0);

        var (wx, wy) = ScreenToWorld(cx, cy);
        _scale = Math.Clamp(_scale * factor, 0.2, 250.0);

        double effWx = _isFlipped ? -wx : wx;
        double x2 = effWx * _scale;
        double y2 = wy * _scale;
        double rad = _rotation * (Math.PI / 180.0);
        double rx = x2 * Math.Cos(rad) - y2 * Math.Sin(rad);
        double ry = x2 * Math.Sin(rad) + y2 * Math.Cos(rad);

        _panX = cx - rx;
        _panY = cy - ry;

        NotifyZoomChanged();
        InvalidateVisual();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _activePointers.Remove(e.Pointer.Id);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pt = e.GetPosition(this);

        // Update HUD coordinates
        var (wx, wy) = ScreenToWorld(pt.X, pt.Y);
        CursorCoordinatesChanged?.Invoke($"X: {wx:F2} mm, Y: {wy:F2} mm");

        if (_activePointers.ContainsKey(e.Pointer.Id))
        {
            var prevPt = _activePointers[e.Pointer.Id];
            _activePointers[e.Pointer.Id] = pt;

            if (_activePointers.Count == 1)
            {
                var props = e.GetCurrentPoint(this).Properties;
                bool isPressed = e.Pointer.Type != PointerType.Mouse
                    || props.IsLeftButtonPressed
                    || props.IsMiddleButtonPressed;

                if (isPressed)
                {
                    if (!_movedBeyondTapThreshold && GetDistance(_pointerDownPos, pt) > 6.0)
                    {
                        _movedBeyondTapThreshold = true;
                        if (_hoveredPad is not null)
                        {
                            _hoveredPad = null;
                            HoveredPadChanged?.Invoke(null, pt);
                        }
                    }

                    if (_movedBeyondTapThreshold)
                    {
                        _panX += pt.X - prevPt.X;
                        _panY += pt.Y - prevPt.Y;
                        InvalidateVisual();
                    }
                }
            }
            else if (_activePointers.Count >= 2)
            {
                var pts = _activePointers.Values.Take(2).ToArray();
                double dist = GetDistance(pts[0], pts[1]);
                Point mid = GetMidpoint(pts[0], pts[1]);
                double angle = GetAngleDegrees(pts[0], pts[1]);

                // Pan by midpoint delta
                _panX += mid.X - _lastPinchMidpoint.X;
                _panY += mid.Y - _lastPinchMidpoint.Y;

                // Zoom by pinch distance ratio
                if (_lastPinchDistance > 5.0 && dist > 5.0)
                {
                    double factor = dist / _lastPinchDistance;
                    ZoomBy(factor, mid.X, mid.Y);
                }

                // Rotate by angle delta around gesture midpoint
                double deltaAngle = angle - _lastPinchAngle;
                while (deltaAngle > 180.0) deltaAngle -= 360.0;
                while (deltaAngle < -180.0) deltaAngle += 360.0;

                if (Math.Abs(deltaAngle) > 0.08)
                {
                    RotateBy(deltaAngle, mid.X, mid.Y);
                }

                _lastPinchDistance = dist;
                _lastPinchMidpoint = mid;
                _lastPinchAngle = angle;
                InvalidateVisual();
            }
        }
        else if (e.Pointer.Type == PointerType.Mouse && _board is not null)
        {
            // Hover test when moving mouse without dragging
            var pad = _spatialIndex.FindPadAt(wx, wy, IsPadCurrentlyVisible, 0.15);
            if (!ReferenceEquals(pad, _hoveredPad))
            {
                _hoveredPad = pad;
                HoveredPadChanged?.Invoke(pad, pt);
                InvalidateVisual();
            }
            else if (pad is not null)
            {
                HoveredPadChanged?.Invoke(pad, pt);
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        StopAnimation();
        Focus();

        var pt = e.GetPosition(this);
        _lastPointerType = e.Pointer.Type;
        _activePointers[e.Pointer.Id] = pt;

        if (_activePointers.Count == 1)
        {
            _pointerDownPos = pt;
            _movedBeyondTapThreshold = false;
        }
        else if (_activePointers.Count == 2)
        {
            _movedBeyondTapThreshold = true;
            var pts = _activePointers.Values.ToArray();
            _lastPinchDistance = GetDistance(pts[0], pts[1]);
            _lastPinchMidpoint = GetMidpoint(pts[0], pts[1]);
            _lastPinchAngle = GetAngleDegrees(pts[0], pts[1]);
        }

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var pt = e.GetPosition(this);

        bool wasTap = _activePointers.Count == 1 && !_movedBeyondTapThreshold;
        _activePointers.Remove(e.Pointer.Id);
        e.Pointer.Capture(null);

        if (_activePointers.Count == 0 && wasTap && _board is not null)
        {
            var (wx, wy) = ScreenToWorld(pt.X, pt.Y);
            double margin = _lastPointerType == PointerType.Touch ? 0.45 : 0.15;
            var clickedPad = _spatialIndex.FindPadAt(wx, wy, IsPadCurrentlyVisible, margin);
            PadClicked?.Invoke(clickedPad);
        }
        else if (_activePointers.Count == 2)
        {
            var pts = _activePointers.Values.ToArray();
            _lastPinchDistance = GetDistance(pts[0], pts[1]);
            _lastPinchMidpoint = GetMidpoint(pts[0], pts[1]);
            _lastPinchAngle = GetAngleDegrees(pts[0], pts[1]);
        }

        e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var pt = e.GetPosition(this);
        double factor = e.Delta.Y > 0 ? 1.18 : (1.0 / 1.18);
        ZoomBy(factor, pt.X, pt.Y);
        e.Handled = true;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (e.PreviousSize.Width <= 1 && e.NewSize.Width > 1 && _board is not null)
        {
            FitBoardToView();
        }
        else
        {
            InvalidateVisual();
        }
    }

    private static double GetAngleDegrees(Point a, Point b)
    {
        return Math.Atan2(b.Y - a.Y, b.X - a.X) * (180.0 / Math.PI);
    }

    private static double GetDistance(Point a, Point b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static Point GetMidpoint(Point a, Point b)
    {
        return new Point((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);
    }

    private PcbRenderState CreateRenderState()
    {
        return new PcbRenderState
        {
            Board = _board,
            Scale = _scale,
            PanX = _panX,
            PanY = _panY,
            Rotation = _rotation,
            IsFlipped = _isFlipped,
            ActiveNetId = _activeNetId,
            HoveredPad = _hoveredPad,
            SelectedPad = _selectedPad,
            HighlightedFootprintRefs = new HashSet<string>(_highlightedFootprintRefs, StringComparer.OrdinalIgnoreCase),
            PlacedFootprintRefs = new HashSet<string>(_placedFootprintRefs, StringComparer.OrdinalIgnoreCase),
            ActiveFootprintRef = _activeFootprintRef,
            VisibleLayers = new HashSet<string>(_visibleLayers, StringComparer.Ordinal),
            ZoneMode = _zoneMode,
            ShowPadNetNames = _showPadNetNames
        };
    }

    private bool IsPadCurrentlyVisible(PcbPad pad)
    {
        if (pad.Layers.Count == 0) return true;
        foreach (string l in pad.Layers)
        {
            if (_visibleLayers.Contains(l)) return true;
            if (l is "*.Cu" or "*")
            {
                foreach (string vl in _visibleLayers)
                {
                    if (vl.EndsWith(".Cu", StringComparison.Ordinal)) return true;
                }
            }
            if (l == "*.Mask")
            {
                if (_visibleLayers.Contains("F.Mask") || _visibleLayers.Contains("B.Mask")) return true;
            }
        }
        return false;
    }

    private void NotifyZoomChanged()
    {
        ZoomPercentageChanged?.Invoke((int)Math.Round(_scale * 10.0));
    }

    private void OnFlyTick(object? sender, EventArgs e)
    {
        double elapsed = _flyStopwatch.Elapsed.TotalMilliseconds;
        double t = Math.Clamp(elapsed / _flyDurationMs, 0.0, 1.0);
        double ease = 1.0 - Math.Pow(1.0 - t, 3.0); // Cubic ease-out

        _scale = _flyStartScale + (_flyEndScale - _flyStartScale) * ease;
        _panX = _flyStartPanX + (_flyEndPanX - _flyStartPanX) * ease;
        _panY = _flyStartPanY + (_flyEndPanY - _flyStartPanY) * ease;

        NotifyZoomChanged();
        InvalidateVisual();

        if (t >= 1.0)
        {
            StopAnimation();
        }
    }

    private void StopAnimation()
    {
        if (_flyTimer is not null)
        {
            _flyTimer.Stop();
            _flyTimer = null;
        }
        _flyStopwatch.Stop();
    }
}