using UglyToad.PdfPig;

namespace GcodeFem.Core.Materials;

/// <summary>One page of a document as lines of text, top to bottom. Pages count from 1.</summary>
public sealed record TextPage(int Number, IReadOnlyList<string> Lines);

/// <summary>A word on a page: where its baseline starts, and the size of its letters (all in points).</summary>
public readonly record struct PlacedWord(string Text, double Left, double Bottom, double Size);

/// <summary>Reads the text of a PDF the way a data sheet's tables need it: every table row as one line.</summary>
public static class PdfText
{
    /// <summary>
    /// Words whose baselines are closer than this share of their letter size are one line. The
    /// lines of a table cell that wraps are two thirds of a letter size apart and stay apart; a
    /// label and its value that the layout set a little off each other come together.
    /// </summary>
    const double SameLine = 0.3;

    public static IReadOnlyList<TextPage> Read(string path)
    {
        try
        {
            using var document = PdfDocument.Open(path);
            return
            [
                .. document.GetPages().Select(page => new TextPage(page.Number,
                    Lines(page.GetWords().Where(w => w.Letters.Count > 0).Select(w => new PlacedWord(w.Text, w.BoundingBox.Left, w.BoundingBox.Bottom, w.Letters[0].PointSize))))),
            ];
        }
        catch (Exception ex) when (ex is not IOException and not UnauthorizedAccessException)
        {
            // PdfPig reports a broken or encrypted file with exceptions of its own.
            throw new InvalidDataException($"'{path}' could not be read as a PDF: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Words → lines: words on one baseline belong together whatever order the file holds them in,
    /// which puts the cells of a table row side by side, left to right.
    /// </summary>
    public static IReadOnlyList<string> Lines(IEnumerable<PlacedWord> words)
    {
        var lines = new List<(double Bottom, double Size, List<PlacedWord> Words)>();
        foreach (var word in words.OrderByDescending(w => w.Bottom))
        {
            if (lines.Count > 0 && Math.Abs(lines[^1].Bottom - word.Bottom) < SameLine * Math.Max(lines[^1].Size, word.Size)) lines[^1].Words.Add(word);
            else lines.Add((word.Bottom, word.Size, [word]));
        }
        return [.. lines.Select(line => string.Join(' ', line.Words.OrderBy(w => w.Left).Select(w => w.Text)))];
    }
}
