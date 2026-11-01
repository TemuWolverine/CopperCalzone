using CopperCalzone.Models;

namespace CopperCalzone.Parsing;

public sealed class KiCadPcbParser
{
    private readonly NetResolver _netResolver = new();
    private readonly SExpNode _tree;

    public KiCadPcbParser(string content)
    {
        _tree = SExpressionParser.Parse(content.AsSpan());
        if (!_tree.IsList || _tree.Tag != "kicad_pcb")
            throw new InvalidDataException("Input file is not a valid KiCad PCB (.kicad_pcb) file.");

        foreach (var netItem in _tree.FindAllSub("net"))
        {
            _netResolver.Resolve(netItem);
        }
    }

    public List<PcbGraphic> ExtractDrawings()
    {
        var drawings = new List<PcbGraphic>();
        string[] tags = ["gr_line", "gr_arc", "gr_circle", "gr_rect", "gr_poly", "gr_text"];

        foreach (string tag in tags)
        {
            foreach (var item in _tree.FindAllSub(tag))
            {
                var pLayer = item.FindSub("layer");
                if (pLayer is null || pLayer.Count < 2) continue;
                string layerName = pLayer.GetString(1);

                if (tag == "gr_text")
                {
                    string textContent = item.Count > 1 ? item.GetString(1) : "";
                    var pAt = item.FindSub("at");
                    double tx = pAt is not null && pAt.Count > 1 ? pAt.GetDouble(1) : 0.0;
                    double ty = pAt is not null && pAt.Count > 2 ? pAt.GetDouble(2) : 0.0;
                    double trot = pAt is not null && pAt.Count > 3 ? pAt.GetDouble(3) : 0.0;

                    double fontSize = 1.0;
                    var sizeNode = item.FindSub("effects")?.FindSub("font")?.FindSub("size");
                    if (sizeNode is not null && sizeNode.Count > 1)
                        fontSize = sizeNode.GetDouble(1, 1.0);

                    drawings.Add(new PcbGraphic
                    {
                        Type = "gr_text",
                        Layer = layerName,
                        Text = textContent,
                        X = Math.Round(tx, 4),
                        Y = Math.Round(ty, 4),
                        Rot = Math.Round(trot, 2),
                        Size = Math.Round(fontSize, 3)
                    });
                    continue;
                }

                double w = 0.1;
                var stroke = item.FindSub("stroke");
                if (stroke is not null)
                {
                    var pW = stroke.FindSub("width");
                    if (pW is not null && pW.Count > 1) w = pW.GetDouble(1, 0.1);
                }
                else
                {
                    var pW = item.FindSub("width");
                    if (pW is not null && pW.Count > 1) w = pW.GetDouble(1, 0.1);
                }

                var obj = new PcbGraphic
                {
                    Type = tag,
                    Layer = layerName,
                    Width = Math.Round(w, 4)
                };

                var pStart = item.FindSub("start");
                var pEnd = item.FindSub("end");
                var pMid = item.FindSub("mid");
                var pCenter = item.FindSub("center");

                if (pStart is not null && pStart.Count > 2) obj.Start = [Math.Round(pStart.GetDouble(1), 4), Math.Round(pStart.GetDouble(2), 4)];
                if (pEnd is not null && pEnd.Count > 2) obj.End = [Math.Round(pEnd.GetDouble(1), 4), Math.Round(pEnd.GetDouble(2), 4)];
                if (pMid is not null && pMid.Count > 2) obj.Mid = [Math.Round(pMid.GetDouble(1), 4), Math.Round(pMid.GetDouble(2), 4)];
                if (pCenter is not null && pCenter.Count > 2) obj.Center = [Math.Round(pCenter.GetDouble(1), 4), Math.Round(pCenter.GetDouble(2), 4)];

                var ptsNode = item.FindSub("pts");
                if (ptsNode is not null)
                {
                    var pts = new List<double[]>();
                    foreach (var xy in ptsNode.FindAllSub("xy"))
                    {
                        if (xy.Count >= 3)
                            pts.Add([Math.Round(xy.GetDouble(1), 4), Math.Round(xy.GetDouble(2), 4)]);
                    }
                    if (pts.Count > 0)
                        obj.Pts = pts;
                }

                var fillNode = item.FindSub("fill");
                if (fillNode is not null && fillNode.Count > 1)
                    obj.Fill = fillNode.GetString(1);
                else if (tag == "gr_poly")
                    obj.Fill = "yes";

                drawings.Add(obj);
            }
        }

        return drawings;
    }

