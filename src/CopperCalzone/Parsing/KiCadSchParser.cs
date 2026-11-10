using CopperCalzone.Models.Schematic;

namespace CopperCalzone.Parsing;

public sealed class KiCadSchParser
{
    private readonly SExpNode _tree;

    public KiCadSchParser(string content)
    {
        _tree = SExpressionParser.Parse(content.AsSpan());
        if (!_tree.IsList || _tree.Tag != "kicad_sch")
        {
            throw new InvalidDataException("Input file is not a valid KiCad schematic (.kicad_sch) file.");
        }
    }

    public KiCadSchematic Parse()
    {
        string paper = "A4";
        var paperNode = _tree.FindSub("paper");
        if (paperNode is not null && paperNode.Count > 1)
        {
            paper = paperNode.GetString(1);
        }

        string title = "", date = "", rev = "";
        var titleBlock = _tree.FindSub("title_block");
        if (titleBlock is not null)
        {
            title = titleBlock.FindSub("title")?.GetString(1) ?? "";
            date = titleBlock.FindSub("date")?.GetString(1) ?? "";
            rev = titleBlock.FindSub("rev")?.GetString(1) ?? "";
        }

        var libSymbols = ParseLibSymbols();
        var symbols = ParseSymbols(libSymbols);
        var wires = ParseWires();
        var buses = ParseBuses();
        var junctions = ParseJunctions();
        var labels = ParseLabels();
        var sheets = ParseSheets();
        var noConnects = ParseNoConnects();

        var sch = new KiCadSchematic
        {
            Paper = paper,
            Title = title,
            Date = date,
            Revision = rev,
            LibSymbols = libSymbols,
            Symbols = symbols,
            Wires = wires,
            Buses = buses,
            Junctions = junctions,
            Labels = labels,
            Sheets = sheets,
            NoConnects = noConnects
        };

        ComputeBoundingBox(sch);
        return sch;
    }

    private Dictionary<string, SchLibSymbol> ParseLibSymbols()
    {
        var dict = new Dictionary<string, SchLibSymbol>(StringComparer.OrdinalIgnoreCase);
        var libNode = _tree.FindSub("lib_symbols");
        if (libNode is null) return dict;

        foreach (var symNode in libNode.FindAllSub("symbol"))
        {
            if (symNode.Count < 2) continue;
            string symName = symNode.GetString(1);
            var libSym = new SchLibSymbol { Name = symName };

            // Parse sub-symbols or direct elements
            ParseLibSymbolBody(symNode, libSym, 0);

            foreach (var subNode in symNode.FindAllSub("symbol"))
            {
                if (subNode.Count < 2) continue;
                string subName = subNode.GetString(1);
                // Sub-symbol names often end in _Unit_Style, e.g. "R_1_1" or "U1_0_1"
                int unit = ExtractUnitFromName(subName);
                ParseLibSymbolBody(subNode, libSym, unit);
            }

            dict[symName] = libSym;
        }

        return dict;
    }

    private static int ExtractUnitFromName(string name)
    {
        int lastUnderscore = name.LastIndexOf('_');
        if (lastUnderscore > 0)
        {
            int prevUnderscore = name.LastIndexOf('_', lastUnderscore - 1);
            if (prevUnderscore >= 0)
            {
                string unitStr = name.Substring(prevUnderscore + 1, lastUnderscore - prevUnderscore - 1);
                if (int.TryParse(unitStr, out int u)) return u;
            }
        }
        return 0;
    }

