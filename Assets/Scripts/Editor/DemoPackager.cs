using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// After every successful Windows build: puts the playtest read-me next to the .exe and zips the
/// whole build into one file ready to send, e.g. Builds/Deckshift_v0.1.zip.
///
/// ⚠️ THE ZIP LEAVES OUT "*_DoNotShip" FOLDERS. Unity writes Burst debug information beside every
/// build under that name; it is for crash symbolication on OUR machine and is not meant to go out.
/// Zipping the build folder by hand includes it, which is why this is automated.
///
/// The read-me's source is DEMO_README.txt at the project root, so editing it is editing what every
/// future build ships with.
/// </summary>
public class DemoPackager : IPostprocessBuildWithReport
{
    public int callbackOrder => 100;

    private const string ReadmeSource = "DEMO_README.txt";
    private const string ReadmeName = "READ ME FIRST.txt";

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.result == BuildResult.Failed || report.summary.result == BuildResult.Cancelled) return;
        if (report.summary.platform != BuildTarget.StandaloneWindows64 &&
            report.summary.platform != BuildTarget.StandaloneWindows) return;

        string buildDir = Path.GetDirectoryName(report.summary.outputPath);
        if (string.IsNullOrEmpty(buildDir) || !Directory.Exists(buildDir)) return;

        if (File.Exists(ReadmeSource))
            File.Copy(ReadmeSource, Path.Combine(buildDir, ReadmeName), true);
        else
            Debug.LogWarning("[DemoPackager] " + ReadmeSource + " not found at the project root; the build has no read-me.");

        string zipPath = Path.Combine(Path.GetDirectoryName(buildDir) ?? buildDir,
                                      Application.productName + "_v" + Application.version + ".zip");
        try
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
            using (FileStream fs = File.Create(zipPath))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                string top = new DirectoryInfo(buildDir).Name;   // the zip unpacks into one folder
                foreach (string file in Directory.GetFiles(buildDir, "*", SearchOption.AllDirectories))
                {
                    string rel = file.Substring(buildDir.Length).TrimStart('\\', '/');
                    if (rel.Split('\\', '/')[0].EndsWith("_DoNotShip")) continue;

                    ZipArchiveEntry entry = zip.CreateEntry(top + "/" + rel.Replace('\\', '/'), System.IO.Compression.CompressionLevel.Optimal);
                    using (Stream dst = entry.Open())
                    using (FileStream src = File.OpenRead(file))
                        src.CopyTo(dst);
                }
            }
            Debug.Log("[DemoPackager] ready to send: " + Path.GetFullPath(zipPath));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[DemoPackager] could not write the zip: " + e.Message);
        }
    }
}