    public (List<PcbFootprint> Footprints, List<PcbPad> Pads) ExtractFootprintsAndPads()
    {
        var footprints = new List<PcbFootprint>();
        var allPads = new List<PcbPad>();

        var fpNodes = _tree.FindAllSub("footprint");
        if (fpNodes.Count == 0)
        {
            fpNodes = _tree.FindAllSub("module");
        }

        foreach (var fpNode in fpNodes)
        {
            string fpPackage = fpNode.Count > 1 ? fpNode.GetString(1) : "";
            int colonIdx = fpPackage.IndexOf(':');
            string cleanPkg = colonIdx >= 0 ? fpPackage[(colonIdx + 1)..] : fpPackage;

            var atNode = fpNode.FindSub("at");
            double fpX = atNode is not null && atNode.Count > 1 ? atNode.GetDouble(1) : 0.0;
            double fpY = atNode is not null && atNode.Count > 2 ? atNode.GetDouble(2) : 0.0;
            double fpRot = atNode is not null && atNode.Count > 3 ? atNode.GetDouble(3) : 0.0;

            var layerNode = fpNode.FindSub("layer");
            string fpLayer = layerNode is not null && layerNode.Count > 1 ? layerNode.GetString(1, "F.Cu") : "F.Cu";

            string reference = "";
            string val = "";
            foreach (var prop in fpNode.FindAllSub("property"))
            {
                if (prop.Count >= 3)
                {
                    string key = prop.GetString(1);
                    if (key == "Reference") reference = prop.GetString(2);
                    else if (key == "Value") val = prop.GetString(2);
                    else if (key == "Footprint" && string.IsNullOrEmpty(cleanPkg))
                    {
                        string pVal = prop.GetString(2);
                        int pColon = pVal.IndexOf(':');
                        cleanPkg = pColon >= 0 ? pVal[(pColon + 1)..] : pVal;
                    }
                }
            }
            if (string.IsNullOrEmpty(reference))
            {
                foreach (var fpt in fpNode.FindAllSub("fp_text"))
                {
                    if (fpt.Count >= 3)
                    {
                        string key = fpt.GetString(1);
                        if (key == "reference") reference = fpt.GetString(2);
                        else if (key == "value") val = fpt.GetString(2);
                    }
                }
            }

            int fpPadCount = 0;
            foreach (var padNode in fpNode.FindAllSub("pad"))
            {
                string padNum = padNode.Count > 1 ? padNode.GetString(1) : "";
                string padType = padNode.Count > 2 ? padNode.GetString(2) : "";
                string padShape = padNode.Count > 3 ? padNode.GetString(3, "rect") : "rect";

                var pAt = padNode.FindSub("at");
                double dx = pAt is not null && pAt.Count > 1 ? pAt.GetDouble(1) : 0.0;
                double dy = pAt is not null && pAt.Count > 2 ? pAt.GetDouble(2) : 0.0;
                double padLocalRot = pAt is not null && pAt.Count > 3 ? pAt.GetDouble(3) : 0.0;

                var pSize = padNode.FindSub("size");
                double w = pSize is not null && pSize.Count > 1 ? pSize.GetDouble(1, 1.0) : 1.0;
                double h = pSize is not null && pSize.Count > 2 ? pSize.GetDouble(2, 1.0) : 1.0;

                var pDrill = padNode.FindSub("drill");
                double drill = 0.0, drillW = 0.0, drillH = 0.0;
                if (pDrill is not null && pDrill.Count > 1)
                {
                    if (pDrill.GetString(1) == "oval" && pDrill.Count > 3)
                    {
                        drillW = pDrill.GetDouble(2);
                        drillH = pDrill.GetDouble(3);
                        drill = Math.Max(drillW, drillH);
                    }
                    else
                    {
                        drill = pDrill.GetDouble(1);
                        drillW = drill;
                        drillH = drill;
                    }
                }

                var pLayers = padNode.FindSub("layers");
                var layers = new List<string>();
                if (pLayers?.List is not null && pLayers.Count > 1)
                {
                    for (int i = 1; i < pLayers.Count; i++)
                        layers.Add(pLayers.GetString(i));
                }
                else
                {
                    layers.Add("F.Cu");
                }

                var pNet = padNode.FindSub("net");
                var (netId, netName) = _netResolver.Resolve(pNet);

                var pFn = padNode.FindSub("pinfunction");
                string pinFn = pFn is not null && pFn.Count > 1 ? pFn.GetString(1) : "";

                var pType = padNode.FindSub("pintype");
                string pinType = pType is not null && pType.Count > 1 ? pType.GetString(1) : "";

                var pRratio = padNode.FindSub("roundrect_rratio");
                double rratio = pRratio is not null && pRratio.Count > 1 ? pRratio.GetDouble(1, 0.25) : 0.25;

                // KiCad .kicad_pcb footprint sub-elements are already flipped for B.Cu
                var (rx, ry) = RotatePoint(dx, dy, fpRot);
                double worldX = fpX + rx;
                double worldY = fpY + ry;
                double totalRot = Mod360(fpRot + padLocalRot);

                var padObj = new PcbPad
                {
                    FpRef = reference,
                    FpVal = val,
                    FpLayer = fpLayer,
                    Num = padNum,
                    Fn = pinFn,
                    PinType = pinType,
                    Type = padType,
                    Shape = padShape,
                    W = Math.Round(w, 4),
                    H = Math.Round(h, 4),
                    Drill = Math.Round(drill, 4),
                    DrillW = Math.Round(drillW, 4),
                    DrillH = Math.Round(drillH, 4),
                    Rratio = Math.Round(rratio, 3),
                    Layers = layers,
                    Net = netId,
                    NetName = netName,
                    X = Math.Round(worldX, 4),
                    Y = Math.Round(worldY, 4),
                    Rot = Math.Round(totalRot, 2)
                };
                fpPadCount++;
                allPads.Add(padObj);
            }

            double[] Trans(double px, double py)
            {
                var (rx, ry) = RotatePoint(px, py, fpRot);
                return [Math.Round(fpX + rx, 4), Math.Round(fpY + ry, 4)];
            }

            var fpDrawings = new List<PcbGraphic>();
            string[] dwgTags = ["fp_line", "fp_arc", "fp_circle", "fp_rect", "fp_poly"];
            foreach (string tag in dwgTags)
            {
                foreach (var dwg in fpNode.FindAllSub(tag))
                {
                    var dLayer = dwg.FindSub("layer");
                    string layerName = dLayer is not null && dLayer.Count > 1 ? dLayer.GetString(1) : "";
                    if (layerName is not ("F.SilkS" or "B.SilkS" or "F.Fab" or "B.Fab"))
                        continue;

                    double sw = 0.12;
                    var stroke = dwg.FindSub("stroke");
                    if (stroke is not null)
                    {
                        var wNode = stroke.FindSub("width");
                        if (wNode is not null && wNode.Count > 1) sw = wNode.GetDouble(1, 0.12);
                    }
                    else
                    {
                        var wNode = dwg.FindSub("width");
                        if (wNode is not null && wNode.Count > 1) sw = wNode.GetDouble(1, 0.12);
                    }

                    var pStart = dwg.FindSub("start");
                    var pEnd = dwg.FindSub("end");
                    var pMid = dwg.FindSub("mid");
                    var pCenter = dwg.FindSub("center");

                    var itemData = new PcbGraphic
                    {
                        Type = tag.Replace("fp_", "gr_"),
                        Layer = layerName,
                        Width = Math.Round(sw, 4)
                    };

                    if (pStart is not null && pStart.Count > 2) itemData.Start = Trans(pStart.GetDouble(1), pStart.GetDouble(2));
                    if (pEnd is not null && pEnd.Count > 2) itemData.End = Trans(pEnd.GetDouble(1), pEnd.GetDouble(2));
                    if (pMid is not null && pMid.Count > 2) itemData.Mid = Trans(pMid.GetDouble(1), pMid.GetDouble(2));
                    if (pCenter is not null && pCenter.Count > 2) itemData.Center = Trans(pCenter.GetDouble(1), pCenter.GetDouble(2));

                    if (tag == "fp_rect" && pStart is not null && pEnd is not null && pStart.Count > 2 && pEnd.Count > 2)
                    {
                        double x1 = pStart.GetDouble(1), y1 = pStart.GetDouble(2);
                        double x2 = pEnd.GetDouble(1), y2 = pEnd.GetDouble(2);
                        itemData.Type = "gr_poly";
                        itemData.Pts =
                        [
                            Trans(x1, y1),
                            Trans(x2, y1),
                            Trans(x2, y2),
                            Trans(x1, y2)
                        ];
                    }

                    var ptsNode = dwg.FindSub("pts");
                    if (ptsNode is not null)
                    {
                        var polyPts = new List<double[]>();
                        foreach (var xy in ptsNode.FindAllSub("xy"))
                        {
                            if (xy.Count >= 3)
                                polyPts.Add(Trans(xy.GetDouble(1), xy.GetDouble(2)));
                        }
                        if (polyPts.Count > 0)
                            itemData.Pts = polyPts;
                    }

                    var fillNode = dwg.FindSub("fill");
                    if (fillNode is not null && fillNode.Count > 1)
                        itemData.Fill = fillNode.GetString(1);
                    else if (tag == "fp_poly")
                        itemData.Fill = "yes";

                    fpDrawings.Add(itemData);
                }
            }

            var fpTexts = new List<PcbText>();
            void ExtractTextNodes(List<SExpNode> textNodes, bool skipVars)
            {
                foreach (var node in textNodes)
                {
                    if (node.Count < 3) continue;
                    string textVal = node.GetString(2);
                    if (string.IsNullOrEmpty(textVal)) continue;
                    if (skipVars && textVal.StartsWith("${", StringComparison.Ordinal)) continue;

                    var pLayer = node.FindSub("layer");
                    string layerName = pLayer is not null && pLayer.Count > 1 ? pLayer.GetString(1) : "";
                    if (layerName is not ("F.SilkS" or "B.SilkS" or "F.Fab" or "B.Fab")) continue;

                    var hideNode = node.FindSub("hide");
                    bool isHidden = hideNode is not null && (hideNode.Count == 1 || hideNode.GetString(1) is "yes" or "true");
                    if (isHidden) continue;

                    var pAt = node.FindSub("at");
                    double tx = pAt is not null && pAt.Count > 1 ? pAt.GetDouble(1) : 0.0;
                    double ty = pAt is not null && pAt.Count > 2 ? pAt.GetDouble(2) : 0.0;
                    double trot = pAt is not null && pAt.Count > 3 ? pAt.GetDouble(3) : 0.0;

                    var (rx, ry) = RotatePoint(tx, ty, fpRot);

                    double fontSize = 1.0;
                    var effects = node.FindSub("effects")?.FindSub("font")?.FindSub("size");
                    if (effects is not null && effects.Count > 1)
                        fontSize = effects.GetDouble(1, 1.0);

                    fpTexts.Add(new PcbText
                    {
                        Text = textVal,
                        Layer = layerName,
                        X = Math.Round(fpX + rx, 4),
                        Y = Math.Round(fpY + ry, 4),
                        Rot = Math.Round(Mod360(fpRot + trot), 2),
                        Size = Math.Round(fontSize, 3)
                    });
                }
            }

            ExtractTextNodes(fpNode.FindAllSub("property"), false);
            ExtractTextNodes(fpNode.FindAllSub("fp_text"), true);

            footprints.Add(new PcbFootprint
            {
                Ref = reference,
                Val = val,
                Package = cleanPkg,
                Layer = fpLayer,
                X = Math.Round(fpX, 4),
                Y = Math.Round(fpY, 4),
                Rot = Math.Round(fpRot, 2),
                Drawings = fpDrawings,
                Texts = fpTexts,
                PadCount = fpPadCount
            });
        }

        return (footprints, allPads);
    }