    private static void ParseLibSymbolBody(SExpNode node, SchLibSymbol libSym, int unit)
    {
        if (!libSym.PinsByUnit.TryGetValue(unit, out var unitPins))
        {
            unitPins = [];
            libSym.PinsByUnit[unit] = unitPins;
        }
        if (!libSym.PrimitivesByUnit.TryGetValue(unit, out var unitPrimitives))
        {
            unitPrimitives = [];
            libSym.PrimitivesByUnit[unit] = unitPrimitives;
        }

        // 1. Pins
        foreach (var pNode in node.FindAllSub("pin"))
        {
            var pin = ParsePin(pNode);
            if (pin is not null)
            {
                libSym.Pins.Add(pin);
                unitPins.Add(pin);
            }
        }

        // 2. Polylines
        foreach (var plNode in node.FindAllSub("polyline"))
        {
            var ptsNode = plNode.FindSub("pts");
            if (ptsNode is null) continue;

            var pts = new List<SchPoint>();
            foreach (var xy in ptsNode.FindAllSub("xy"))
            {
                if (xy.Count >= 3)
                    pts.Add(new SchPoint(xy.GetDouble(1), xy.GetDouble(2)));
            }

            double strokeW = plNode.FindSub("stroke")?.FindSub("width")?.GetDouble(1, 0.15) ?? 0.15;
            string? fillType = plNode.FindSub("fill")?.FindSub("type")?.GetString(1);

            var poly = new SchPolyline
            {
                Points = pts,
                StrokeWidth = strokeW,
                FillType = fillType
            };
            libSym.Primitives.Add(poly);
            unitPrimitives.Add(poly);
        }

        // 3. Rectangles
        foreach (var rNode in node.FindAllSub("rectangle"))
        {
            var pStart = rNode.FindSub("start");
            var pEnd = rNode.FindSub("end");
            if (pStart is null || pEnd is null || pStart.Count < 3 || pEnd.Count < 3) continue;

            double strokeW = rNode.FindSub("stroke")?.FindSub("width")?.GetDouble(1, 0.15) ?? 0.15;
            string? fillType = rNode.FindSub("fill")?.FindSub("type")?.GetString(1);

            var rect = new SchRectangle
            {
                StartX = pStart.GetDouble(1),
                StartY = pStart.GetDouble(2),
                EndX = pEnd.GetDouble(1),
                EndY = pEnd.GetDouble(2),
                StrokeWidth = strokeW,
                FillType = fillType
            };
            libSym.Primitives.Add(rect);
            unitPrimitives.Add(rect);
        }

        // 4. Circles
        foreach (var cNode in node.FindAllSub("circle"))
        {
            var pCenter = cNode.FindSub("center");
            var pRadius = cNode.FindSub("radius");
            if (pCenter is null || pRadius is null || pCenter.Count < 3 || pRadius.Count < 2) continue;

            double strokeW = cNode.FindSub("stroke")?.FindSub("width")?.GetDouble(1, 0.15) ?? 0.15;
            string? fillType = cNode.FindSub("fill")?.FindSub("type")?.GetString(1);

            var circle = new SchCircle
            {
                CenterX = pCenter.GetDouble(1),
                CenterY = pCenter.GetDouble(2),
                Radius = pRadius.GetDouble(1),
                StrokeWidth = strokeW,
                FillType = fillType
            };
            libSym.Primitives.Add(circle);
            unitPrimitives.Add(circle);
        }

        // 5. Arcs
        foreach (var aNode in node.FindAllSub("arc"))
        {
            var pStart = aNode.FindSub("start");
            var pMid = aNode.FindSub("mid");
            var pEnd = aNode.FindSub("end");
            if (pStart is null || pMid is null || pEnd is null) continue;

            double strokeW = aNode.FindSub("stroke")?.FindSub("width")?.GetDouble(1, 0.15) ?? 0.15;
            string? fillType = aNode.FindSub("fill")?.FindSub("type")?.GetString(1);

            var arc = new SchArc
            {
                StartX = pStart.GetDouble(1),
                StartY = pStart.GetDouble(2),
                MidX = pMid.GetDouble(1),
                MidY = pMid.GetDouble(2),
                EndX = pEnd.GetDouble(1),
                EndY = pEnd.GetDouble(2),
                StrokeWidth = strokeW,
                FillType = fillType
            };
            libSym.Primitives.Add(arc);
            unitPrimitives.Add(arc);
        }

        // 6. Text
        foreach (var tNode in node.FindAllSub("text"))
        {
            if (tNode.Count < 2) continue;
            string txt = tNode.GetString(1);
            var pAt = tNode.FindSub("at");
            double tx = pAt?.GetDouble(1, 0) ?? 0;
            double ty = pAt?.GetDouble(2, 0) ?? 0;
            double trot = pAt?.GetDouble(3, 0) ?? 0;
            double tsize = tNode.FindSub("effects")?.FindSub("font")?.FindSub("size")?.GetDouble(1, 1.27) ?? 1.27;

            var textPrim = new SchTextPrimitive
            {
                Text = txt,
                X = tx,
                Y = ty,
                Rotation = trot,
                Size = tsize
            };
            libSym.Primitives.Add(textPrim);
            unitPrimitives.Add(textPrim);
        }
    }

