using System.IO;
using System.Text.Json;
using Holo.Models;

namespace Holo.Services;

/// <summary>
/// Port of HoloCore.EvaluationStore on macOS.
/// Persists EvaluationReport as JSON + CSV files.
/// </summary>
public sealed class EvaluationStore
{
    private readonly string _directory;

    public EvaluationStore(string? customDirectory = null)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _directory = customDirectory ?? Path.Combine(appData, "Holo", "Evaluations");
        Directory.CreateDirectory(_directory);
    }

    public string Save(EvaluationReport report)
    {
        var timestamp = report.CompletedAt.ToString("yyyyMMdd-HHmmss");
        var filename = $"evaluation-{timestamp}.json";
        var path = Path.Combine(_directory, filename);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        var json = JsonSerializer.Serialize(report, options);
        File.WriteAllText(path, json);

        // Also save CSV
        var csvPath = Path.ChangeExtension(path, ".csv");
        File.WriteAllText(csvPath, ToCsv(report));

        return path;
    }

    public List<EvaluationReport> LoadAll()
    {
        if (!Directory.Exists(_directory)) return [];

        var reports = new List<EvaluationReport>();
        foreach (var file in Directory.GetFiles(_directory, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var report = JsonSerializer.Deserialize<EvaluationReport>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (report != null && report.TopologyZoneCount == 4)
                    reports.Add(report);
            }
            catch { /* skip malformed */ }
        }
        return reports.OrderByDescending(r => r.CompletedAt).ToList();
    }

    private static string ToCsv(EvaluationReport report)
    {
        var lines = new List<string>
        {
            "timestamp,expected,predicted,confidence,latency_ms,rejected,correct"
        };
        foreach (var tap in report.TapResults)
        {
            lines.Add(string.Join(",",
                report.CompletedAt.ToString("O"),
                tap.Expected.ShortName(),
                tap.Predicted?.ShortName() ?? "REJECTED",
                tap.Confidence.ToString("F4"),
                tap.LatencyMs.ToString("F2"),
                tap.WasRejected ? "1" : "0",
                (!tap.WasRejected && tap.Predicted == tap.Expected) ? "1" : "0"
            ));
        }
        return string.Join("\n", lines) + "\n";
    }
}