    public List<PcbLayer> ExtractLayers()
    {
        var layers = new List<PcbLayer>();
        var layersNode = _tree.FindSub("layers");
        if (layersNode?.List is null) return layers;

        for (int i = 1; i < layersNode.List.Count; i++)
        {
            var item = layersNode.List[i];
            if (item.IsList && item.Count >= 3)
            {
                object? layerId = item.GetInt(0) is int idInt ? idInt : item.GetString(0);
                string canonicalName = item.GetString(1);
                string layerType = item.GetString(2);
                string userName = item.Count > 3 ? item.GetString(3, canonicalName) : canonicalName;
                layers.Add(new PcbLayer
                {
                    Id = layerId,
                    Name = canonicalName,
                    Type = layerType,
                    UserName = userName
                });
            }
        }
        return layers;
    }

    public BoardMetadata ExtractMetadata()
    {
        double thickness = 1.6;
        var general = _tree.FindSub("general");
        if (general is not null)
        {
            var thNode = general.FindSub("thickness");
            if (thNode is not null && thNode.Count > 1)
                thickness = thNode.GetDouble(1, 1.6);
        }

        string title = "", date = "", rev = "", company = "";
        var tb = _tree.FindSub("title_block");
        if (tb is not null)
        {
            var t = tb.FindSub("title");
            if (t is not null && t.Count > 1) title = t.GetString(1);
            var d = tb.FindSub("date");
            if (d is not null && d.Count > 1) date = d.GetString(1);
            var r = tb.FindSub("rev");
            if (r is not null && r.Count > 1) rev = r.GetString(1);
            var c = tb.FindSub("company");
            if (c is not null && c.Count > 1) company = c.GetString(1);
        }

        var genNode = _tree.FindSub("generator");
        string generator = genNode is not null && genNode.Count > 1 ? genNode.GetString(1, "pcbnew") : "pcbnew";

        return new BoardMetadata
        {
            Title = title,
            Date = date,
            Rev = rev,
            Company = company,
            Thickness = thickness,
            Generator = generator
        };
    }

