using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using CopperCalzone.Models;
using SkiaSharp;

namespace CopperCalzone.Rendering;

public sealed class PcbSkiaDrawOperation : ICustomDrawOperation
{
    private readonly Rect _bounds;
    private readonly PcbRenderState _state;

    private static readonly SKTypeface MonoBoldTypeface =
        SKTypeface.FromFamilyName("Consolas", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        ?? SKTypeface.Default;

    private static readonly SKTypeface SansSemiBoldTypeface =
        SKTypeface.FromFamilyName("Segoe UI", SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
        ?? SKTypeface.Default;

    public static readonly Dictionary<string, SKColor> LayerColors = new(StringComparer.Ordinal)
    {
        ["F.Cu"] = new SKColor(0xC8, 0x34, 0x34),
        ["B.Cu"] = new SKColor(0x33, 0x66, 0xCC),
        ["In1.Cu"] = new SKColor(0xD4, 0xA0, 0x17),
        ["In2.Cu"] = new SKColor(0x9C, 0x27, 0xB0),
        ["In3.Cu"] = new SKColor(0x00, 0x89, 0x7B),
        ["In4.Cu"] = new SKColor(0xE6, 0x51, 0x00),
        ["Edge.Cuts"] = new SKColor(0xFF, 0xD7, 0x00),
        ["F.SilkS"] = new SKColor(0xF0, 0xF3, 0xF8),
        ["B.SilkS"] = new SKColor(0xA0, 0xD8, 0xEF),
        ["F.Mask"] = new SKColor(0x94, 0x3B, 0x67),
        ["B.Mask"] = new SKColor(0x3B, 0x67, 0x94),
        ["F.Fab"] = new SKColor(0x78, 0x90, 0x9C),
        ["B.Fab"] = new SKColor(0x78, 0x90, 0x9C)
    };

    private static readonly SKColor HighlightColor = new(0x00, 0xF0, 0xFF);
    private static readonly SKColor BomPrimaryGlow = new(0xFF, 0xB1, 0x42); // Vibrant Golden Amber
    private static readonly SKColor BomActiveGlow = new(0x00, 0xE5, 0xFF);  // Neon Cyan for active component
    private static readonly SKColor Pin1Color = new(0xFF, 0x38, 0x38);      // Vibrant Coral Red for Pin 1 indicator
    private static readonly SKColor ViaCopperColor = new(0xB8, 0x86, 0x0B);

    public PcbSkiaDrawOperation(Rect bounds, PcbRenderState state)
    {
        _bounds = bounds;
        _state = state;
    }

    public Rect Bounds => _bounds;

    public void Dispose() { }

    public bool HitTest(Point p) => _bounds.Contains(p);

    public bool Equals(ICustomDrawOperation? other) => false;

    public void Render(ImmediateDrawingContext context)
    {
        var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (leaseFeature is null) return;

        using var lease = leaseFeature.Lease();
        RenderToSkiaCanvas(lease.SkCanvas);
    }

    public void RenderToSkiaCanvas(SKCanvas canvas)
    {
        // Save base screen-space matrix (already scaled for DPI and positioned at control origin)
        SKMatrix screenMatrix = canvas.TotalMatrix;

        canvas.Save();
        canvas.ClipRect(new SKRect(0, 0, (float)_bounds.Width, (float)_bounds.Height));

        SKColor bgColor = _state.IsLightTheme
            ? new SKColor(0xF7, 0xF2, 0xFA)
            : new SKColor(0x0E, 0x0C, 0x12);
        canvas.DrawColor(bgColor);

        var board = _state.Board;
        if (board is null)
        {
            canvas.Restore();
            return;
        }

        // Compute world matrix
        canvas.Translate((float)_state.PanX, (float)_state.PanY);
        if (Math.Abs(_state.Rotation) > 0.001)
        {
            canvas.RotateDegrees((float)_state.Rotation);
        }
        canvas.Scale((float)_state.Scale, (float)_state.Scale);
        if (_state.IsFlipped)
        {
            canvas.Scale(-1f, 1f);
        }
        SKMatrix worldMatrix = canvas.TotalMatrix;

        RenderBackgroundLayer(canvas, board, bgColor, screenMatrix, worldMatrix);

        if (_state.ActiveNetId.HasValue)
        {
            canvas.SetMatrix(worldMatrix);
            RenderHighlightLayer(canvas, board, bgColor, screenMatrix, worldMatrix);
        }

        if (_state.HighlightedFootprintRefs.Count > 0)
        {
            canvas.SetMatrix(worldMatrix);
            RenderBomHighlightLayer(canvas, board, bgColor, screenMatrix, worldMatrix);
        }

        canvas.SetMatrix(worldMatrix);
        RenderOverlayLayer(canvas, screenMatrix);

        canvas.Restore();
    }

    private SKColor GetLayerColor(string layerName, byte alpha = 255)
    {
        if (_state.IsLightTheme && layerName == "F.SilkS")
            return new SKColor(0x1E, 0x29, 0x3B, alpha);
        if (_state.IsLightTheme && layerName == "B.SilkS")
            return new SKColor(0x03, 0x69, 0xA1, alpha);

        if (LayerColors.TryGetValue(layerName, out var c))
            return c.WithAlpha(alpha);

        return new SKColor(0x88, 0x88, 0x88, alpha);
    }

    private void RenderBackgroundLayer(
        SKCanvas canvas,
        PcbBoard board,
        SKColor drillBgColor,
        SKMatrix screenMatrix,
        SKMatrix worldMatrix)
    {
        bool hasActiveNet = _state.ActiveNetId.HasValue;
        bool hasBomHighlight = _state.HighlightedFootprintRefs.Count > 0;
        int activeNet = _state.ActiveNetId ?? -1;
        byte dimAlpha = (hasActiveNet || hasBomHighlight) ? (byte)46 : (byte)255; // 18% opacity when dimmed

        using var strokePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };

        using var fillPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        // 1. Edge.Cuts (Board Outline)
        if (_state.VisibleLayers.Contains("Edge.Cuts"))
        {
            strokePaint.Color = GetLayerColor("Edge.Cuts", 255);
            strokePaint.StrokeWidth = 0.15f;
            fillPaint.Color = GetLayerColor("Edge.Cuts", 255);

            foreach (var dwg in board.Drawings)
            {
                if (dwg.Layer == "Edge.Cuts")
                {
                    DrawGraphicItem(canvas, dwg, strokePaint, fillPaint);
                }
            }
        }

        // 2. Copper Pour Zones (Inactive)
        if (_state.ZoneMode != ZoneDisplayMode.Hide)
        {
            byte zoneFillAlpha = (byte)(dimAlpha * 0.40);
            foreach (var zone in board.Zones)
            {
                if (!_state.VisibleLayers.Contains(zone.Layer)) continue;
                if (hasActiveNet && zone.Net == activeNet) continue;

                SKColor baseColor = GetLayerColor(zone.Layer, 255);
                strokePaint.Color = baseColor.WithAlpha(dimAlpha);
                strokePaint.StrokeWidth = 0.1f;
                fillPaint.Color = baseColor.WithAlpha(zoneFillAlpha);

                foreach (var poly in zone.Polygons)
                {
                    if (poly.Count < 3) continue;
                    using var path = BuildPolygonPath(poly);

                    if (_state.ZoneMode == ZoneDisplayMode.Solid)
                    {
                        canvas.DrawPath(path, fillPaint);
                    }
                    else if (_state.ZoneMode == ZoneDisplayMode.Hatch)
                    {
                        DrawHatchedPath(canvas, path, baseColor.WithAlpha((byte)(dimAlpha * 0.65)));
                    }
                    canvas.DrawPath(path, strokePaint);
                }
            }
        }

        // 3. Tracks (Segments and Arcs)
        foreach (var track in board.Tracks)
        {
            if (!_state.VisibleLayers.Contains(track.Layer)) continue;
            if (hasActiveNet && track.Net == activeNet) continue;

            strokePaint.Color = GetLayerColor(track.Layer, dimAlpha);
            strokePaint.StrokeWidth = (float)track.Width;

            DrawTrack(canvas, track, strokePaint);
        }

        // 4. Silkscreen & Fab Graphics (Board-level and Footprint-level)
        byte silkAlpha = (byte)(dimAlpha * 0.85);

        // Group filled polygons by layer for single-pass solid rendering (eliminates antialiasing line seams on labels/logos)
        var filledPolysByLayer = new Dictionary<string, List<PcbGraphic>>(StringComparer.Ordinal);
        var otherGraphics = new List<PcbGraphic>();

        void CategorizeGraphic(PcbGraphic dwg)
        {
            if (dwg.Layer == "Edge.Cuts" || dwg.Type == "gr_text") return;
            if (!_state.VisibleLayers.Contains(dwg.Layer)) return;

            bool isFilledPoly = dwg.Type == "gr_poly" && dwg.Pts is { Count: > 2 } &&
                                !string.IsNullOrEmpty(dwg.Fill) && dwg.Fill is not ("no" or "none");
            if (isFilledPoly)
            {
                if (!filledPolysByLayer.TryGetValue(dwg.Layer, out var list))
                {
                    list = new List<PcbGraphic>();
                    filledPolysByLayer[dwg.Layer] = list;
                }
                list.Add(dwg);
            }
            else
            {
                otherGraphics.Add(dwg);
            }
        }

        foreach (var dwg in board.Drawings) CategorizeGraphic(dwg);
        foreach (var fp in board.Footprints)
        {
            foreach (var dwg in fp.Drawings) CategorizeGraphic(dwg);
        }

        // Draw batched filled polygons by layer
        foreach (var (layer, polys) in filledPolysByLayer)
        {
            SKColor c = GetLayerColor(layer, silkAlpha);
            fillPaint.Color = c;
            strokePaint.Color = c;

            using var combinedPath = new SKPath();
            foreach (var dwg in polys)
            {
                combinedPath.MoveTo((float)dwg.Pts![0][0], (float)dwg.Pts[0][1]);
                for (int i = 1; i < dwg.Pts.Count; i++)
                    combinedPath.LineTo((float)dwg.Pts[i][0], (float)dwg.Pts[i][1]);
                combinedPath.Close();
            }
            canvas.DrawPath(combinedPath, fillPaint);

            foreach (var dwg in polys)
            {
                if (dwg.Width > 0)
                {
                    strokePaint.StrokeWidth = (float)dwg.Width;
                    using var p = new SKPath();
                    p.MoveTo((float)dwg.Pts![0][0], (float)dwg.Pts[0][1]);
                    for (int i = 1; i < dwg.Pts.Count; i++)
                        p.LineTo((float)dwg.Pts[i][0], (float)dwg.Pts[i][1]);
                    p.Close();
                    canvas.DrawPath(p, strokePaint);
                }
            }
        }

        // Draw other graphics (lines, arcs, circles, rects, unfilled polygons)
        foreach (var dwg in otherGraphics)
        {
            SKColor c = GetLayerColor(dwg.Layer, silkAlpha);
            strokePaint.Color = c;
            fillPaint.Color = c;
            strokePaint.StrokeWidth = (float)(dwg.Width > 0 ? dwg.Width : 0.12);
            DrawGraphicItem(canvas, dwg, strokePaint, fillPaint);
        }

        // 5. Vias
        foreach (var via in board.Vias)
        {
            if (!IsViaVisible(via)) continue;
            if (hasActiveNet && via.Net == activeNet) continue;

            fillPaint.Color = ViaCopperColor.WithAlpha(dimAlpha);
            canvas.DrawCircle((float)via.X, (float)via.Y, (float)(via.Size / 2.0), fillPaint);

            fillPaint.Color = drillBgColor;
            canvas.DrawCircle((float)via.X, (float)via.Y, (float)(via.Drill / 2.0), fillPaint);
        }

        // 6. Pads
        foreach (var pad in board.Pads)
        {
            if (!_state.IsPadVisible(pad)) continue;
            if (hasActiveNet && pad.Net == activeNet) continue;
            if (hasBomHighlight && _state.HighlightedFootprintRefs.Contains(pad.FpRef)) continue;

            bool isThroughHole = pad.Drill > 0 || pad.Layers.Contains("*.Cu");
            string colorLayer = isThroughHole
                ? (_state.IsFlipped ? "B.Cu" : "F.Cu")
                : (pad.Layers.Contains("F.Cu") ? "F.Cu" : "B.Cu");

            fillPaint.Color = GetLayerColor(colorLayer, dimAlpha);
            DrawPadShape(canvas, pad, fillPaint, 1.0f);

            if (pad.Drill > 0)
            {
                fillPaint.Color = drillBgColor;
                DrawPadDrill(canvas, pad, fillPaint);
            }
        }

        // 7. Silkscreen & Technical Text (in Screen Space)
        foreach (var dwg in board.Drawings)
        {
            if (dwg.Type == "gr_text" && _state.VisibleLayers.Contains(dwg.Layer) && !string.IsNullOrEmpty(dwg.Text))
            {
                DrawSilkscreenText(canvas, screenMatrix, dwg.Text!, dwg.Layer, dwg.X, dwg.Y, dwg.Rot, dwg.Size, silkAlpha);
            }
        }

        foreach (var fp in board.Footprints)
        {
            foreach (var txt in fp.Texts)
            {
                if (_state.VisibleLayers.Contains(txt.Layer) && !string.IsNullOrEmpty(txt.Text))
                {
                    DrawSilkscreenText(canvas, screenMatrix, txt.Text, txt.Layer, txt.X, txt.Y, txt.Rot, txt.Size, silkAlpha);
                }
            }
        }

        // 8. Pad & Via Net Labels (in Screen Space)
        if (_state.ShowPadNetNames)
        {
            var netLookup = new Dictionary<int, string>(board.Nets.Count);
            foreach (var n in board.Nets)
                netLookup[n.Id] = n.Name;

            foreach (var pad in board.Pads)
            {
                if (!_state.IsPadVisible(pad)) continue;
                if (hasActiveNet && pad.Net == activeNet) continue;
                if (hasBomHighlight && _state.HighlightedFootprintRefs.Contains(pad.FpRef)) continue;
                DrawPadText(canvas, screenMatrix, pad, false);
            }

            foreach (var via in board.Vias)
            {
                if (!IsViaVisible(via)) continue;
                if (hasActiveNet && via.Net == activeNet) continue;
                if (netLookup.TryGetValue(via.Net, out string? netName) && !string.IsNullOrEmpty(netName))
                {
                    DrawViaText(canvas, screenMatrix, via, netName, false);
                }
            }
        }

        canvas.SetMatrix(worldMatrix);
    }

    private void RenderHighlightLayer(
        SKCanvas canvas,
        PcbBoard board,
        SKColor drillBgColor,
        SKMatrix screenMatrix,
        SKMatrix worldMatrix)
    {
        int activeNet = _state.ActiveNetId!.Value;

        using var strokePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            Color = HighlightColor
        };

        using var fillPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = HighlightColor
        };

