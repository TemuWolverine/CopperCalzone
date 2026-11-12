using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using CopperCalzone.Models.Schematic;
using SkiaSharp;

namespace CopperCalzone.Rendering.Schematic;

public sealed class SchSkiaDrawOperation : ICustomDrawOperation
{
    private static readonly SKColor ActiveGlow = new(0x00, 0xE5, 0xFF);

    private static readonly SKColor BusColor = new(0x38, 0xBD, 0xF8);

    private static readonly SKColor LabelColor = new(0xF4, 0x72, 0xB6);

    private static readonly SKTypeface MonoBoldTypeface =
                    SKTypeface.FromFamilyName("Consolas", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        ?? SKTypeface.Default;

    private static readonly SKColor PinNameColor = new(0xC0, 0x84, 0xFC);

    // Lavender
    private static readonly SKColor PinNumColor = new(0xFB, 0x92, 0x3C);

    private static readonly SKColor PlacedGlow = new(0x22, 0xC5, 0x5E);

    // Neon Cyan
    private static readonly SKColor PrimaryGlow = new(0xFF, 0xB1, 0x42);

    private static readonly SKTypeface SansSemiBoldTypeface =
                        SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        ?? SKTypeface.Default;

    // Sky Blue
    private static readonly SKColor SymbolColor = new(0xFB, 0xBF, 0x24);

    // Warm Amber/Yellow for symbol lines
    private static readonly SKColor TextRefColor = new(0x67, 0xE8, 0xF9);

    // Cyan
    private static readonly SKColor TextValColor = new(0xF1, 0xF5, 0xF9);

    // Palette
    private static readonly SKColor WireColor = new(0x4A, 0xDE, 0x80); // Bright Green

    // Soft White
    // Orange
    // Pink / Rose

    // Golden Amber
    // Green

    private readonly Rect _bounds;
    private readonly SchRenderState _state;

    public Rect Bounds => _bounds;

    public SchSkiaDrawOperation(Rect bounds, SchRenderState state)
    {
        _bounds = bounds;
        _state = state;
    }

    public void Dispose()
    {
    }

    public bool Equals(ICustomDrawOperation? other) => false;

    public bool HitTest(Point p) => _bounds.Contains(p);

    public void Render(ImmediateDrawingContext context)
    {
        var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (leaseFeature is null) return;

        using var lease = leaseFeature.Lease();
        RenderToCanvas(lease.SkCanvas);
    }

    private static void DrawPropertyText(SKCanvas canvas, string text, float x, float y, double rotDeg, SKPaint paint)
    {
        int rot = ((int)Math.Round(rotDeg) % 360 + 360) % 360;
        bool isVertical = rot is 90 or 270;

        if (!isVertical)
        {
            canvas.DrawText(text, x, y, paint);
        }
        else
        {
            canvas.Save();
            canvas.Translate(x, y);
            canvas.RotateDegrees(-90); // Read bottom-to-top
            canvas.DrawText(text, 0, 0, paint);
            canvas.Restore();
        }
    }

    private void DrawBuses(SKCanvas canvas, KiCadSchematic sch)
    {
        if (sch.Buses.Count == 0) return;

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            Color = BusColor,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeWidth = (float)Math.Max(0.45, 2.8 / _state.Scale)
        };

        using var path = new SKPath();
        foreach (var b in sch.Buses)
        {
            if (b.Points.Count < 2) continue;
            path.MoveTo((float)b.Points[0].X, (float)b.Points[0].Y);
            for (int i = 1; i < b.Points.Count; i++)
            {
                path.LineTo((float)b.Points[i].X, (float)b.Points[i].Y);
            }
        }
        canvas.DrawPath(path, paint);
    }