    public List<PcbTrack> ExtractTracks()
    {
        var tracks = new List<PcbTrack>();

        foreach (var item in _tree.FindAllSub("segment"))
        {
            var pStart = item.FindSub("start");
            var pEnd = item.FindSub("end");
            var pW = item.FindSub("width");
            var pLayer = item.FindSub("layer");
            var pNet = item.FindSub("net");
            if (pStart is not null && pEnd is not null && pLayer is not null && pStart.Count > 2 && pEnd.Count > 2)
            {
                var (netId, _) = _netResolver.Resolve(pNet);
                tracks.Add(new PcbTrack
                {
                    Type = "segment",
                    Start = [Math.Round(pStart.GetDouble(1), 4), Math.Round(pStart.GetDouble(2), 4)],
                    End = [Math.Round(pEnd.GetDouble(1), 4), Math.Round(pEnd.GetDouble(2), 4)],
                    Width = Math.Round(pW is not null && pW.Count > 1 ? pW.GetDouble(1, 0.25) : 0.25, 4),
                    Layer = pLayer.GetString(1),
                    Net = netId
                });
            }
        }

        foreach (var item in _tree.FindAllSub("arc"))
        {
            var pStart = item.FindSub("start");
            var pMid = item.FindSub("mid");
            var pEnd = item.FindSub("end");
            var pW = item.FindSub("width");
            var pLayer = item.FindSub("layer");
            var pNet = item.FindSub("net");
            if (pStart is not null && pMid is not null && pEnd is not null && pLayer is not null &&
                pStart.Count > 2 && pMid.Count > 2 && pEnd.Count > 2)
            {
                var (netId, _) = _netResolver.Resolve(pNet);
                tracks.Add(new PcbTrack
                {
                    Type = "arc",
                    Start = [Math.Round(pStart.GetDouble(1), 4), Math.Round(pStart.GetDouble(2), 4)],
                    Mid = [Math.Round(pMid.GetDouble(1), 4), Math.Round(pMid.GetDouble(2), 4)],
                    End = [Math.Round(pEnd.GetDouble(1), 4), Math.Round(pEnd.GetDouble(2), 4)],
                    Width = Math.Round(pW is not null && pW.Count > 1 ? pW.GetDouble(1, 0.25) : 0.25, 4),
                    Layer = pLayer.GetString(1),
                    Net = netId
                });
            }
        }

        return tracks;
    }