    private static SchPin? ParsePin(SExpNode node)
    {
        // (pin <type> <shape> (at x y rot) (length len) (name "...") (number "..."))
        if (node.Count < 2) return null;

        string typeStr = node.GetString(1);
        SchPinType pinType = typeStr switch
        {
            "input" => SchPinType.Input,
            "output" => SchPinType.Output,
            "bidirectional" => SchPinType.Bidirectional,
            "tri_state" => SchPinType.TriState,
            "passive" => SchPinType.Passive,
            "free" => SchPinType.Free,
            "power_in" => SchPinType.PowerIn,
            "power_out" => SchPinType.PowerOut,
            "open_collector" => SchPinType.OpenCollector,
            "open_emitter" => SchPinType.OpenEmitter,
            "no_connect" => SchPinType.NoConnect,
            _ => SchPinType.Unspecified
        };

        var pAt = node.FindSub("at");
        double px = pAt?.GetDouble(1, 0) ?? 0;
        double py = pAt?.GetDouble(2, 0) ?? 0;
        double prot = pAt?.GetDouble(3, 0) ?? 0;

        double len = node.FindSub("length")?.GetDouble(1, 2.54) ?? 2.54;

        string name = node.FindSub("name")?.GetString(1) ?? "";
        bool hideName = node.FindSub("name")?.FindSub("effects")?.FindSub("hide")?.GetString(1) == "yes";

        string number = node.FindSub("number")?.GetString(1) ?? "";
        bool hideNum = node.FindSub("number")?.FindSub("effects")?.FindSub("hide")?.GetString(1) == "yes";

        return new SchPin
        {
            Type = pinType,
            X = px,
            Y = py,
            Rotation = prot,
            Length = len,
            Name = name,
            Number = number,
            HideName = hideName,
            HideNumber = hideNum
        };
    }

    private List<SchSymbol> ParseSymbols(Dictionary<string, SchLibSymbol> libSymbols)
    {
        var list = new List<SchSymbol>();

        foreach (var symNode in _tree.FindAllSub("symbol"))
        {
            var libIdNode = symNode.FindSub("lib_id");
            if (libIdNode is null || libIdNode.Count < 2) continue;
            string libId = libIdNode.GetString(1);

            var pAt = symNode.FindSub("at");
            double sx = pAt?.GetDouble(1, 0) ?? 0;
            double sy = pAt?.GetDouble(2, 0) ?? 0;
            double srot = pAt?.GetDouble(3, 0) ?? 0;

            var mirrorNode = symNode.FindSub("mirror");
            bool mirrorX = mirrorNode?.GetString(1) == "x";
            bool mirrorY = mirrorNode?.GetString(1) == "y";

            int unit = symNode.FindSub("unit")?.GetInt(1) ?? 1;
            string uuid = symNode.FindSub("uuid")?.GetString(1) ?? "";

            var symbol = new SchSymbol
            {
                LibId = libId,
                X = sx,
                Y = sy,
                Rotation = srot,
                MirrorX = mirrorX,
                MirrorY = mirrorY,
                Unit = unit,
                Uuid = uuid
            };

            // Parse Properties: Reference, Value, Footprint
            foreach (var prop in symNode.FindAllSub("property"))
            {
                if (prop.Count < 3) continue;
                string propName = prop.GetString(1);
                string propVal = prop.GetString(2);

                var propAt = prop.FindSub("at");
                double px = propAt?.GetDouble(1, sx) ?? sx;
                double py = propAt?.GetDouble(2, sy) ?? sy;
                double prot = propAt?.GetDouble(3, 0) ?? 0;
                bool hide = prop.FindSub("hide")?.GetString(1) == "yes" ||
                            prop.FindSub("effects")?.FindSub("hide")?.GetString(1) == "yes";

                if (propName.Equals("Reference", StringComparison.OrdinalIgnoreCase))
                {
                    symbol.Reference = propVal;
                    symbol.RefX = px;
                    symbol.RefY = py;
                    symbol.RefRotation = prot;
                    symbol.RefHide = hide;
                }
                else if (propName.Equals("Value", StringComparison.OrdinalIgnoreCase))
                {
                    symbol.Value = propVal;
                    symbol.ValX = px;
                    symbol.ValY = py;
                    symbol.ValRotation = prot;
                    symbol.ValHide = hide;
                }
                else if (propName.Equals("Footprint", StringComparison.OrdinalIgnoreCase))
                {
                    symbol.Footprint = propVal;
                }
            }

            // Resolve world space pins and primitives from library symbol
            ResolveSymbolWorldElements(symbol, libSymbols);
            list.Add(symbol);
        }

        return list;
    }

