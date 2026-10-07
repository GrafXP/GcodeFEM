using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using GcodeFem.Core.Fem;
using GcodeFem.Core.Geometry;
using GcodeFem.Core.Study;
using GcodeFem.Core.Visual;
using HelixToolkit.Wpf.SharpDX;
using Microsoft.Win32;

namespace GcodeFem.App;

public partial class MainWindow : Window
{
    readonly MainViewModel viewModel = new();
    readonly StartupOptions options = StartupOptions.Parse(Environment.GetCommandLineArgs().Skip(1));

    public MainWindow()
    {
        InitializeComponent();
        DataContext = viewModel;
        if (options.Size("size") is { } size) (Width, Height) = size;
        viewModel.FitRequested += FitCamera;
        viewModel.Failed += (title, message) =>
        {
            // A scripted run must not wait for someone to click a dialog away.
            if (options.Screenshot is null) MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            else Console.Error.WriteLine($"{title}: {message}");
        };
        Closed += (_, _) => viewModel.Dispose();
        Loaded += async (_, _) => await StartAsync();
    }

    const string StudyFilter = "GcodeFem studies (*.study.json)|*.study.json|All files (*.*)|*.*";

    void OpenModel_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "STL models (*.stl)|*.stl" };
        if (dialog.ShowDialog(this) == true) Load(dialog.FileName);
    }

    void OpenStudy_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = StudyFilter };
        if (dialog.ShowDialog(this) == true) Try(() => viewModel.OpenStudy(dialog.FileName), "Could not open the study");
    }

    void SaveStudy_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.StudyPath is { } path) Try(() => viewModel.SaveStudy(path), "Could not save the study");
        else SaveStudyAs_Click(sender, e);
    }

    void SaveStudyAs_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.ModelPath is not { } model) return;
        // Next to the model by default: the study names it by its path from there.
        var dialog = new SaveFileDialog
        {
            Filter = StudyFilter,
            InitialDirectory = Path.GetDirectoryName(viewModel.StudyPath ?? model),
            FileName = Path.GetFileName(viewModel.StudyPath) ?? Path.GetFileNameWithoutExtension(model) + ".study.json",
        };
        if (dialog.ShowDialog(this) != true) return;
        var path = dialog.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? dialog.FileName : dialog.FileName + ".study.json";
        Try(() => viewModel.SaveStudy(path), "Could not save the study");
    }

    bool Load(string path) => Try(() => viewModel.LoadModel(path), "Could not open model");

    /// <summary>Carries out something the user asked for; a failure is reported, not fatal.</summary>
    bool Try(Action action, string title)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex) when (MainViewModel.IsExpected(ex))
        {
            if (options.Screenshot is null) MessageBox.Show(this, ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            else Console.Error.WriteLine($"{title}: {ex.Message}");
            return false;
        }
    }

    // A click that is not the start of a drag picks the face under it. Helix turns the camera with the right button.
    Point? pressed;

    void Viewport_MouseDown(object sender, MouseButtonEventArgs e) => pressed = e.GetPosition(Viewport);

    void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
    {
        var at = e.GetPosition(Viewport);
        if (pressed is { } from && (at - from).Length < 4) PickAt(at);
        pressed = null;
    }

    /// <param name="at">In the viewport's own coordinates.</param>
    void PickAt(Point at)
    {
        if (!viewModel.IsPicking && !viewModel.IsPlacing) return;
        var ray = Viewport.UnProject(at);
        viewModel.Pick(ray.Position, ray.Direction);
    }

    /// <summary>
    /// Opens the model named on the command line (or the sample cube) and carries out whatever else
    /// the command line asks for: slice, solve, pick a view, save a screenshot and quit.
    /// </summary>
    async Task StartAsync()
    {
        var presets = viewModel.LoadPresetsAsync();
        var loaded = options.Text("study") is { } study
            ? Try(() => viewModel.OpenStudy(study), "Could not open the study")
            : (options.Model ?? FindSample("cube20.stl")) is { } model && Load(model);
        var ok = loaded;
        try
        {
            if (loaded)
            {
                if (options.Triple("rot") is { } rotation) (viewModel.RotationX, viewModel.RotationY, viewModel.RotationZ) = (rotation.X, rotation.Y, rotation.Z);
                viewModel.Process = options.Text("process") ?? viewModel.Process;
                viewModel.Filament = options.Text("filament") ?? viewModel.Filament;
                await presets;
                ApplyStudy();

                var view = options.Enum<ViewMode>("view") ?? ViewMode.Model;
                if (view != ViewMode.Model)
                {
                    await viewModel.SliceAsync();
                    ok = viewModel.HasPrint;
                }
                if (ok && view == ViewMode.Results)
                {
                    if (options.Text("level") is { } level and not "auto")
                        viewModel.Level = viewModel.Levels.First(l => l.Value == int.Parse(level, CultureInfo.InvariantCulture));
                    await viewModel.SolveAsync();
                    ok = viewModel.HasResult;
                }
                if (ok) ApplyView(view);
                if (ok && view == ViewMode.Model) await ClickAsync();
                if (ok && options.Text("save-study") is { } saved) viewModel.SaveStudy(Path.GetFullPath(saved));
            }
        }
        catch (Exception ex) when (MainViewModel.IsExpected(ex))
        {
            ok = false;
            Console.Error.WriteLine(ex.Message);
            if (options.Screenshot is null) MessageBox.Show(this, ex.Message, "Could not follow the command line", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        if (options.Screenshot is not { } file) return;
        while (!viewModel.SceneReady.IsCompleted) await viewModel.SceneReady;
        await Task.Delay(TimeSpan.FromSeconds(options.Number("wait") ?? 1.5)); // a few frames, so the new geometry is on screen
        SaveScreenshot(file);
        Environment.ExitCode = ok ? 0 : 1;
        Close();
    }

    /// <summary>The command line's say on the study: the load case, a quick clamp and push, a new interface, the one selected and its value.</summary>
    void ApplyStudy()
    {
        if (options.Text("case") is { } name)
            viewModel.SelectedLoadCase = viewModel.LoadCases.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
                                         ?? throw new KeyNotFoundException($"There is no load case '{name}'.");
        if (options.Text("fix") is { } face)
        {
            var side = face.ToLowerInvariant() is ['x' or 'y' or 'z', 'm', 'i' or 'a', 'n' or 'x'] text
                ? (Axis: (Axis)(text[0] - 'x'), Max: text.EndsWith("max", StringComparison.Ordinal))
                : throw new FormatException($"--fix expects xmin, xmax, ymin, ymax, zmin or zmax but got '{face}'.");
            viewModel.ClampAndPush(side.Axis, side.Max, options.Triple("force") ?? new System.Numerics.Vector3(0, 0, -100));
        }
        if (options.Enum<InterfaceKind>("add") is { } kind) viewModel.AddInterface(kind);
        if (options.Text("select") is { } selected)
            viewModel.SelectedInterface = viewModel.Interfaces.FirstOrDefault(i => string.Equals(i.Name, selected, StringComparison.OrdinalIgnoreCase))
                                          ?? throw new KeyNotFoundException($"There is no interface '{selected}'.");
        if (options.Text("name") is { } renamed && viewModel.SelectedInterface is { } item) item.Name = renamed;
        if (options.Text("value") is { } value && viewModel.SelectedInterface is { } valued) valued.ValueText = value;
        if (options.Number("pick-angle") is { } angle) viewModel.PickAngle = angle;
    }

    /// <summary>
    /// Clicks on the model as the mouse would: --lay "x,y" lays the face there on the bed, then
    /// --click "x,y;x,y" picks faces for the selected interface. The points are pixels of a
    /// screenshot of this window, which is where one reads them off.
    /// </summary>
    async Task ClickAsync()
    {
        if (options.Text("lay") is { } face)
        {
            await CameraReady();
            viewModel.IsPlacing = true;
            PickAt(InViewport(face, "lay"));
        }
        if (options.Text("click") is not { } clicks) return;
        await CameraReady();
        viewModel.IsPicking = true;
        foreach (var click in clicks.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) PickAt(InViewport(click, "click"));
        viewModel.IsPicking = false;
    }

    /// <summary>The camera must have been through a frame with the scene as it is for a ray to come out right.</summary>
    async Task CameraReady()
    {
        while (!viewModel.SceneReady.IsCompleted) await viewModel.SceneReady;
        await Task.Delay(TimeSpan.FromSeconds(1));
    }

    /// <summary>"x,y" in pixels of a screenshot → the same spot in the viewport's own coordinates.</summary>
    Point InViewport(string pixel, string option)
    {
        if (pixel.Split(',') is not [var x, var y]) throw new FormatException($"--{option} expects x,y but got '{pixel}'.");
        var dpi = VisualTreeHelper.GetDpi(this);
        var inContent = new Point(double.Parse(x, CultureInfo.InvariantCulture) / dpi.DpiScaleX, double.Parse(y, CultureInfo.InvariantCulture) / dpi.DpiScaleY);
        return ((UIElement)Content).TranslatePoint(inContent, Viewport);
    }

    void ApplyView(ViewMode view)
    {
        viewModel.View = view;
        if (options.Enum<ToolpathColouring>("colour") is { } colouring) viewModel.Colouring = colouring;
        if (options.Enum<ResultQuantity>("colour") is { } quantity) viewModel.Quantity = quantity;
        if (options.Enum<CellColouring>("colour") is { } cells) viewModel.CellColouring = cells;
        if (options.Text("layers")?.Split('-') is [var low, var high])
        {
            viewModel.LayerLow = 1;
            viewModel.LayerHigh = int.Parse(high, CultureInfo.InvariantCulture);
            viewModel.LayerLow = int.Parse(low, CultureInfo.InvariantCulture);
        }
        if (options.Text("hide") is { } hidden)
            foreach (var item in viewModel.LineTypes)
                item.IsShown = !hidden.Split(',').Any(name => item.Label.Contains(name, StringComparison.OrdinalIgnoreCase));
        if (options.Number("pass") is { } pass) viewModel.Pass = (int)pass - 1;
        if (options.Number("scale-top") is { } top) viewModel.ScaleTop = top;
        if (options.Number("deform") is { } deformation) viewModel.Deformation = deformation;
        if (options.Has("no-elements")) viewModel.ShowElements = false;
        if (options.Has("thin")) viewModel.ThinLines = true;
        if (options.Triple("look") is { } look)
        {
            viewModel.Camera.LookDirection = new Vector3D(look.X, look.Y, look.Z);
            if (lastFit is { } bounds) FitCamera(bounds, options.Number("zoom") ?? 1);
        }
    }

    Box3? lastFit;

    void FitCamera(Box3 bounds) => FitCamera(bounds, 1);

    /// <summary>
    /// Keeps the current viewing direction and backs the camera off until the bounding sphere fits.
    /// Helix's ZoomExtents(Rect3D) left the camera pointing at nothing in 3.1.2, so this is done by hand.
    /// </summary>
    void FitCamera(Box3 bounds, double zoom)
    {
        lastFit = bounds;
        var camera = viewModel.Camera;
        var center = bounds.Center;
        var radius = Math.Max(bounds.Size.Length() / 2, 1e-3);
        var distance = radius / Math.Sin(camera.FieldOfView * Math.PI / 360) * 1.1 / zoom;

        var direction = camera.LookDirection;
        direction.Normalize();
        camera.LookDirection = direction * distance;
        camera.Position = new Point3D(center.X, center.Y, center.Z) - direction * distance;
        camera.UpDirection = new Vector3D(0, 0, 1);
        // Near enough to look at single beads, far enough for the whole bed.
        camera.NearPlaneDistance = Math.Max(0.05, radius / 200);
        camera.FarPlaneDistance = Math.Max(2000, radius * 40);
    }

    /// <summary>The window's content as a PNG, the 3D view included.</summary>
    void SaveScreenshot(string file)
    {
        var content = (FrameworkElement)Content;
        var dpi = VisualTreeHelper.GetDpi(this);
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(content.ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(content.ActualHeight * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        using var stream = File.Create(file);
        encoder.Save(stream);
    }

    /// <summary>Development convenience: the repository's samples folder, found by walking up from the exe.</summary>
    static string? FindSample(string name)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "samples", name);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}

/// <summary>True when the bound enum value is the one named by the parameter; lets radio buttons pick an enum value.</summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter as string, StringComparison.Ordinal);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Enum.Parse(targetType, (string)parameter) : Binding.DoNothing;
}