    public List<PcbVia> ExtractVias()
    {
        var vias = new List<PcbVia>();
        foreach (var item in _tree.FindAllSub("via"))
        {
            var pAt = item.FindSub("at");
            var pSize = item.FindSub("size");
            var pDrill = item.FindSub("drill");
            var pLayers = item.FindSub("layers");
            var pNet = item.FindSub("net");
            if (pAt is not null && pAt.Count > 2)
            {
                var layers = new List<string>();
                if (pLayers is not null && pLayers.Count > 1)
                {
                    for (int i = 1; i < pLayers.Count; i++)
                        layers.Add(pLayers.GetString(i));
                }
                else
                {
                    layers.Add("F.Cu");
                    layers.Add("B.Cu");
                }

                var (netId, _) = _netResolver.Resolve(pNet);
                vias.Add(new PcbVia
                {
                    X = Math.Round(pAt.GetDouble(1), 4),
                    Y = Math.Round(pAt.GetDouble(2), 4),
                    Size = Math.Round(pSize is not null && pSize.Count > 1 ? pSize.GetDouble(1, 0.6) : 0.6, 4),
                    Drill = Math.Round(pDrill is not null && pDrill.Count > 1 ? pDrill.GetDouble(1, 0.3) : 0.3, 4),
                    Layers = layers,
                    Net = netId
                });
            }
        }
        return vias;
    }

