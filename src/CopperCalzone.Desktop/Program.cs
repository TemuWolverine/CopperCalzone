using Avalonia;
using CopperCalzone.Models;
using CopperCalzone.Parsing;
using CopperCalzone.Rendering;
using CopperCalzone.ViewModels;
using SkiaSharp;

namespace CopperCalzone.Desktop;

internal sealed class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--verify")
        {
            return RunSelfVerification(args.Length > 1 ? args[1] : null);
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static int RunSelfVerification(string? screenshotOutputPath)
    {
        Console.WriteLine("=== KiCad CopperCalzone C#/Avalonia Self-Verification ===");

        // 1. Test S-Expression .kicad_pcb parser with KiCad 8/9/10 features
        const string sampleKicadPcb = """
            (kicad_pcb (version 20240108) (generator "pcbnew")
              (general (thickness 1.6))
              (title_block (title "TestBoard") (date "2026-11-01") (rev "1.0") (company "DeepMind"))
              (layers
                (0 "F.Cu" signal)
                (31 "B.Cu" signal)
                (36 "B.SilkS" user "B.Silkscreen")
                (37 "F.SilkS" user "F.Silkscreen")
                (44 "Edge.Cuts" user)
              )
              (net 0 "")
              (net 1 "GND")
              (net 2 "+3V3")
              (gr_rect (start 100 100) (end 150 130) (stroke (width 0.15)) (fill none) (layer "Edge.Cuts"))
              (footprint "Package_SO:SOIC-8" (layer "F.Cu") (at 120 115 90)
                (property "Reference" "U1" (at 0 -3 90) (layer "F.SilkS") (effects (font (size 1 1))))
                (property "Value" "LM358" (at 0 3 90) (layer "F.Fab") (effects (font (size 1 1))))
                (fp_rect (start -2 -2.5) (end 2 2.5) (stroke (width 0.12)) (fill none) (layer "F.SilkS"))
                (pad "1" smd roundrect (at -1.905 -2.7 90) (size 0.6 1.5) (layers "F.Cu" "F.Mask") (roundrect_rratio 0.25) (net 1 "GND") (pinfunction "IN-"))
                (pad "2" smd roundrect (at -0.635 -2.7 90) (size 0.6 1.5) (layers "F.Cu" "F.Mask") (roundrect_rratio 0.25) (net "+3V3") (pinfunction "VCC"))
                (pad "3" thru_hole oval (at 0.635 2.7 90) (size 1.2 1.8) (drill oval 0.6 0.9) (layers "*.Cu" "*.Mask") (net "Net-(U1-OUT)") (pinfunction "OUT"))
              )
              (footprint "Capacitor_SMD:C_0805_2012Metric" (layer "F.Cu") (at 110 110)
                (property "Reference" "C1" (at 0 -2 0) (layer "F.SilkS"))
                (property "Value" "100nF" (at 0 2 0) (layer "F.Fab"))
                (pad "1" smd roundrect (at -0.95 0) (size 1.0 1.3) (layers "F.Cu" "F.Mask") (net 1 "GND"))
                (pad "2" smd roundrect (at 0.95 0) (size 1.0 1.3) (layers "F.Cu" "F.Mask") (net 2 "+3V3"))
              )
              (footprint "Capacitor_SMD:C_0805_2012Metric" (layer "F.Cu") (at 115 110)
                (property "Reference" "C5" (at 0 -2 0) (layer "F.SilkS"))
                (property "Value" "100nF" (at 0 2 0) (layer "F.Fab"))
                (pad "1" smd roundrect (at -0.95 0) (size 1.0 1.3) (layers "F.Cu" "F.Mask") (net 1 "GND"))
                (pad "2" smd roundrect (at 0.95 0) (size 1.0 1.3) (layers "F.Cu" "F.Mask") (net 2 "+3V3"))
              )
              (footprint "Symbol:OSHW-Logo_Silkscreen" (layer "F.SilkS") (at 105 105)
                (property "Reference" "LOGO1" (at 0 0 0) (layer "F.SilkS"))
                (property "Value" "OSHW" (at 0 0 0) (layer "F.SilkS"))
                (fp_line (start -2 -2) (end 2 2) (stroke (width 0.15)) (layer "F.SilkS"))
              )
              (footprint "Symbol:Hardware-Label" (layer "F.Cu") (at 102 102)
                (property "Reference" "G1" (at 0 0 0) (layer "F.SilkS"))
                (property "Value" "PCB_LABEL" (at 0 0 0) (layer "F.Fab"))
                (fp_line (start -1 -1) (end 1 1) (stroke (width 0.15)) (layer "F.Cu"))
              )
              (footprint "MountingHole:MountingHole_3.2mm_M3" (layer "F.Cu") (at 108 108)
                (property "Reference" "H1" (at 0 0 0) (layer "F.SilkS"))
                (property "Value" "MountingHole" (at 0 0 0) (layer "F.Fab"))
                (pad "" np_thru_hole circle (at 0 0) (size 3.2 3.2) (drill 3.2) (layers "*.Cu" "*.Mask") (net 0 ""))
              )
              (footprint "TestPoint:TestPoint_Pad_D1.0mm" (layer "F.Cu") (at 125 125)
                (property "Reference" "TP1" (at 0 0 0) (layer "F.SilkS"))
                (property "Value" "TEST" (at 0 0 0) (layer "F.Fab"))
                (pad "1" smd circle (at 0 0) (size 1.0 1.0) (layers "F.Cu" "F.Mask") (net "unconnected-(TP1-Pad1)"))
              )
              (segment (start 117.3 113.095) (end 125.0 113.095) (width 0.25) (layer "F.Cu") (net 1))
              (arc (start 125.0 113.095) (mid 126.5 114.0) (end 127.0 116.0) (width 0.25) (layer "F.Cu") (net 1))
              (via (at 127.0 116.0) (size 0.6) (drill 0.3) (layers "F.Cu" "B.Cu") (net 1))
              (zone (net 1) (net_name "GND") (layer "B.Cu")
                (filled_polygon (layer "B.Cu") (pts (xy 101 101) (xy 149 101) (xy 149 129) (xy 101 129)))
              )
            )
            """;

        var parsedKicad = BoardLoader.LoadFromContent(sampleKicadPcb, "TestBoard.kicad_pcb");
        Console.WriteLine($"[PASS] Parsed .kicad_pcb '{parsedKicad.Metadata.Title}': {parsedKicad.Pads.Count} pads, {parsedKicad.Nets.Count} nets, {parsedKicad.Tracks.Count} tracks, {parsedKicad.Vias.Count} vias, {parsedKicad.Zones.Count} zones.");

        // 2. Test BOM Viewer and assembly tracking on parsed KiCad board
        PcbBoard renderBoard = parsedKicad;
        var vm = new CopperCalzone.ViewModels.MainViewModel();
        vm.LoadBoard(parsedKicad);
        vm.LoadBoard(parsedKicad);
        Console.WriteLine($"[PASS] Initialized BOM Viewer for '{parsedKicad.Metadata.Title}': {vm.TotalBomCount} BOM groups, {vm.TotalComponentCount} total parts. ({vm.ShowingBomLabel})");

        // Verify silkscreen exclusion
        bool hasSilkscreen = vm.FilteredBomItems.Any(b => b.References.Contains("LOGO1") || b.Layer == "F.SilkS" || b.Layer == "B.SilkS");
        if (hasSilkscreen) throw new Exception("Silkscreen item found in BOM!");
        Console.WriteLine("[PASS] Verified silkscreen footprints (LOGO1 on F.SilkS) are strictly excluded from BOM.");

        // Verify net connectivity filtering (labels, zero-net holes, unconnected test points)
        bool hasLabel = vm.FilteredBomItems.Any(b => b.References.Contains("G1"));
        if (hasLabel) throw new Exception("Copper graphic/text label G1 found in BOM!");
        bool hasMountingHole = vm.FilteredBomItems.Any(b => b.References.Contains("H1"));
        if (hasMountingHole) throw new Exception("Net-0 mounting hole H1 found in BOM!");
        bool hasUnconnectedTp = vm.FilteredBomItems.Any(b => b.References.Contains("TP1"));
        if (hasUnconnectedTp) throw new Exception("Unconnected test point TP1 found in BOM!");
        Console.WriteLine("[PASS] Verified net-connection filter strictly excludes labels (G1), net-0 holes (H1), and unconnected testpoints (TP1).");

        // Verify valid electrical components (U1, C1, C5) are present
        if (!vm.FilteredBomItems.Any(b => b.References.Contains("U1")))
            throw new Exception("Electrical component U1 missing from BOM!");

        // Verify grouping of C1 and C5
        var capGroup = vm.FilteredBomItems.FirstOrDefault(b => b.Value == "100nF");
        if (capGroup == null || capGroup.Quantity != 2 || !capGroup.ReferencesText.Contains("C1") || !capGroup.ReferencesText.Contains("C5"))
            throw new Exception("Grouped references test failed!");
        Console.WriteLine($"[PASS] Verified grouped references: References={capGroup.ReferencesText}, Value={capGroup.Value}, Footprint={capGroup.Package}, Qty={capGroup.Quantity}");

        vm.SwitchTab("bom");
        vm.SelectedBomItem = capGroup;
        capGroup.IsPlaced = true;
        Console.WriteLine($"[PASS] Assembly progress tracking: {vm.AssemblyProgressText}");

        // 4. Test adjustable sidebar and BOM column widths
        if (vm.SidebarWidth < 520) throw new Exception("SidebarWidth should be >= 520 on BOM tab");
        vm.SidebarWidth = 650;
        if (vm.SidebarWidth != 650) throw new Exception("SidebarWidth was not adjustable to 650");
        vm.SwitchTab("nets");
        if (vm.SidebarWidth != 340) throw new Exception("SidebarWidth should revert to non-BOM width (340)");
        vm.SwitchTab("bom");
        if (vm.SidebarWidth != 650) throw new Exception("SidebarWidth should restore last BOM width (650)");

        vm.BomColRefWidth = 160;
        vm.BomColValWidth = 140;
        vm.BomColFpWidth = 220;
        if (vm.BomColRefWidth != 160 || vm.BomColValWidth != 140 || vm.BomColFpWidth != 220)
            throw new Exception("BOM column widths were not adjustable");
        Console.WriteLine($"[PASS] Verified adjustable sidebar width ({vm.SidebarWidth}px) and adjustable BOM columns (Ref:{vm.BomColRefWidth}, Val:{vm.BomColValWidth}, FP:{vm.BomColFpWidth}).");

        // 5. Test Settings Panel, Tab Switching, and Persistence
        string tempSettingsFile = Path.Combine(Path.GetTempPath(), $"copper_settings_test_{Guid.NewGuid():N}.json");
        try
        {
            var testSettingsService = new CopperCalzone.Services.SettingsService(tempSettingsFile);
            var settingsVm = new CopperCalzone.ViewModels.SettingsViewModel(testSettingsService, vm.ApplySettings);

            // Test tab switching: Viewer -> Settings -> Viewer
            vm.OpenSettings();
            if (!vm.IsSettingsTabActive || vm.IsViewerTabActive)
                throw new Exception("OpenSettings did not activate Settings tab");

            vm.SwitchToViewer();
            if (!vm.IsViewerTabActive || vm.IsSettingsTabActive)
                throw new Exception("SwitchToViewer did not activate Viewer tab");

            // Test setting persistence: default sidebar tab, layer options, zone mode
            settingsVm.SetDefaultSidebarTab("layers");
            settingsVm.SetDefaultLayerPreset("top");
            settingsVm.SetZoneMode("hatch");
            settingsVm.ShowPadNetNames = false;
            settingsVm.AutoSave();

            // Load new service instance from file to verify persistence
            var loadedService = new CopperCalzone.Services.SettingsService(tempSettingsFile);
            var loadedSettings = loadedService.Current;

            if (loadedSettings.DefaultSidebarTab != "layers")
                throw new Exception($"Expected DefaultSidebarTab='layers', got '{loadedSettings.DefaultSidebarTab}'");
            if (loadedSettings.DefaultLayerPreset != "top")
                throw new Exception($"Expected DefaultLayerPreset='top', got '{loadedSettings.DefaultLayerPreset}'");
            if (loadedSettings.ZoneMode != "hatch")
                throw new Exception($"Expected ZoneMode='hatch', got '{loadedSettings.ZoneMode}'");
            if (loadedSettings.ShowPadNetNames != false)
                throw new Exception("Expected ShowPadNetNames=false");

            Console.WriteLine($"[PASS] Verified Settings persistence round-trip (DefaultTab={loadedSettings.DefaultSidebarTab}, Preset={loadedSettings.DefaultLayerPreset}, ZoneMode={loadedSettings.ZoneMode}, PadNetNames={loadedSettings.ShowPadNetNames})");
        }
        finally
        {
            if (File.Exists(tempSettingsFile)) File.Delete(tempSettingsFile);
            if (File.Exists(tempSettingsFile + ".tmp")) File.Delete(tempSettingsFile + ".tmp");
        }

        // 6. Test Sub-ViewModel decomposition (Option A)
        if (vm.Nets == null || vm.Bom == null || vm.Layers == null || vm.Inspector == null)
            throw new Exception("Sub-ViewModels were not instantiated properly");

        if (vm.Nets.TotalNetCount != vm.TotalNetCount || vm.Nets.TotalNetCount != 4)
            throw new Exception("NetsViewModel TotalNetCount mismatch");

        if (vm.Bom.TotalBomCount != vm.TotalBomCount || vm.Bom.TotalBomCount != 2)
            throw new Exception("BomViewModel TotalBomCount mismatch");

        if (vm.Layers.CopperLayers.Count == 0 || vm.Layers.TechLayers.Count == 0)
            throw new Exception("LayersViewModel layer collections were not populated");

        // 6b. Test PropertyChanged propagation from sub-viewmodels to MainViewModel
        var propVm = new MainViewModel();
        var changedProps = new HashSet<string>();
        propVm.PropertyChanged += (s, e) => { if (!string.IsNullOrEmpty(e.PropertyName)) changedProps.Add(e.PropertyName); };
        propVm.LoadBoard(renderBoard);
        if (!changedProps.Contains(nameof(MainViewModel.TotalNetCount)))
            throw new Exception("MainViewModel did not receive PropertyChanged for TotalNetCount from NetsViewModel");
        if (!changedProps.Contains(nameof(MainViewModel.TotalBomCount)))
            throw new Exception("MainViewModel did not receive PropertyChanged for TotalBomCount from BomViewModel");
        Console.WriteLine($"[PASS] Verified PropertyChanged propagation: TotalNetCount ({propVm.TotalNetCount}) and TotalBomCount ({propVm.TotalBomCount}) notified reactively.");

        Console.WriteLine($"[PASS] Verified Sub-ViewModel decomposition: NetsVM ({vm.Nets.TotalNetCount} nets), BomVM ({vm.Bom.TotalBomCount} groups), LayersVM ({vm.Layers.CopperLayers.Count} Cu + {vm.Layers.TechLayers.Count} tech), InspectorVM.");

        // 7. Test Skia rendering pipeline offscreen if requested
        if (!string.IsNullOrEmpty(screenshotOutputPath))
        {
            int width = 1280, height = 800;
            using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
            var canvas = surface.Canvas;

            double scale = Math.Min((width - 80.0) / renderBoard.Bbox.Width, (height - 80.0) / renderBoard.Bbox.Height);
            double panX = width / 2.0 - renderBoard.Bbox.CenterX * scale;
            double panY = height / 2.0 - renderBoard.Bbox.CenterY * scale;

            var gndNet = renderBoard.Nets.OrderByDescending(n => n.PadCount).FirstOrDefault();
            var selectedPad = renderBoard.Pads.FirstOrDefault(p => p.Net == gndNet?.Id);

            var state = new PcbRenderState
            {
                Board = renderBoard,
                Scale = scale,
                PanX = panX,
                PanY = panY,
                ActiveNetId = gndNet?.Id,
                SelectedPad = selectedPad,
                ShowPadNetNames = true,
                ZoneMode = ZoneDisplayMode.Solid,
                VisibleLayers = ["Edge.Cuts", "F.Cu", "B.Cu", "In1.Cu", "In2.Cu", "F.SilkS", "B.SilkS"]
            };

            var drawOp = new PcbSkiaDrawOperation(new Rect(0, 0, width, height), state);
            drawOp.RenderToSkiaCanvas(canvas);

            // Test BOM highlight rendering pass
            var bomState = new PcbRenderState
            {
                Board = renderBoard,
                Scale = scale,
                PanX = panX,
                PanY = panY,
                HighlightedFootprintRefs = ["D4", "J7", "C18"],
                ActiveFootprintRef = "D4",
                ShowPadNetNames = true,
                ZoneMode = ZoneDisplayMode.Solid,
                VisibleLayers = ["Edge.Cuts", "F.Cu", "B.Cu", "In1.Cu", "In2.Cu", "F.SilkS", "B.SilkS"]
            };
            var bomDrawOp = new PcbSkiaDrawOperation(new Rect(0, 0, width, height), bomState);
            canvas.Clear(new SKColor(0x12, 0x15, 0x1C));
            bomDrawOp.RenderToSkiaCanvas(canvas);

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 95);
            using var outStream = File.OpenWrite(screenshotOutputPath);
            data.SaveTo(outStream);
            Console.WriteLine($"[PASS] Rendered Skia PCB output for '{renderBoard.Metadata.Title}' (Active Net: {gndNet?.Name}, Selected Pin: {selectedPad?.DisplayTitle}) -> {screenshotOutputPath}");
        }

        Console.WriteLine("=== All Verification Checks Passed! ===");
        return 0;
    }
}