        // 1. Highlight Active Zones
        if (_state.ZoneMode != ZoneDisplayMode.Hide)
        {
            fillPaint.Color = HighlightColor.WithAlpha(115); // 0.45 alpha
            strokePaint.Color = HighlightColor;
            strokePaint.StrokeWidth = 0.12f;

            foreach (var zone in board.Zones)
            {
                if (zone.Net != activeNet) continue;
                if (!_state.VisibleLayers.Contains(zone.Layer)) continue;

                foreach (var poly in zone.Polygons)
                {
                    if (poly.Count < 3) continue;
                    using var path = BuildPolygonPath(poly);
                    if (_state.ZoneMode != ZoneDisplayMode.Outline)
                        canvas.DrawPath(path, fillPaint);
                    canvas.DrawPath(path, strokePaint);
                }
            }
        }

        // 2. Highlight Active Tracks
        strokePaint.Color = HighlightColor;
        foreach (var track in board.Tracks)
        {
            if (track.Net != activeNet) continue;
            if (!_state.VisibleLayers.Contains(track.Layer)) continue;

            strokePaint.StrokeWidth = (float)Math.Max(track.Width * 1.15, 0.2);
            DrawTrack(canvas, track, strokePaint);
        }

        // 3. Highlight Active Vias
        foreach (var via in board.Vias)
        {
            if (via.Net != activeNet) continue;
            if (!IsViaVisible(via)) continue;

            fillPaint.Color = HighlightColor;
            canvas.DrawCircle((float)via.X, (float)via.Y, (float)(via.Size / 2.0 * 1.2), fillPaint);

            fillPaint.Color = drillBgColor;
            canvas.DrawCircle((float)via.X, (float)via.Y, (float)(via.Drill / 2.0), fillPaint);
        }