    private void DrawGrid(SKCanvas canvas)
    {
        // Schematic grid in KiCad is typically 1.27mm (50mil) or 2.54mm (100mil)
        double gridStep = 2.54;
        if (_state.Scale < 1.5) return; // Don't draw dense grid when zoomed far out

        double left = -_state.PanX / _state.Scale;
        double top = -_state.PanY / _state.Scale;
        double right = left + _bounds.Width / _state.Scale;
        double bottom = top + _bounds.Height / _state.Scale;

        int startX = (int)Math.Floor(left / gridStep);
        int endX = (int)Math.Ceiling(right / gridStep);
        int startY = (int)Math.Floor(top / gridStep);
        int endY = (int)Math.Ceiling(bottom / gridStep);

        using var dotPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = new SKColor(0x28, 0x30, 0x46, 160)
        };

        float dotRadius = (float)Math.Max(0.04, 0.6 / _state.Scale);
        for (int x = startX; x <= endX; x++)
        {
            float px = (float)(x * gridStep);
            for (int y = startY; y <= endY; y++)
            {
                float py = (float)(y * gridStep);
                canvas.DrawCircle(px, py, dotRadius, dotPaint);
            }
        }
    }

    private void DrawHighlights(SKCanvas canvas, KiCadSchematic sch, SKMatrix screenMatrix, SKMatrix worldMatrix)
    {
        var highlighted = _state.HighlightedSymbolRefs;
        var placed = _state.PlacedSymbolRefs;
        if (highlighted.Count == 0 && placed.Count == 0) return;

        using var reticlePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };

        using var fillGlowPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        // Render in World Space
        foreach (var sym in sch.Symbols)
        {
            bool isHigh = highlighted.Contains(sym.Reference);
            bool isPlac = placed.Contains(sym.Reference);
            if (!isHigh && !isPlac) continue;

            bool isActive = isHigh && string.Equals(sym.Reference, _state.ActiveSymbolRef, StringComparison.OrdinalIgnoreCase);

            SKColor glowColor;
            if (isPlac && !isHigh) glowColor = PlacedGlow;
            else if (isActive) glowColor = ActiveGlow;
            else if (isHigh) glowColor = PrimaryGlow;
            else glowColor = PlacedGlow;

            float pad = 0.8f;
            var rect = new SKRect((float)sym.MinX - pad, (float)sym.MinY - pad, (float)sym.MaxX + pad, (float)sym.MaxY + pad);

            fillGlowPaint.Color = glowColor.WithAlpha(isActive ? (byte)55 : (byte)28);
            canvas.DrawRoundRect(rect, 0.8f, 0.8f, fillGlowPaint);

            reticlePaint.Color = glowColor;
            reticlePaint.StrokeWidth = (float)Math.Max(0.2, (isActive ? 2.8 : 1.8) / _state.Scale);
            canvas.DrawRoundRect(rect, 0.8f, 0.8f, reticlePaint);
        }

        // Render Badges in Screen Space for highlighted symbols
        if (highlighted.Count > 0)
        {
            canvas.SetMatrix(screenMatrix);
            foreach (var sym in sch.Symbols)
            {
                if (!highlighted.Contains(sym.Reference)) continue;

                bool isActive = string.Equals(sym.Reference, _state.ActiveSymbolRef, StringComparison.OrdinalIgnoreCase);
                bool isPlac = placed.Contains(sym.Reference);
                SKColor compColor = isActive ? ActiveGlow : (isPlac ? PlacedGlow : PrimaryGlow);

                var (sx, sy) = WorldToScreen((sym.MinX + sym.MaxX) / 2.0, sym.MinY);
                if (sx < -100 || sx > _bounds.Width + 100 || sy < -100 || sy > _bounds.Height + 100)
                    continue;

                string labelText = isPlac ? $"✓  {sym.Reference}  {sym.Value}" : $"{sym.Reference}  {sym.Value}";

                float fontSize = 12f;
                using var fontPaint = new SKPaint
                {
                    IsAntialias = true,
                    Typeface = SansSemiBoldTypeface,
                    TextSize = fontSize
                };

                float textW = fontPaint.MeasureText(labelText);
                float pillW = textW + 16f;
                float pillH = 22f;
                var pillRect = new SKRect((float)sx - pillW / 2f, (float)sy - pillH - 8f, (float)sx + pillW / 2f, (float)sy - 8f);

                using var pillBgPaint = new SKPaint
                {
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill,
                    Color = new SKColor(0x12, 0x16, 0x22, 0xF0)
                };
                canvas.DrawRoundRect(pillRect, 6f, 6f, pillBgPaint);

                using var pillBorderPaint = new SKPaint
                {
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = isActive ? 2.0f : 1.2f,
                    Color = compColor
                };
                canvas.DrawRoundRect(pillRect, 6f, 6f, pillBorderPaint);

                using var textPaint = new SKPaint
                {
                    IsAntialias = true,
                    Typeface = SansSemiBoldTypeface,
                    TextSize = fontSize,
                    Color = SKColors.White
                };
                canvas.DrawText(labelText, (float)sx - textW / 2f, (float)sy - 8f - pillH / 2f + fontSize * 0.35f, textPaint);
            }
            canvas.SetMatrix(worldMatrix);
        }
    }

    private void DrawJunctions(SKCanvas canvas, KiCadSchematic sch)
    {
        if (sch.Junctions.Count == 0) return;

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = WireColor
        };

        foreach (var j in sch.Junctions)
        {
            float radius = (float)(j.Diameter / 2.0);
            if (radius < 0.35f) radius = 0.45f;
            canvas.DrawCircle((float)j.X, (float)j.Y, radius, paint);
        }
    }

    private void DrawLabels(SKCanvas canvas, KiCadSchematic sch)
    {
        if (sch.Labels.Count == 0) return;

        using var strokePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            Color = LabelColor,
            StrokeWidth = (float)Math.Max(0.18, 1.2 / _state.Scale)
        };

        using var textPaint = new SKPaint
        {
            IsAntialias = true,
            Typeface = SansSemiBoldTypeface,
            Color = LabelColor
        };

        foreach (var lbl in sch.Labels)
        {
            float sz = (float)Math.Max(0.8, lbl.Size);
            textPaint.TextSize = sz;

            int rotNorm = ((int)Math.Round(lbl.Rotation) % 360 + 360) % 360;
            bool isUpsideDownAngle = rotNorm > 90 && rotNorm <= 270;
            float drawAngle = isUpsideDownAngle ? -(rotNorm - 180) : -rotNorm;
            bool extendLeft = isUpsideDownAngle;

            canvas.Save();
            canvas.Translate((float)lbl.X, (float)lbl.Y);
            if (Math.Abs(drawAngle) > 0.001f)
            {
                canvas.RotateDegrees(drawAngle);
            }

            // Draw directional flag or underline
            float textW = textPaint.MeasureText(lbl.Text);
            float h = sz * 1.2f;

            if (lbl.Kind is "global" or "hierarchical")
            {
                using var flagPath = new SKPath();
                if (!extendLeft)
                {
                    flagPath.MoveTo(0, 0);
                    flagPath.LineTo(h * 0.4f, -h / 2f);
                    flagPath.LineTo(textW + h * 0.8f, -h / 2f);
                    flagPath.LineTo(textW + h * 0.8f, h / 2f);
                    flagPath.LineTo(h * 0.4f, h / 2f);
                    flagPath.Close();
                    canvas.DrawPath(flagPath, strokePaint);
                    canvas.DrawText(lbl.Text, h * 0.5f, sz * 0.35f, textPaint);
                }
                else
                {
                    flagPath.MoveTo(0, 0);
                    flagPath.LineTo(-h * 0.4f, -h / 2f);
                    flagPath.LineTo(-(textW + h * 0.8f), -h / 2f);
                    flagPath.LineTo(-(textW + h * 0.8f), h / 2f);
                    flagPath.LineTo(-h * 0.4f, h / 2f);
                    flagPath.Close();
                    canvas.DrawPath(flagPath, strokePaint);
                    canvas.DrawText(lbl.Text, -(textW + h * 0.4f), sz * 0.35f, textPaint);
                }
            }
            else
            {
                // Local net label
                if (!extendLeft)
                {
                    canvas.DrawLine(0, 0, textW + 0.5f, 0, strokePaint);
                    canvas.DrawText(lbl.Text, 0.3f, -0.4f, textPaint);
                }
                else
                {
                    canvas.DrawLine(0, 0, -(textW + 0.5f), 0, strokePaint);
                    canvas.DrawText(lbl.Text, -(textW + 0.2f), -0.4f, textPaint);
                }
            }

            canvas.Restore();
        }
    }

    private void DrawNoConnects(SKCanvas canvas, KiCadSchematic sch)
    {
        if (sch.NoConnects.Count == 0) return;

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xEF, 0x44, 0x44), // Vibrant Red
            StrokeWidth = (float)Math.Max(0.18, 1.2 / _state.Scale)
        };

        float s = 0.5f;
        foreach (var nc in sch.NoConnects)
        {
            float cx = (float)nc.X;
            float cy = (float)nc.Y;
            canvas.DrawLine(cx - s, cy - s, cx + s, cy + s, paint);
            canvas.DrawLine(cx - s, cy + s, cx + s, cy - s, paint);
        }
    }

    private void DrawSheets(SKCanvas canvas, KiCadSchematic sch)
    {
        if (sch.Sheets.Count == 0) return;

        using var strokePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            Color = new SKColor(0xFB, 0x92, 0x3C),
            StrokeWidth = (float)Math.Max(0.2, 1.5 / _state.Scale)
        };

        using var fillPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = new SKColor(0x45, 0x1A, 0x03, 70)
        };

        using var textPaint = new SKPaint
        {
            IsAntialias = true,
            Typeface = SansSemiBoldTypeface,
            TextSize = 2.0f,
            Color = strokePaint.Color
        };

        foreach (var s in sch.Sheets)
        {
            var rect = new SKRect((float)s.X, (float)s.Y, (float)(s.X + s.Width), (float)(s.Y + s.Height));
            canvas.DrawRoundRect(rect, 1.0f, 1.0f, fillPaint);
            canvas.DrawRoundRect(rect, 1.0f, 1.0f, strokePaint);

            canvas.DrawText($"Sheet: {s.Name}", (float)s.X + 1.5f, (float)s.Y + 3.0f, textPaint);
            if (!string.IsNullOrWhiteSpace(s.FilePath))
            {
                textPaint.TextSize = 1.4f;
                canvas.DrawText(s.FilePath, (float)s.X + 1.5f, (float)s.Y + 5.5f, textPaint);
                textPaint.TextSize = 2.0f;
            }
        }
    }

    private void DrawSymbols(SKCanvas canvas, KiCadSchematic sch)
    {
        using var strokePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            Color = SymbolColor,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeWidth = (float)Math.Max(0.18, 1.4 / _state.Scale)
        };

        using var fillPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = SymbolColor.WithAlpha(40)
        };

        using var refTextPaint = new SKPaint
        {
            IsAntialias = true,
            Typeface = MonoBoldTypeface,
            Color = TextRefColor
        };

        using var valTextPaint = new SKPaint
        {
            IsAntialias = true,
            Typeface = SansSemiBoldTypeface,
            Color = TextValColor
        };

        using var pinNamePaint = new SKPaint
        {
            IsAntialias = true,
            Typeface = SansSemiBoldTypeface,
            Color = PinNameColor
        };

        using var pinNumPaint = new SKPaint
        {
            IsAntialias = true,
            Typeface = MonoBoldTypeface,
            Color = PinNumColor
        };

        foreach (var sym in sch.Symbols)
        {
            // 1. Draw transformed primitives
            foreach (var prim in sym.WorldPrimitives)
            {
                if (prim is SchPolyline pl && pl.Points.Count > 1)
                {
                    using var pPath = new SKPath();
                    pPath.MoveTo((float)pl.Points[0].X, (float)pl.Points[0].Y);
                    for (int i = 1; i < pl.Points.Count; i++)
                    {
                        pPath.LineTo((float)pl.Points[i].X, (float)pl.Points[i].Y);
                    }
                    if (pl.FillType is "outline" or "background")
                    {
                        canvas.DrawPath(pPath, fillPaint);
                    }
                    canvas.DrawPath(pPath, strokePaint);
                }
                else if (prim is SchRectangle r)
                {
                    var rRect = new SKRect((float)Math.Min(r.StartX, r.EndX), (float)Math.Min(r.StartY, r.EndY),
                                           (float)Math.Max(r.StartX, r.EndX), (float)Math.Max(r.StartY, r.EndY));
                    if (r.FillType is "outline" or "background")
                    {
                        canvas.DrawRect(rRect, fillPaint);
                    }
                    canvas.DrawRect(rRect, strokePaint);
                }
                else if (prim is SchCircle c)
                {
                    if (c.FillType is "outline" or "background")
                    {
                        canvas.DrawCircle((float)c.CenterX, (float)c.CenterY, (float)c.Radius, fillPaint);
                    }
                    canvas.DrawCircle((float)c.CenterX, (float)c.CenterY, (float)c.Radius, strokePaint);
                }
                else if (prim is SchArc a)
                {
                    // Draw arc as polyline approximation via mid point
                    using var aPath = new SKPath();
                    aPath.MoveTo((float)a.StartX, (float)a.StartY);
                    aPath.QuadTo((float)a.MidX, (float)a.MidY, (float)a.EndX, (float)a.EndY);
                    canvas.DrawPath(aPath, strokePaint);
                }
            }

            // 2. Draw Pins
            foreach (var pin in sym.WorldPins)
            {
                canvas.DrawLine((float)pin.X, (float)pin.Y, (float)pin.TargetX, (float)pin.TargetY, strokePaint);

                // Small circle at connection point
                float connR = (float)Math.Max(0.08, 0.7 / _state.Scale);
                canvas.DrawCircle((float)pin.X, (float)pin.Y, connR, strokePaint);

                // Pin Number & Name text if zoomed in enough
                if (_state.Scale > 1.2)
                {
                    pinNumPaint.TextSize = 0.9f;
                    pinNamePaint.TextSize = 1.0f;

                    bool isHorizontal = Math.Abs(pin.DirX) >= Math.Abs(pin.DirY);
                    float midX = (float)(pin.X + pin.TargetX) / 2f;
                    float midY = (float)(pin.Y + pin.TargetY) / 2f;

                    if (!pin.HideNumber && !string.IsNullOrWhiteSpace(pin.Number))
                    {
                        float numW = pinNumPaint.MeasureText(pin.Number);
                        if (isHorizontal)
                        {
                            canvas.DrawText(pin.Number, midX - numW / 2f, (float)pin.Y - 0.35f, pinNumPaint);
                        }
                        else
                        {
                            canvas.DrawText(pin.Number, midX + 0.35f, midY + 0.35f, pinNumPaint);
                        }
                    }

                    if (!pin.HideName && !string.IsNullOrWhiteSpace(pin.Name))
                    {
                        float nameW = pinNamePaint.MeasureText(pin.Name);
                        if (isHorizontal)
                        {
                            if (pin.DirX >= 0)
                            {
                                // Points Right into body: starts just inside body
                                canvas.DrawText(pin.Name, (float)pin.TargetX + 0.35f, (float)pin.TargetY + 0.35f, pinNamePaint);
                            }
                            else
                            {
                                // Points Left into body: ends just inside body (right-aligned)
                                canvas.DrawText(pin.Name, (float)pin.TargetX - 0.35f - nameW, (float)pin.TargetY + 0.35f, pinNamePaint);
                            }
                        }
                        else
                        {
                            if (pin.DirY >= 0)
                            {
                                // Points Down into body: below target
                                canvas.DrawText(pin.Name, (float)pin.TargetX - nameW / 2f, (float)pin.TargetY + 1.2f, pinNamePaint);
                            }
                            else
                            {
                                // Points Up into body: above target
                                canvas.DrawText(pin.Name, (float)pin.TargetX - nameW / 2f, (float)pin.TargetY - 0.5f, pinNamePaint);
                            }
                        }
                    }
                }
            }

            // 3. Draw Reference & Value Text
            if (!sym.RefHide && !string.IsNullOrWhiteSpace(sym.Reference))
            {
                refTextPaint.TextSize = 1.4f;
                DrawPropertyText(canvas, sym.Reference, (float)sym.RefX, (float)sym.RefY, sym.RefRotation, refTextPaint);
            }
            if (!sym.ValHide && !string.IsNullOrWhiteSpace(sym.Value))
            {
                valTextPaint.TextSize = 1.2f;
                DrawPropertyText(canvas, sym.Value, (float)sym.ValX, (float)sym.ValY, sym.ValRotation, valTextPaint);
            }
        }
    }

    private void DrawWires(SKCanvas canvas, KiCadSchematic sch)
    {
        if (sch.Wires.Count == 0) return;

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            Color = WireColor,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeWidth = (float)Math.Max(0.18, 1.4 / _state.Scale)
        };

        using var path = new SKPath();
        foreach (var w in sch.Wires)
        {
            if (w.Points.Count < 2) continue;
            path.MoveTo((float)w.Points[0].X, (float)w.Points[0].Y);
            for (int i = 1; i < w.Points.Count; i++)
            {
                path.LineTo((float)w.Points[i].X, (float)w.Points[i].Y);
            }
        }
        canvas.DrawPath(path, paint);
    }

    private void RenderToCanvas(SKCanvas canvas)
    {
        SKMatrix screenMatrix = canvas.TotalMatrix;

        canvas.Save();
        canvas.ClipRect(new SKRect(0, 0, (float)_bounds.Width, (float)_bounds.Height));

        SKColor bgColor = new SKColor(0x0F, 0x13, 0x1D);
        canvas.DrawColor(bgColor);

        var sch = _state.Schematic;
        if (sch is null)
        {
            canvas.Restore();
            return;
        }

        // Apply World Transform
        canvas.Translate((float)_state.PanX, (float)_state.PanY);
        canvas.Scale((float)_state.Scale, (float)_state.Scale);
        SKMatrix worldMatrix = canvas.TotalMatrix;

        // 1. Draw Grid
        DrawGrid(canvas);

        // 2. Draw Sheets
        DrawSheets(canvas, sch);

        // 3. Draw Buses
        DrawBuses(canvas, sch);

        // 4. Draw Wires
        DrawWires(canvas, sch);

        // 5. Draw Junctions
        DrawJunctions(canvas, sch);

        // 6. Draw No-Connects
        DrawNoConnects(canvas, sch);

        // 7. Draw Labels
        DrawLabels(canvas, sch);

        // 8. Draw Symbols
        DrawSymbols(canvas, sch);

        // 9. Draw Highlights & Reticles
        DrawHighlights(canvas, sch, screenMatrix, worldMatrix);

        canvas.Restore();
    }

    private (double ScreenX, double ScreenY) WorldToScreen(double worldX, double worldY)
    {
        double x2 = worldX * _state.Scale;
        double y2 = worldY * _state.Scale;
        return (x2 + _state.PanX, y2 + _state.PanY);
    }
}
