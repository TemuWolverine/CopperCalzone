using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using CopperCalzone.Models.Schematic;

namespace CopperCalzone.Rendering.Schematic;

public sealed class SchCanvasControl : Control
{
    private readonly Stopwatch _flyStopwatch = new();
    private readonly HashSet<string> _highlightedSymbolRefs = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _placedSymbolRefs = new(StringComparer.OrdinalIgnoreCase);
    private string? _activeSymbolRef;

    private KiCadSchematic? _schematic;
    private double _scale = 6.0;
    private double _panX, _panY;

    // Multi-touch & pointer tracking
    private readonly Dictionary<int, Point> _activePointers = [];
    private double _lastPinchDistance;
    private Point _lastPinchMidpoint;
    private Point _pointerDownPos;
    private bool _movedBeyondTapThreshold;

    // Smooth FlyTo animation
    private double _flyStartPanX, _flyEndPanX;
    private double _flyStartPanY, _flyEndPanY;
    private double _flyStartScale, _flyEndScale;
    private double _flyDurationMs = 380;
    private DispatcherTimer? _flyTimer;

    public event Action<SchSymbol?>? SymbolClicked;
    public event Action<string>? CursorCoordinatesChanged;
    public event Action<int>? ZoomPercentageChanged;

    public KiCadSchematic? Schematic => _schematic;
    public double Scale => _scale;

    static SchCanvasControl()
    {
        AffectsRender<SchCanvasControl>(BoundsProperty);
    }

    public SchCanvasControl()
    {
        ClipToBounds = true;
    }

    public void SetSchematic(KiCadSchematic? sch)
    {
        StopAnimation();
        _schematic = sch;
        _highlightedSymbolRefs.Clear();
        _placedSymbolRefs.Clear();
        _activeSymbolRef = null;

        FitSchematicToView();
        InvalidateVisual();
    }

    public void SetHighlightedSymbols(IEnumerable<string>? refs, string? activeRef = null, bool flyTo = true)
    {
        _highlightedSymbolRefs.Clear();
        if (refs is not null)
        {
            foreach (var r in refs)
            {
                if (!string.IsNullOrWhiteSpace(r))
                    _highlightedSymbolRefs.Add(r);
            }
        }
        _activeSymbolRef = activeRef ?? _highlightedSymbolRefs.FirstOrDefault();

        if (flyTo && _schematic is not null && !string.IsNullOrEmpty(_activeSymbolRef))
        {
            FlyToSymbol(_activeSymbolRef);
        }
        else
        {
            InvalidateVisual();
        }
    }

    public void SetPlacedSymbols(IEnumerable<string>? refs)
    {
        _placedSymbolRefs.Clear();
        if (refs is not null)
        {
            foreach (var r in refs)
            {
                if (!string.IsNullOrWhiteSpace(r))
                    _placedSymbolRefs.Add(r);
            }
        }
        InvalidateVisual();
    }

    public void ZoomBy(double factor, Point? center = null)
    {
        StopAnimation();
        Point focal = center ?? new Point(Bounds.Width / 2.0, Bounds.Height / 2.0);
        double oldScale = _scale;
        double newScale = Math.Clamp(_scale * factor, 0.2, 80.0);
        if (Math.Abs(oldScale - newScale) < 0.001) return;

        double worldX = (focal.X - _panX) / oldScale;
        double worldY = (focal.Y - _panY) / oldScale;

        _scale = newScale;
        _panX = focal.X - worldX * _scale;
        _panY = focal.Y - worldY * _scale;

        ZoomPercentageChanged?.Invoke((int)Math.Round(_scale * 100.0 / 6.0));
        InvalidateVisual();
    }

    public void FitSchematicToView()
    {
        if (_schematic is null) return;
        StopAnimation();

        double w = Bounds.Width > 10 ? Bounds.Width : 1000;
        double h = Bounds.Height > 10 ? Bounds.Height : 700;

        double schW = Math.Max(10, _schematic.Width);
        double schH = Math.Max(10, _schematic.Height);

        double margin = 40;
        double sx = (w - margin * 2) / schW;
        double sy = (h - margin * 2) / schH;
        _scale = Math.Clamp(Math.Min(sx, sy), 0.2, 40.0);

        _panX = w / 2.0 - _schematic.CenterX * _scale;
        _panY = h / 2.0 - _schematic.CenterY * _scale;

        ZoomPercentageChanged?.Invoke((int)Math.Round(_scale * 100.0 / 6.0));
        InvalidateVisual();
    }

    public void FlyTo(double worldX, double worldY, double? targetScale = null)
    {
        StopAnimation();

        double w = Bounds.Width > 10 ? Bounds.Width : 1000;
        double h = Bounds.Height > 10 ? Bounds.Height : 700;

        _flyStartScale = _scale;
        _flyEndScale = targetScale ?? Math.Clamp(_scale, 4.0, 15.0);

        _flyStartPanX = _panX;
        _flyStartPanY = _panY;

        _flyEndPanX = w / 2.0 - worldX * _flyEndScale;
        _flyEndPanY = h / 2.0 - worldY * _flyEndScale;

        _flyStopwatch.Restart();
        _flyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _flyTimer.Tick += OnFlyTick;
        _flyTimer.Start();
    }

