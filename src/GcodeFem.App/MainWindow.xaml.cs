using System.IO;
using System.Windows;
using System.Windows.Media.Media3D;
using GcodeFem.Core.Geometry;
using Microsoft.Win32;
using PerspectiveCamera = HelixToolkit.Wpf.SharpDX.PerspectiveCamera;

namespace GcodeFem.App;

public partial class MainWindow : Window
{
    readonly MainViewModel viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = viewModel;
        Closed += (_, _) => viewModel.Dispose();
        Loaded += (_, _) =>
        {
            var initial = Environment.GetCommandLineArgs().Skip(1).FirstOrDefault() ?? FindSample("cube20.stl");
            if (initial != null) Load(initial);
        };
    }

    void OpenStl_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "STL models (*.stl)|*.stl" };
        if (dialog.ShowDialog(this) == true) Load(dialog.FileName);
    }

    void Load(string path)
    {
        try
        {
            viewModel.LoadStl(path);
            FitCamera(viewModel.Part!.Bounds);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Could not open model", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Keeps the current viewing direction and backs the camera off until the bounding sphere fits.
    /// Helix's ZoomExtents(Rect3D) left the camera pointing at nothing in 3.1.2, so this is done by hand.
    /// </summary>
    void FitCamera(Box3 bounds)
    {
        if (Viewport.Camera is not PerspectiveCamera camera) return;

        var center = bounds.Center;
        var radius = Math.Max(bounds.Size.Length() / 2, 1e-3);
        var distance = radius / Math.Sin(camera.FieldOfView * Math.PI / 360) * 1.1;

        var direction = camera.LookDirection;
        direction.Normalize();
        camera.LookDirection = direction * distance;
        camera.Position = new Point3D(center.X, center.Y, center.Z) - direction * distance;
        camera.UpDirection = new Vector3D(0, 0, 1);
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
