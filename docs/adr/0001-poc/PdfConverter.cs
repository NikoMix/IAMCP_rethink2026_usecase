using System.Diagnostics;

namespace Adr0001.Poc;

/// <summary>Converts DOCX to PDF with LibreOffice headless: soffice --headless --convert-to pdf --outdir &lt;dir&gt; &lt;file&gt;.</summary>
public static class PdfConverter
{
    public static string? FindSoffice(string? explicitPath)
    {
        var candidates = new List<string?>
        {
            explicitPath,
            Environment.GetEnvironmentVariable("SOFFICE_PATH"),
            @"C:\Program Files\LibreOffice\program\soffice.exe",
            "/usr/bin/soffice",
            "/usr/lib/libreoffice/program/soffice",
            "/Applications/LibreOffice.app/Contents/MacOS/soffice",
        };

        var pathDirs = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator);
        candidates.AddRange(pathDirs.SelectMany(d => new[] { Path.Combine(d, "soffice"), Path.Combine(d, "soffice.exe") }));
        return candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c) && File.Exists(c));
    }

    public static string Command(string soffice, string docxPath, string outDir) =>
        $"\"{soffice}\" " + string.Join(" ", Arguments(docxPath, outDir, "<temp-profile-dir>").Select(a => a.Contains(' ') ? $"\"{a}\"" : a));

    // A private profile avoids clashing with a running desktop LibreOffice instance.
    private static IEnumerable<string> Arguments(string docxPath, string outDir, string profileDir) =>
    [
        "-env:UserInstallation=" + (Path.IsPathRooted(profileDir) ? new Uri(profileDir).AbsoluteUri : profileDir),
        "--headless", "--norestore", "--convert-to", "pdf", "--outdir", outDir, docxPath,
    ];

    public static string Convert(string soffice, string docxPath, string outDir, TimeSpan timeout)
    {
        var profileDir = Path.Combine(Path.GetTempPath(), "adr0001-lo-profile-" + Guid.NewGuid().ToString("N"));
        var psi = new ProcessStartInfo(soffice)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in Arguments(docxPath, outDir, profileDir))
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start soffice.");
        // Drain both pipes while waiting so a chatty soffice cannot block on a full buffer.
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeout))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"soffice did not finish within {timeout.TotalSeconds:0} s.");
        }

        var pdf = Path.Combine(outDir, Path.GetFileNameWithoutExtension(docxPath) + ".pdf");
        if (process.ExitCode != 0 || !File.Exists(pdf))
        {
            throw new InvalidOperationException(
                $"soffice exited with {process.ExitCode}: {stderr.Result} {stdout.Result}");
        }

        return pdf;
    }
}