    public void FlyToSymbol(string reference)
    {
        if (_schematic is null) return;
        var sym = _schematic.Symbols.FirstOrDefault(s => s.Reference.Equals(reference, StringComparison.OrdinalIgnoreCase));
        if (sym is not null)
        {
            FlyTo((sym.MinX + sym.MaxX) / 2.0, (sym.MinY + sym.MaxY) / 2.0);
        }
    }

    private void OnFlyTick(object? sender, EventArgs e)
    {
        double t = _flyStopwatch.Elapsed.TotalMilliseconds / _flyDurationMs;
        if (t >= 1.0)
        {
            _panX = _flyEndPanX;
            _panY = _flyEndPanY;
            _scale = _flyEndScale;
            StopAnimation();
            ZoomPercentageChanged?.Invoke((int)Math.Round(_scale * 100.0 / 6.0));
            InvalidateVisual();
            return;
        }

        // Smooth cubic ease-out
        double factor = 1.0 - Math.Pow(1.0 - t, 3.0);
        _panX = _flyStartPanX + (_flyEndPanX - _flyStartPanX) * factor;
        _panY = _flyStartPanY + (_flyEndPanY - _flyStartPanY) * factor;
        _scale = _flyStartScale + (_flyEndScale - _flyStartScale) * factor;

        ZoomPercentageChanged?.Invoke((int)Math.Round(_scale * 100.0 / 6.0));
        InvalidateVisual();
    }

    private void StopAnimation()
    {
        if (_flyTimer is not null)
        {
            _flyTimer.Stop();
            _flyTimer.Tick -= OnFlyTick;
            _flyTimer = null;
        }
        _flyStopwatch.Stop();
    }

    public (double X, double Y) ScreenToWorld(double sx, double sy)
    {
        return ((sx - _panX) / _scale, (sy - _panY) / _scale);
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

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _activePointers.Remove(e.Pointer.Id);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        double delta = e.Delta.Y;
        double factor = delta > 0 ? 1.2 : (1.0 / 1.2);
        ZoomBy(factor, e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        StopAnimation();
        Focus();

        var pt = e.GetPosition(this);
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
        }

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pt = e.GetPosition(this);

        // Update coordinate HUD
        var (wx, wy) = ScreenToWorld(pt.X, pt.Y);
        CursorCoordinatesChanged?.Invoke($"X: {wx:F2} mm  Y: {wy:F2} mm");

        if (_activePointers.ContainsKey(e.Pointer.Id))
        {
            var prevPt = _activePointers[e.Pointer.Id];
            _activePointers[e.Pointer.Id] = pt;

            if (_activePointers.Count == 1)
            {
                var props = e.GetCurrentPoint(this).Properties;
                bool isPressed = e.Pointer.Type != PointerType.Mouse
                    || props.IsLeftButtonPressed
                    || props.IsMiddleButtonPressed
                    || props.IsRightButtonPressed;

                if (isPressed)
                {
                    if (!_movedBeyondTapThreshold && GetDistance(_pointerDownPos, pt) > 6.0)
                    {
                        _movedBeyondTapThreshold = true;
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

                // Pan by midpoint delta
                _panX += mid.X - _lastPinchMidpoint.X;
                _panY += mid.Y - _lastPinchMidpoint.Y;

                // Zoom by pinch distance ratio (rotation not needed)
                if (_lastPinchDistance > 5.0 && dist > 5.0)
                {
                    double factor = dist / _lastPinchDistance;
                    ZoomBy(factor, mid);
                }

                _lastPinchDistance = dist;
                _lastPinchMidpoint = mid;
                InvalidateVisual();
            }
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var pt = e.GetPosition(this);

        bool wasTap = _activePointers.Count == 1 && !_movedBeyondTapThreshold;
        _activePointers.Remove(e.Pointer.Id);
        e.Pointer.Capture(null);

        if (_activePointers.Count == 0 && wasTap)
        {
            var (wx, wy) = ScreenToWorld(pt.X, pt.Y);
            var clicked = HitTestSymbol(wx, wy);
            SymbolClicked?.Invoke(clicked);
        }
        else if (_activePointers.Count == 2)
        {
            var pts = _activePointers.Values.ToArray();
            _lastPinchDistance = GetDistance(pts[0], pts[1]);
            _lastPinchMidpoint = GetMidpoint(pts[0], pts[1]);
        }

        e.Handled = true;
    }

    private SchSymbol? HitTestSymbol(double wx, double wy)
    {
        if (_schematic is null) return null;
        foreach (var sym in _schematic.Symbols)
        {
            if (wx >= sym.MinX && wx <= sym.MaxX && wy >= sym.MinY && wy <= sym.MaxY)
            {
                return sym;
            }
        }
        return null;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var state = new SchRenderState
        {
            Schematic = _schematic,
            Scale = _scale,
            PanX = _panX,
            PanY = _panY,
            HighlightedSymbolRefs = new HashSet<string>(_highlightedSymbolRefs, StringComparer.OrdinalIgnoreCase),
            PlacedSymbolRefs = new HashSet<string>(_placedSymbolRefs, StringComparer.OrdinalIgnoreCase),
            ActiveSymbolRef = _activeSymbolRef
        };

        context.Custom(new SchSkiaDrawOperation(Bounds, state));
    }
}