    public List<PcbZone> ExtractZones()
    {
        var zones = new List<PcbZone>();
        foreach (var item in _tree.FindAllSub("zone"))
        {
            var pNet = item.FindSub("net");
            var pLayer = item.FindSub("layer");
            var pLayers = item.FindSub("layers");
            string layer = pLayer is not null && pLayer.Count > 1
                ? pLayer.GetString(1)
                : (pLayers is not null && pLayers.Count > 1 ? pLayers.GetString(1) : "F.Cu");

            var (netId, _) = _netResolver.Resolve(pNet);
            var filledPolys = new List<List<double[]>>();

            foreach (var fp in item.FindAllSub("filled_polygon"))
            {
                var ptsNode = fp.FindSub("pts");
                if (ptsNode is not null)
                {
                    var polyPts = new List<double[]>();
                    foreach (var xy in ptsNode.FindAllSub("xy"))
                    {
                        if (xy.Count >= 3)
                            polyPts.Add([Math.Round(xy.GetDouble(1), 4), Math.Round(xy.GetDouble(2), 4)]);
                    }
                    if (polyPts.Count > 0)
                        filledPolys.Add(polyPts);
                }
            }

            if (filledPolys.Count > 0)
            {
                zones.Add(new PcbZone
                {
                    Net = netId,
                    Layer = layer,
                    Polygons = filledPolys
                });
            }
        }
        return zones;
    }