    private static void ResolveSymbolWorldElements(SchSymbol symbol, Dictionary<string, SchLibSymbol> libSymbols)
    {
        if (!libSymbols.TryGetValue(symbol.LibId, out var libSym))
            return;

        // Collect primitives for unit 0 (common) and specific unit
        var rawPrimitives = new List<SchPrimitive>();
        if (libSym.PrimitivesByUnit.TryGetValue(0, out var commPrims)) rawPrimitives.AddRange(commPrims);
        if (symbol.Unit > 0 && libSym.PrimitivesByUnit.TryGetValue(symbol.Unit, out var unitPrims)) rawPrimitives.AddRange(unitPrims);
        if (rawPrimitives.Count == 0) rawPrimitives.AddRange(libSym.Primitives);

        // Collect pins for unit 0 and specific unit
        var rawPins = new List<SchPin>();
        if (libSym.PinsByUnit.TryGetValue(0, out var commPins)) rawPins.AddRange(commPins);
        if (symbol.Unit > 0 && libSym.PinsByUnit.TryGetValue(symbol.Unit, out var unitPins)) rawPins.AddRange(unitPins);
        if (rawPins.Count == 0) rawPins.AddRange(libSym.Pins);

        double minX = symbol.X, maxX = symbol.X;
        double minY = symbol.Y, maxY = symbol.Y;

        // Transform primitives to world space
        foreach (var prim in rawPrimitives)
        {
            if (prim is SchPolyline pl)
            {
                var worldPts = new List<SchPoint>(pl.Points.Count);
                foreach (var pt in pl.Points)
                {
                    var wpt = TransformPoint(pt.X, pt.Y, symbol.X, symbol.Y, symbol.Rotation, symbol.MirrorX, symbol.MirrorY);
                    worldPts.Add(wpt);
                    minX = Math.Min(minX, wpt.X); maxX = Math.Max(maxX, wpt.X);
                    minY = Math.Min(minY, wpt.Y); maxY = Math.Max(maxY, wpt.Y);
                }
                symbol.WorldPrimitives.Add(new SchPolyline
                {
                    Points = worldPts,
                    StrokeWidth = pl.StrokeWidth,
                    FillType = pl.FillType
                });
            }
            else if (prim is SchRectangle rect)
            {
                var pt1 = TransformPoint(rect.StartX, rect.StartY, symbol.X, symbol.Y, symbol.Rotation, symbol.MirrorX, symbol.MirrorY);
                var pt2 = TransformPoint(rect.EndX, rect.EndY, symbol.X, symbol.Y, symbol.Rotation, symbol.MirrorX, symbol.MirrorY);

                minX = Math.Min(minX, Math.Min(pt1.X, pt2.X)); maxX = Math.Max(maxX, Math.Max(pt1.X, pt2.X));
                minY = Math.Min(minY, Math.Min(pt1.Y, pt2.Y)); maxY = Math.Max(maxY, Math.Max(pt1.Y, pt2.Y));

                symbol.WorldPrimitives.Add(new SchRectangle
                {
                    StartX = pt1.X,
                    StartY = pt1.Y,
                    EndX = pt2.X,
                    EndY = pt2.Y,
                    StrokeWidth = rect.StrokeWidth,
                    FillType = rect.FillType
                });
            }
            else if (prim is SchCircle circle)
            {
                var c = TransformPoint(circle.CenterX, circle.CenterY, symbol.X, symbol.Y, symbol.Rotation, symbol.MirrorX, symbol.MirrorY);
                minX = Math.Min(minX, c.X - circle.Radius); maxX = Math.Max(maxX, c.X + circle.Radius);
                minY = Math.Min(minY, c.Y - circle.Radius); maxY = Math.Max(maxY, c.Y + circle.Radius);

                symbol.WorldPrimitives.Add(new SchCircle
                {
                    CenterX = c.X,
                    CenterY = c.Y,
                    Radius = circle.Radius,
                    StrokeWidth = circle.StrokeWidth,
                    FillType = circle.FillType
                });
            }
            else if (prim is SchArc arc)
            {
                var s = TransformPoint(arc.StartX, arc.StartY, symbol.X, symbol.Y, symbol.Rotation, symbol.MirrorX, symbol.MirrorY);
                var m = TransformPoint(arc.MidX, arc.MidY, symbol.X, symbol.Y, symbol.Rotation, symbol.MirrorX, symbol.MirrorY);
                var e = TransformPoint(arc.EndX, arc.EndY, symbol.X, symbol.Y, symbol.Rotation, symbol.MirrorX, symbol.MirrorY);

                minX = Math.Min(minX, Math.Min(s.X, Math.Min(m.X, e.X)));
                maxX = Math.Max(maxX, Math.Max(s.X, Math.Max(m.X, e.X)));
                minY = Math.Min(minY, Math.Min(s.Y, Math.Min(m.Y, e.Y)));
                maxY = Math.Max(maxY, Math.Max(s.Y, Math.Max(m.Y, e.Y)));

                symbol.WorldPrimitives.Add(new SchArc
                {
                    StartX = s.X,
                    StartY = s.Y,
                    MidX = m.X,
                    MidY = m.Y,
                    EndX = e.X,
                    EndY = e.Y,
                    StrokeWidth = arc.StrokeWidth,
                    FillType = arc.FillType
                });
            }
        }

        // Transform pins to world space
        foreach (var pin in rawPins)
        {
            double pinRad = pin.Rotation * (Math.PI / 180.0);
            double targetLx = pin.X + pin.Length * Math.Cos(pinRad);
            double targetLy = pin.Y + pin.Length * Math.Sin(pinRad);

            var pinPos = TransformPoint(pin.X, pin.Y, symbol.X, symbol.Y, symbol.Rotation, symbol.MirrorX, symbol.MirrorY);
            var targetPos = TransformPoint(targetLx, targetLy, symbol.X, symbol.Y, symbol.Rotation, symbol.MirrorX, symbol.MirrorY);
            double dirX = targetPos.X - pinPos.X;
            double dirY = targetPos.Y - pinPos.Y;
            double totalRot = (pin.Rotation + symbol.Rotation) % 360.0;

            minX = Math.Min(minX, Math.Min(pinPos.X, targetPos.X)); maxX = Math.Max(maxX, Math.Max(pinPos.X, targetPos.X));
            minY = Math.Min(minY, Math.Min(pinPos.Y, targetPos.Y)); maxY = Math.Max(maxY, Math.Max(pinPos.Y, targetPos.Y));

            symbol.WorldPins.Add(new SchPin
            {
                Number = pin.Number,
                Name = pin.Name,
                Type = pin.Type,
                X = pinPos.X,
                Y = pinPos.Y,
                TargetX = targetPos.X,
                TargetY = targetPos.Y,
                DirX = dirX,
                DirY = dirY,
                Rotation = totalRot,
                Length = pin.Length,
                HideNumber = pin.HideNumber,
                HideName = pin.HideName
            });
        }

        // Add padding around symbol bounding box
        symbol.MinX = minX - 0.5;
        symbol.MinY = minY - 0.5;
        symbol.MaxX = maxX + 0.5;
        symbol.MaxY = maxY + 0.5;
    }

