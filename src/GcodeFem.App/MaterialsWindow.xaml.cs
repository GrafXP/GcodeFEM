using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using GcodeFem.Core.Materials;
using GcodeFem.Core.Slicing;
using Microsoft.Win32;

namespace GcodeFem.App;

/// <summary>The material library: what each material says and where every number of it comes from, and new materials from data sheets.</summary>
public partial class MaterialsWindow : Window
{
    readonly MaterialsViewModel viewModel;
    readonly bool scripted;

    /// <param name="scripted">A run from the command line: no questions are asked.</param>
    public MaterialsWindow(MaterialLibrary library, PresetLibrary? presets, bool scripted = false)
    {
        InitializeComponent();
        Look.DarkTitleBar(this);
        this.scripted = scripted;
        DataContext = viewModel = new MaterialsViewModel(library, presets);
    }

    /// <summary>Shows the material of that name, as a click on it in the list would.</summary>
    public void Select(string name)
    {
        if (viewModel.Materials.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase)) is { } entry) viewModel.Selected = entry;
        else throw new KeyNotFoundException($"There is no material '{name}'.");
    }

    /// <summary>Reads a data sheet into the form.</summary>
    public void Import(string pdf)
    {
        try
        {
            viewModel.Import(pdf);
        }
        catch (Exception ex) when (MainViewModel.IsExpected(ex))
        {
            if (scripted) throw;
            MessageBox.Show(this, ex.Message, "Could not read the data sheet", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Technical data sheets (*.pdf)|*.pdf" };
        if (dialog.ShowDialog(this) == true) Import(dialog.FileName);
    }

    void OpenDocument_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.DocumentPath is not { } path) return;
        try
        {
            // With whatever program shows PDFs here, beside this window.
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(this, ex.Message, "Could not open the data sheet", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(CancelEventArgs e)
    {
        if (viewModel.IsChanged && !scripted &&
            MessageBox.Show(this, $"The changes to '{viewModel.Name}' are not saved. Close without saving them?", "Materials", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
            e.Cancel = true;
        base.OnClosing(e);
    }
}