    public PcbBoard ParseAll(string fallbackTitle = "KiCad Board")
    {
        var metadata = ExtractMetadata();
        if (string.IsNullOrWhiteSpace(metadata.Title))
            metadata.Title = fallbackTitle;

        var layers = ExtractLayers();
        var (footprints, pads) = ExtractFootprintsAndPads();
        var tracks = ExtractTracks();
        var vias = ExtractVias();
        var zones = ExtractZones();
        var drawings = ExtractDrawings();

        var edgePoints = new List<double[]>();
        foreach (var d in drawings)
        {
            if (d.Layer == "Edge.Cuts")
            {
                if (d.Start is not null) edgePoints.Add(d.Start);
                if (d.End is not null) edgePoints.Add(d.End);
                if (d.Mid is not null) edgePoints.Add(d.Mid);
                if (d.Pts is not null) edgePoints.AddRange(d.Pts);
            }
        }

        if (edgePoints.Count == 0)
        {
            foreach (var p in pads) edgePoints.Add([p.X, p.Y]);
            foreach (var t in tracks)
            {
                edgePoints.Add(t.Start);
                edgePoints.Add(t.End);
            }
        }

        double minX = 0.0, minY = 0.0, maxX = 100.0, maxY = 100.0;
        if (edgePoints.Count > 0)
        {
            minX = edgePoints.Min(pt => pt[0]);
            maxX = edgePoints.Max(pt => pt[0]);
            minY = edgePoints.Min(pt => pt[1]);
            maxY = edgePoints.Max(pt => pt[1]);
        }

        var netStats = new Dictionary<int, PcbNet>();
        foreach (var (id, name) in _netResolver.AllNets())
        {
            netStats[id] = new PcbNet
            {
                Id = id,
                Name = name
            };
        }

        foreach (var p in pads)
        {
            if (netStats.TryGetValue(p.Net, out var n))
                n.PadCount++;
        }

        foreach (var v in vias)
        {
            if (netStats.TryGetValue(v.Net, out var n))
                n.ViaCount++;
        }

        foreach (var t in tracks)
        {
            if (netStats.TryGetValue(t.Net, out var n))
            {
                n.TrackCount++;
                double dx = t.End[0] - t.Start[0];
                double dy = t.End[1] - t.Start[1];
                n.Length = Math.Round(n.Length + Math.Sqrt(dx * dx + dy * dy), 2);
            }
        }

        return new PcbBoard
        {
            Metadata = metadata,
            Bbox = new BoundingBox
            {
                MinX = Math.Round(minX, 4),
                MinY = Math.Round(minY, 4),
                MaxX = Math.Round(maxX, 4),
                MaxY = Math.Round(maxY, 4),
                Width = Math.Round(maxX - minX, 4),
                Height = Math.Round(maxY - minY, 4),
                CenterX = Math.Round((minX + maxX) / 2.0, 4),
                CenterY = Math.Round((minY + maxY) / 2.0, 4)
            },
            Layers = layers,
            Nets = netStats.Values.ToList(),
            Pads = pads,
            Footprints = footprints,
            Tracks = tracks,
            Vias = vias,
            Zones = zones,
            Drawings = drawings
        };
    }

    private static double Mod360(double angle)
    {
        double m = angle % 360.0;
        return m < 0 ? m + 360.0 : m;
    }

    private static (double X, double Y) RotatePoint(double x, double y, double angleDeg)
    {
        double rad = -angleDeg * (Math.PI / 180.0);
        double cosA = Math.Cos(rad);
        double sinA = Math.Sin(rad);
        return (x * cosA - y * sinA, x * sinA + y * cosA);
    }
}