    private static SchPoint TransformPoint(double lx, double ly, double posX, double posY, double rotDeg, bool mirrorX, bool mirrorY)
    {
        // KiCad Symbol library space is Cartesian (+Y is UP).
        // KiCad Schematic page / Skia canvas space is Screen (+Y is DOWN).
        // Base mapping: x0 = lx, y0 = -ly
        // (mirror x) flips across X axis (inverts X)
        // (mirror y) flips across Y axis (inverts Y)
        double x0 = mirrorX ? -lx : lx;
        double y0 = mirrorY ? ly : -ly;

        double rx = x0;
        double ry = y0;

        if (Math.Abs(rotDeg) > 0.001)
        {
            // KiCad S-expression rotation is counter-clockwise in degrees.
            // In screen coordinates (+X right, +Y down), a CCW rotation by angle rotDeg:
            double rad = rotDeg * (Math.PI / 180.0);
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);
            rx = x0 * cos + y0 * sin;
            ry = -x0 * sin + y0 * cos;
        }

        return new SchPoint(posX + rx, posY + ry);
    }

    private List<SchWire> ParseWires()
    {
        var list = new List<SchWire>();
        foreach (var wNode in _tree.FindAllSub("wire"))
        {
            var ptsNode = wNode.FindSub("pts");
            if (ptsNode is null) continue;

            var pts = new List<SchPoint>();
            foreach (var xy in ptsNode.FindAllSub("xy"))
            {
                if (xy.Count >= 3)
                    pts.Add(new SchPoint(xy.GetDouble(1), xy.GetDouble(2)));
            }

            if (pts.Count < 2) continue;

            double strokeW = wNode.FindSub("stroke")?.FindSub("width")?.GetDouble(1, 0.15) ?? 0.15;
            string uuid = wNode.FindSub("uuid")?.GetString(1) ?? "";

            list.Add(new SchWire
            {
                Points = pts,
                StrokeWidth = strokeW,
                Uuid = uuid
            });
        }
        return list;
    }

    private List<SchBus> ParseBuses()
    {
        var list = new List<SchBus>();
        foreach (var bNode in _tree.FindAllSub("bus"))
        {
            var ptsNode = bNode.FindSub("pts");
            if (ptsNode is null) continue;

            var pts = new List<SchPoint>();
            foreach (var xy in ptsNode.FindAllSub("xy"))
            {
                if (xy.Count >= 3)
                    pts.Add(new SchPoint(xy.GetDouble(1), xy.GetDouble(2)));
            }

            if (pts.Count < 2) continue;

            double strokeW = bNode.FindSub("stroke")?.FindSub("width")?.GetDouble(1, 0.4) ?? 0.4;
            string uuid = bNode.FindSub("uuid")?.GetString(1) ?? "";

            list.Add(new SchBus
            {
                Points = pts,
                StrokeWidth = strokeW,
                Uuid = uuid
            });
        }
        return list;
    }

    private List<SchJunction> ParseJunctions()
    {
        var list = new List<SchJunction>();
        foreach (var jNode in _tree.FindAllSub("junction"))
        {
            var pAt = jNode.FindSub("at");
            if (pAt is null || pAt.Count < 3) continue;

            double jx = pAt.GetDouble(1);
            double jy = pAt.GetDouble(2);
            double diam = jNode.FindSub("diameter")?.GetDouble(1, 1.0) ?? 1.0;
            if (diam <= 0.01) diam = 1.0; // default junction dot diameter

            string uuid = jNode.FindSub("uuid")?.GetString(1) ?? "";

            list.Add(new SchJunction
            {
                X = jx,
                Y = jy,
                Diameter = diam,
                Uuid = uuid
            });
        }
        return list;
    }

    private List<SchLabel> ParseLabels()
    {
        var list = new List<SchLabel>();

        // 1. Local labels
        foreach (var lNode in _tree.FindAllSub("label"))
        {
            if (lNode.Count < 2) continue;
            string text = lNode.GetString(1);
            var pAt = lNode.FindSub("at");
            double lx = pAt?.GetDouble(1, 0) ?? 0;
            double ly = pAt?.GetDouble(2, 0) ?? 0;
            double lrot = pAt?.GetDouble(3, 0) ?? 0;
            double lsize = lNode.FindSub("effects")?.FindSub("font")?.FindSub("size")?.GetDouble(1, 1.27) ?? 1.27;
            string uuid = lNode.FindSub("uuid")?.GetString(1) ?? "";

            list.Add(new SchLabel
            {
                Text = text,
                Kind = "local",
                Shape = "passive",
                X = lx,
                Y = ly,
                Rotation = lrot,
                Size = lsize,
                Uuid = uuid
            });
        }

        // 2. Global labels
        foreach (var gNode in _tree.FindAllSub("global_label"))
        {
            if (gNode.Count < 2) continue;
            string text = gNode.GetString(1);
            string shape = gNode.FindSub("shape")?.GetString(1) ?? "passive";
            var pAt = gNode.FindSub("at");
            double lx = pAt?.GetDouble(1, 0) ?? 0;
            double ly = pAt?.GetDouble(2, 0) ?? 0;
            double lrot = pAt?.GetDouble(3, 0) ?? 0;
            double lsize = gNode.FindSub("effects")?.FindSub("font")?.FindSub("size")?.GetDouble(1, 1.27) ?? 1.27;
            string uuid = gNode.FindSub("uuid")?.GetString(1) ?? "";

            list.Add(new SchLabel
            {
                Text = text,
                Kind = "global",
                Shape = shape,
                X = lx,
                Y = ly,
                Rotation = lrot,
                Size = lsize,
                Uuid = uuid
            });
        }

        // 3. Hierarchical labels
        foreach (var hNode in _tree.FindAllSub("hierarchical_label"))
        {
            if (hNode.Count < 2) continue;
            string text = hNode.GetString(1);
            string shape = hNode.FindSub("shape")?.GetString(1) ?? "passive";
            var pAt = hNode.FindSub("at");
            double lx = pAt?.GetDouble(1, 0) ?? 0;
            double ly = pAt?.GetDouble(2, 0) ?? 0;
            double lrot = pAt?.GetDouble(3, 0) ?? 0;
            double lsize = hNode.FindSub("effects")?.FindSub("font")?.FindSub("size")?.GetDouble(1, 1.27) ?? 1.27;
            string uuid = hNode.FindSub("uuid")?.GetString(1) ?? "";

            list.Add(new SchLabel
            {
                Text = text,
                Kind = "hierarchical",
                Shape = shape,
                X = lx,
                Y = ly,
                Rotation = lrot,
                Size = lsize,
                Uuid = uuid
            });
        }

        return list;
    }

    private List<SchSheetRef> ParseSheets()
    {
        var list = new List<SchSheetRef>();
        foreach (var sNode in _tree.FindAllSub("sheet"))
        {
            var pAt = sNode.FindSub("at");
            var pSize = sNode.FindSub("size");
            double sx = pAt?.GetDouble(1, 0) ?? 0;
            double sy = pAt?.GetDouble(2, 0) ?? 0;
            double sw = pSize?.GetDouble(1, 0) ?? 0;
            double sh = pSize?.GetDouble(2, 0) ?? 0;
            string uuid = sNode.FindSub("uuid")?.GetString(1) ?? "";

            string sheetName = "";
            string sheetFile = "";

            foreach (var prop in sNode.FindAllSub("property"))
            {
                if (prop.Count < 3) continue;
                string pName = prop.GetString(1);
                string pVal = prop.GetString(2);

                if (pName.Equals("Sheetname", StringComparison.OrdinalIgnoreCase))
                    sheetName = pVal;
                else if (pName.Equals("Sheetfile", StringComparison.OrdinalIgnoreCase))
                    sheetFile = pVal;
            }

            list.Add(new SchSheetRef
            {
                Name = string.IsNullOrWhiteSpace(sheetName) ? sheetFile : sheetName,
                FilePath = sheetFile,
                X = sx,
                Y = sy,
                Width = sw,
                Height = sh,
                Uuid = uuid
            });
        }
        return list;
    }

    private List<SchPoint> ParseNoConnects()
    {
        var list = new List<SchPoint>();
        foreach (var ncNode in _tree.FindAllSub("no_connect"))
        {
            var pAt = ncNode.FindSub("at");
            if (pAt is not null && pAt.Count >= 3)
            {
                list.Add(new SchPoint(pAt.GetDouble(1), pAt.GetDouble(2)));
            }
        }
        return list;
    }

    private static void ComputeBoundingBox(KiCadSchematic sch)
    {
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        void Expand(double x, double y)
        {
            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);
        }

        foreach (var sym in sch.Symbols)
        {
            Expand(sym.MinX, sym.MinY);
            Expand(sym.MaxX, sym.MaxY);
        }

        foreach (var wire in sch.Wires)
        {
            foreach (var pt in wire.Points) Expand(pt.X, pt.Y);
        }

        foreach (var bus in sch.Buses)
        {
            foreach (var pt in bus.Points) Expand(pt.X, pt.Y);
        }

        foreach (var junc in sch.Junctions)
        {
            Expand(junc.X, junc.Y);
        }

        foreach (var lbl in sch.Labels)
        {
            Expand(lbl.X, lbl.Y);
        }

        foreach (var sheet in sch.Sheets)
        {
            Expand(sheet.X, sheet.Y);
            Expand(sheet.X + sheet.Width, sheet.Y + sheet.Height);
        }

        if (minX == double.MaxValue)
        {
            minX = 0; minY = 0; maxX = 297; maxY = 210; // Default A4
        }

        sch.MinX = minX;
        sch.MinY = minY;
        sch.MaxX = maxX;
        sch.MaxY = maxY;
    }
}