        // 4. Highlight Active Pads
        foreach (var pad in board.Pads)
        {
            if (pad.Net != activeNet) continue;
            if (!_state.IsPadVisible(pad)) continue;

            fillPaint.Color = HighlightColor;
            DrawPadShape(canvas, pad, fillPaint, 1.15f);

            if (pad.Drill > 0)
            {
                fillPaint.Color = drillBgColor;
                DrawPadDrill(canvas, pad, fillPaint);
            }
        }

        // 5. Highlight Labels on Active Pads & Vias (in Screen Space)
        if (_state.ShowPadNetNames)
        {
            string activeNetName = board.Nets.FirstOrDefault(n => n.Id == activeNet)?.Name ?? "";
            foreach (var pad in board.Pads)
            {
                if (pad.Net != activeNet) continue;
                if (!_state.IsPadVisible(pad)) continue;
                DrawPadText(canvas, screenMatrix, pad, true);
            }

            if (!string.IsNullOrEmpty(activeNetName))
            {
                foreach (var via in board.Vias)
                {
                    if (via.Net != activeNet) continue;
                    if (!IsViaVisible(via)) continue;
                    DrawViaText(canvas, screenMatrix, via, activeNetName, true);
                }
            }
        }

        canvas.SetMatrix(worldMatrix);
    }

    private void RenderBomHighlightLayer(
        SKCanvas canvas,
        PcbBoard board,
        SKColor drillBgColor,
        SKMatrix screenMatrix,
        SKMatrix worldMatrix)
    {
        var highlighted = _state.HighlightedFootprintRefs;
        if (highlighted.Count == 0) return;

        using var strokePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };

        using var fillPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        var padsByRef = board.Pads
            .Where(p => highlighted.Contains(p.FpRef))
            .GroupBy(p => p.FpRef, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // 1. Draw component boundary bounding reticles and silkscreen outlines in World Space
        foreach (var fp in board.Footprints)
        {
            if (!highlighted.Contains(fp.Ref)) continue;

            bool isCurrentActive = string.Equals(fp.Ref, _state.ActiveFootprintRef, StringComparison.OrdinalIgnoreCase);
            SKColor compColor = isCurrentActive ? BomActiveGlow : BomPrimaryGlow;

            // Footprint drawings (silkscreen & fab lines) with high opacity
            strokePaint.Color = compColor;
            strokePaint.StrokeWidth = (float)Math.Max(0.18, 1.8 / _state.Scale);
            fillPaint.Color = compColor.WithAlpha(50);

            foreach (var dwg in fp.Drawings)
            {
                DrawGraphicItem(canvas, dwg, strokePaint, fillPaint);
            }

            // Calculate component bounding box from pads
            if (padsByRef.TryGetValue(fp.Ref, out var fpPads) && fpPads.Count > 0)
            {
                double minX = double.MaxValue, minY = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue;
                foreach (var p in fpPads)
                {
                    double hw = Math.Max(p.W, 0.4) / 2.0;
                    double hh = Math.Max(p.H, 0.4) / 2.0;
                    minX = Math.Min(minX, p.X - hw);
                    maxX = Math.Max(maxX, p.X + hw);
                    minY = Math.Min(minY, p.Y - hh);
                    maxY = Math.Max(maxY, p.Y + hh);
                }

                float padMargin = 0.35f;
                var rect = new SKRect((float)minX - padMargin, (float)minY - padMargin, (float)maxX + padMargin, (float)maxY + padMargin);

                fillPaint.Color = compColor.WithAlpha(isCurrentActive ? (byte)55 : (byte)28);
                canvas.DrawRoundRect(rect, 0.35f, 0.35f, fillPaint);

                strokePaint.Color = compColor;
                strokePaint.StrokeWidth = (float)Math.Max(0.15, (isCurrentActive ? 2.6 : 1.8) / _state.Scale);
                canvas.DrawRoundRect(rect, 0.35f, 0.35f, strokePaint);

                // Highlight all component pads
                foreach (var pad in fpPads)
                {
                    fillPaint.Color = compColor;
                    DrawPadShape(canvas, pad, fillPaint, 1.15f);

                    if (pad.Drill > 0)
                    {
                        fillPaint.Color = drillBgColor;
                        DrawPadDrill(canvas, pad, fillPaint);
                    }

                    // Pin 1 indicator
                    if (pad.Num == "1")
                    {
                        using var pin1Paint = new SKPaint
                        {
                            IsAntialias = true,
                            Style = SKPaintStyle.Stroke,
                            Color = Pin1Color,
                            StrokeWidth = (float)Math.Max(0.18, 2.4 / _state.Scale)
                        };

                        float radius = (float)(Math.Max(pad.W, pad.H) / 2.0 + 0.38);
                        canvas.DrawCircle((float)pad.X, (float)pad.Y, radius, pin1Paint);

                        using var pin1Dot = new SKPaint
                        {
                            IsAntialias = true,
                            Style = SKPaintStyle.Fill,
                            Color = Pin1Color
                        };
                        canvas.DrawCircle((float)pad.X, (float)pad.Y, (float)Math.Max(0.12, 1.4 / _state.Scale), pin1Dot);
                    }
                }
            }
        }

        // 2. Component Badges in Screen Space
        canvas.SetMatrix(screenMatrix);
        foreach (var fp in board.Footprints)
        {
            if (!highlighted.Contains(fp.Ref)) continue;

            bool isCurrentActive = string.Equals(fp.Ref, _state.ActiveFootprintRef, StringComparison.OrdinalIgnoreCase);
            SKColor compColor = isCurrentActive ? BomActiveGlow : BomPrimaryGlow;

            var (sx, sy) = WorldToScreen(fp.X, fp.Y);
            if (sx < -100 || sx > _bounds.Width + 100 || sy < -100 || sy > _bounds.Height + 100)
                continue;

            string labelText = string.IsNullOrWhiteSpace(fp.Val)
                ? fp.Ref
                : $"{fp.Ref}  {fp.Val}";

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
                StrokeWidth = isCurrentActive ? 2.0f : 1.2f,
                Color = compColor
            };
            canvas.DrawRoundRect(pillRect, 6f, 6f, pillBorderPaint);

            DrawOutlinedText(
                canvas,
                labelText,
                (float)sx,
                (float)sy - 8f - pillH / 2f,
                fontSize,
                SansSemiBoldTypeface,
                SKColors.White,
                SKColors.Black,
                2.5f
            );
        }

        canvas.SetMatrix(worldMatrix);
    }

    private void RenderOverlayLayer(SKCanvas canvas, SKMatrix screenMatrix)
    {
        // 1. Hovered Pad Indicator
        if (_state.HoveredPad is { } hovered)
        {
            using var hoverPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                Color = SKColors.White,
                StrokeWidth = (float)Math.Max(0.12, 1.5 / _state.Scale)
            };

            float rx = (float)(hovered.X - hovered.W / 2.0 - 0.15);
            float ry = (float)(hovered.Y - hovered.H / 2.0 - 0.15);
            float rw = (float)(hovered.W + 0.30);
            float rh = (float)(hovered.H + 0.30);
            canvas.DrawRect(rx, ry, rw, rh, hoverPaint);
        }

        // 2. Selected / Inspected Pin Target Reticle
        if (_state.SelectedPad is { } selected)
        {
            float padRadius = (float)(Math.Max(selected.W, selected.H) / 2.0 + 0.45);
            float strokeW = (float)Math.Max(0.15, 2.2 / _state.Scale);

            using var selPaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                Color = new SKColor(0xFF, 0xB1, 0x42), // Amber target reticle
                StrokeWidth = strokeW
            };

            canvas.DrawCircle((float)selected.X, (float)selected.Y, padRadius, selPaint);

            // Crosshair ticks around the selected pin
            float tickLen = padRadius * 0.55f;
            float cx = (float)selected.X;
            float cy = (float)selected.Y;
            canvas.DrawLine(cx - padRadius - tickLen, cy, cx - padRadius * 0.6f, cy, selPaint);
            canvas.DrawLine(cx + padRadius * 0.6f, cy, cx + padRadius + tickLen, cy, selPaint);
            canvas.DrawLine(cx, cy - padRadius - tickLen, cx, cy - padRadius * 0.6f, selPaint);
            canvas.DrawLine(cx, cy + padRadius * 0.6f, cx, cy + padRadius + tickLen, selPaint);
        }
    }

    private bool IsViaVisible(PcbVia via)
    {
        foreach (string l in via.Layers)
        {
            if (_state.VisibleLayers.Contains(l)) return true;
        }
        return false;
    }

    private static SKPath BuildPolygonPath(List<double[]> poly)
    {
        var path = new SKPath();
        path.MoveTo((float)poly[0][0], (float)poly[0][1]);
        for (int i = 1; i < poly.Count; i++)
        {
            path.LineTo((float)poly[i][0], (float)poly[i][1]);
        }
        path.Close();
        return path;
    }

    private static void DrawHatchedPath(SKCanvas canvas, SKPath polyPath, SKColor color)
    {
        canvas.Save();
        canvas.ClipPath(polyPath, SKClipOperation.Intersect, true);

        SKRect bounds = polyPath.Bounds;
        float step = 0.8f; // 0.8mm hatch spacing
        using var hatchPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            Color = color,
            StrokeWidth = 0.12f
        };

        float minX = bounds.Left - bounds.Height;
        float maxX = bounds.Right + bounds.Height;
        for (float x = minX; x <= maxX; x += step)
        {
            canvas.DrawLine(x, bounds.Top, x + bounds.Height, bounds.Bottom, hatchPaint);
        }

        canvas.Restore();
    }

    private static void DrawTrack(SKCanvas canvas, PcbTrack track, SKPaint strokePaint)
    {
        if (track.Type == "segment")
        {
            canvas.DrawLine(
                (float)track.Start[0],
                (float)track.Start[1],
                (float)track.End[0],
                (float)track.End[1],
                strokePaint);
        }
        else if (track.Type == "arc" && track.Mid is { Length: >= 2 })
        {
            using var path = new SKPath();
            path.MoveTo((float)track.Start[0], (float)track.Start[1]);
            float cpx = (float)(2.0 * track.Mid[0] - 0.5 * (track.Start[0] + track.End[0]));
            float cpy = (float)(2.0 * track.Mid[1] - 0.5 * (track.Start[1] + track.End[1]));
            path.QuadTo(cpx, cpy, (float)track.End[0], (float)track.End[1]);
            canvas.DrawPath(path, strokePaint);
        }
    }

    private static void DrawPadShape(SKCanvas canvas, PcbPad pad, SKPaint fillPaint, float scaleMultiplier)
    {
        canvas.Save();
        canvas.Translate((float)pad.X, (float)pad.Y);
        if (Math.Abs(pad.Rot) > 0.001)
        {
            canvas.RotateDegrees((float)(-pad.Rot));
        }

        float w = (float)pad.W * scaleMultiplier;
        float h = (float)pad.H * scaleMultiplier;

        if (pad.Shape == "circle")
        {
            canvas.DrawCircle(0, 0, w / 2f, fillPaint);
        }
        else if (pad.Shape == "oval")
        {
            float r = Math.Min(w, h) / 2f;
            canvas.DrawRoundRect(-w / 2f, -h / 2f, w, h, r, r, fillPaint);
        }
        else if (pad.Shape == "roundrect")
        {
            float rratio = (float)(pad.Rratio > 0 ? pad.Rratio : 0.25);
            float r = Math.Min(w, h) * rratio;
            canvas.DrawRoundRect(-w / 2f, -h / 2f, w, h, r, r, fillPaint);
        }
        else
        {
            canvas.DrawRect(-w / 2f, -h / 2f, w, h, fillPaint);
        }

        canvas.Restore();
    }

    private static void DrawPadDrill(SKCanvas canvas, PcbPad pad, SKPaint fillPaint)
    {
        if (pad.DrillW > 0 && pad.DrillH > 0 && Math.Abs(pad.DrillW - pad.DrillH) > 0.0001)
        {
            canvas.Save();
            canvas.Translate((float)pad.X, (float)pad.Y);
            if (Math.Abs(pad.Rot) > 0.001)
            {
                canvas.RotateDegrees((float)(-pad.Rot));
            }
            float dw = (float)pad.DrillW;
            float dh = (float)pad.DrillH;
            float r = Math.Min(dw, dh) / 2f;
            canvas.DrawRoundRect(-dw / 2f, -dh / 2f, dw, dh, r, r, fillPaint);
            canvas.Restore();
        }
        else
        {
            canvas.DrawCircle((float)pad.X, (float)pad.Y, (float)(pad.Drill / 2.0), fillPaint);
        }
    }

    private static void DrawGraphicItem(SKCanvas canvas, PcbGraphic dwg, SKPaint strokePaint, SKPaint fillPaint)
    {
        if (dwg.Type == "gr_text") return;

        if (dwg.Type == "gr_line" && dwg.Start is { Length: >= 2 } && dwg.End is { Length: >= 2 })
        {
            canvas.DrawLine((float)dwg.Start[0], (float)dwg.Start[1], (float)dwg.End[0], (float)dwg.End[1], strokePaint);
        }
        else if (dwg.Type == "gr_arc" && dwg.Start is { Length: >= 2 } && dwg.Mid is { Length: >= 2 } && dwg.End is { Length: >= 2 })
        {
            using var path = new SKPath();
            path.MoveTo((float)dwg.Start[0], (float)dwg.Start[1]);
            float cpx = (float)(2.0 * dwg.Mid[0] - 0.5 * (dwg.Start[0] + dwg.End[0]));
            float cpy = (float)(2.0 * dwg.Mid[1] - 0.5 * (dwg.Start[1] + dwg.End[1]));
            path.QuadTo(cpx, cpy, (float)dwg.End[0], (float)dwg.End[1]);
            canvas.DrawPath(path, strokePaint);
        }
        else if (dwg.Type == "gr_circle" && dwg.Center is { Length: >= 2 } && dwg.End is { Length: >= 2 })
        {
            double dx = dwg.End[0] - dwg.Center[0];
            double dy = dwg.End[1] - dwg.Center[1];
            float r = (float)Math.Sqrt(dx * dx + dy * dy);
            bool isFilled = !string.IsNullOrEmpty(dwg.Fill) && dwg.Fill is not ("no" or "none");
            if (isFilled)
            {
                canvas.DrawCircle((float)dwg.Center[0], (float)dwg.Center[1], r, fillPaint);
            }
            canvas.DrawCircle((float)dwg.Center[0], (float)dwg.Center[1], r, strokePaint);
        }
        else if (dwg.Type == "gr_rect" && dwg.Start is { Length: >= 2 } && dwg.End is { Length: >= 2 })
        {
            float rx = (float)Math.Min(dwg.Start[0], dwg.End[0]);
            float ry = (float)Math.Min(dwg.Start[1], dwg.End[1]);
            float rw = (float)Math.Abs(dwg.End[0] - dwg.Start[0]);
            float rh = (float)Math.Abs(dwg.End[1] - dwg.Start[1]);
            bool isFilled = !string.IsNullOrEmpty(dwg.Fill) && dwg.Fill is not ("no" or "none");
            if (isFilled)
            {
                canvas.DrawRect(rx, ry, rw, rh, fillPaint);
            }
            canvas.DrawRect(rx, ry, rw, rh, strokePaint);
        }
        else if (dwg.Type == "gr_poly" && dwg.Pts is { Count: > 2 })
        {
            using var path = BuildPolygonPath(dwg.Pts);
            bool isFilled = !string.IsNullOrEmpty(dwg.Fill) && dwg.Fill is not ("no" or "none");
            if (isFilled)
            {
                canvas.DrawPath(path, fillPaint);
            }
            if (dwg.Width > 0)
            {
                canvas.DrawPath(path, strokePaint);
            }
            else if (!isFilled)
            {
                float oldW = strokePaint.StrokeWidth;
                strokePaint.StrokeWidth = 0.05f;
                canvas.DrawPath(path, strokePaint);
                strokePaint.StrokeWidth = oldW;
            }
        }
    }

    private (float X, float Y) WorldToScreen(double worldX, double worldY)
    {
        double effX = _state.IsFlipped ? -worldX : worldX;
        double x2 = effX * _state.Scale;
        double y2 = worldY * _state.Scale;

        if (Math.Abs(_state.Rotation) > 0.001)
        {
            double rad = _state.Rotation * (Math.PI / 180.0);
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);
            double rx = x2 * cos - y2 * sin;
            double ry = x2 * sin + y2 * cos;
            return ((float)(_state.PanX + rx), (float)(_state.PanY + ry));
        }

        return ((float)(_state.PanX + x2), (float)(_state.PanY + y2));
    }

    private void DrawSilkscreenText(
        SKCanvas canvas,
        SKMatrix screenMatrix,
        string text,
        string layer,
        double worldX,
        double worldY,
        double rot,
        double sizeMm,
        byte alpha)
    {
        double pxSize = (sizeMm > 0 ? sizeMm : 1.0) * _state.Scale;
        if (pxSize < 4.0) return;

        var (sx, sy) = WorldToScreen(worldX, worldY);

        float maxPx = (float)(pxSize * Math.Max(text.Length, 2) + 20);
        if (sx < -maxPx || sx > _bounds.Width + maxPx || sy < -maxPx || sy > _bounds.Height + maxPx)
            return;

        double angle = _state.IsFlipped ? -rot : rot;
        angle -= _state.Rotation;

        canvas.Save();
        canvas.SetMatrix(screenMatrix);
        canvas.Translate(sx, sy);
        if (Math.Abs(angle) > 0.01)
            canvas.RotateDegrees((float)(-angle));

        bool isMirrored = (layer.StartsWith("B.", StringComparison.Ordinal) && !_state.IsFlipped)
                       || (layer.StartsWith("F.", StringComparison.Ordinal) && _state.IsFlipped);
        if (isMirrored)
            canvas.Scale(-1f, 1f);

        float fSize = (float)Math.Max(7.0, Math.Round(pxSize));
        DrawOutlinedText(
            canvas,
            text,
            0,
            0,
            fSize,
            SansSemiBoldTypeface,
            GetLayerColor(layer, alpha),
            new SKColor(0x0B, 0x0E, 0x14, (byte)(alpha * 0.75)),
            Math.Max(1.5f, fSize * 0.16f));

        canvas.Restore();
    }

    private void DrawPadText(SKCanvas canvas, SKMatrix screenMatrix, PcbPad pad, bool isHighlight)
    {
        string netName = pad.NetName ?? "";
        string pinNum = pad.Num ?? "";
        if (string.IsNullOrEmpty(netName) && string.IsNullOrEmpty(pinNum))
            return;

        string displayNet = CleanDisplayNetName(netName);

        var (sx, sy) = WorldToScreen(pad.X, pad.Y);

        float padMaxPx = (float)(Math.Max(pad.W, pad.H) * _state.Scale + 30);
        if (sx < -padMaxPx || sx > _bounds.Width + padMaxPx || sy < -padMaxPx || sy > _bounds.Height + padMaxPx)
            return;

        double angle = pad.Rot;
        bool isVertical = pad.H > pad.W * 1.25;
        if (isVertical) angle += 90.0;
        angle -= _state.Rotation;

        double normAngle = ((angle % 360.0) + 360.0) % 360.0;
        if (_state.IsFlipped)
            normAngle = (360.0 - normAngle) % 360.0;
        if (normAngle > 90.0 && normAngle < 270.0)
            angle += 180.0;

        canvas.Save();
        canvas.SetMatrix(screenMatrix);
        canvas.Translate(sx, sy);
        canvas.RotateDegrees((float)(-angle));
        if (_state.IsFlipped)
            canvas.Scale(-1f, 1f);

        double lengthAxis = isVertical ? pad.H : pad.W;
        double widthAxis = isVertical ? pad.W : pad.H;
        double availW = lengthAxis * _state.Scale * 0.92;
        double availH = widthAxis * _state.Scale * 0.92;
        bool isThroughHole = pad.Drill > 0 || pad.Layers.Contains("*.Cu");

        bool showTwoLines = !string.IsNullOrEmpty(pinNum) && !string.IsNullOrEmpty(displayNet) && availH > 22 && availW > 16;

        if (showTwoLines)
        {
            float fSizeNum = (float)Math.Max(7.0, Math.Min(availH * 0.32, 20.0));
            float fSizeNet = (float)Math.Max(8.0, Math.Min(Math.Min(availH * 0.40, availW / (Math.Max(displayNet.Length, 1) * 0.62)), 26.0));

            SKColor strokeColor = isHighlight ? HighlightColor : new SKColor(0x0B, 0x0E, 0x14, 242);
            SKColor fillNum = isHighlight ? new SKColor(0x05, 0x08, 0x10) : new SKColor(0xFF, 0xFF, 0xFF, 216);
            SKColor fillNet = isHighlight ? new SKColor(0x05, 0x08, 0x10) : SKColors.White;

            DrawOutlinedText(canvas, pinNum, 0, (float)(-availH * 0.22), fSizeNum, MonoBoldTypeface, fillNum, strokeColor, Math.Max(2f, fSizeNum * 0.22f));
            DrawOutlinedText(canvas, displayNet, 0, (float)(availH * 0.24), fSizeNet, MonoBoldTypeface, fillNet, strokeColor, Math.Max(2f, fSizeNet * 0.22f));
        }
        else
        {
            string text = !string.IsNullOrEmpty(displayNet) ? displayNet : pinNum;
            double fSize = isThroughHole
                ? Math.Max(availH * 0.70, pad.Drill * _state.Scale * 0.65)
                : availH * 0.70;
            fSize = Math.Min(fSize, availW / (Math.Max(text.Length, 1) * 0.58));
            float clampedSize = (float)Math.Max(7.0, Math.Min(fSize, 52.0));

            SKColor strokeColor = isHighlight ? HighlightColor : new SKColor(0x0B, 0x0E, 0x14, 242);
            SKColor fillColor = isHighlight ? new SKColor(0x05, 0x08, 0x10) : SKColors.White;

            DrawOutlinedText(canvas, text, 0, 0, clampedSize, MonoBoldTypeface, fillColor, strokeColor, Math.Max(2f, clampedSize * 0.22f));
        }

        canvas.Restore();
    }

    private void DrawViaText(SKCanvas canvas, SKMatrix screenMatrix, PcbVia via, string netName, bool isHighlight)
    {
        if (netName.StartsWith("unconnected-(", StringComparison.Ordinal))
            return;

        string text = CleanDisplayNetName(netName);
        if (string.IsNullOrEmpty(text))
            return;

        var (sx, sy) = WorldToScreen(via.X, via.Y);

        double viaPx = via.Size * _state.Scale;
        if (sx < -viaPx || sx > _bounds.Width + viaPx || sy < -viaPx || sy > _bounds.Height + viaPx)
            return;

        canvas.Save();
        canvas.SetMatrix(screenMatrix);
        canvas.Translate(sx, sy);
        if (_state.IsFlipped)
            canvas.Scale(-1f, 1f);

        float fSize = (float)Math.Max(7.0, Math.Min(viaPx * 0.75, 28.0));
        SKColor strokeColor = isHighlight ? HighlightColor : new SKColor(0x0B, 0x0E, 0x14, 242);
        SKColor fillColor = isHighlight ? new SKColor(0x05, 0x08, 0x10) : SKColors.White;

        DrawOutlinedText(canvas, text, 0, 0, fSize, MonoBoldTypeface, fillColor, strokeColor, Math.Max(2f, fSize * 0.22f));
        canvas.Restore();
    }

    private static string CleanDisplayNetName(string netName)
    {
        if (string.IsNullOrEmpty(netName)) return "";
        if (netName.StartsWith("Net-(", StringComparison.Ordinal) && netName.EndsWith(')'))
            return netName[5..^1];
        if (netName.StartsWith("unconnected-(", StringComparison.Ordinal))
            return "NC";
        return netName;
    }

    private static void DrawOutlinedText(
        SKCanvas canvas,
        string text,
        float x,
        float y,
        float fontSize,
        SKTypeface typeface,
        SKColor fillColor,
        SKColor strokeColor,
        float strokeWidth)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Typeface = typeface,
            TextSize = fontSize,
            TextAlign = SKTextAlign.Center,
            StrokeJoin = SKStrokeJoin.Round
        };

        // Vertically center baseline
        var metrics = paint.FontMetrics;
        float baselineOffset = -(metrics.Ascent + metrics.Descent) / 2f;
        float drawY = y + baselineOffset;

        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = strokeWidth;
        paint.Color = strokeColor;
        canvas.DrawText(text, x, drawY, paint);

        paint.Style = SKPaintStyle.Fill;
        paint.Color = fillColor;
        canvas.DrawText(text, x, drawY, paint);
    }
}
