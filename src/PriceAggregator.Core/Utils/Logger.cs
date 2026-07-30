using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PriceAggregator.Core.Models;
using PriceAggregator.Core.Utils;
namespace PriceAggregator.Core.Utils;

public static class Logger
{
    private static readonly string LogDirectory = Path.Combine(AppContext.BaseDirectory, "logs");

    public static void Log(string message)
    {
        var timestamped = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";

        // Still print to console when running manually
        Console.WriteLine(timestamped);

        try
        {
            Directory.CreateDirectory(LogDirectory);
            var fileName = $"scrape-{DateTime.Now:yyyy-MM-dd}.log";
            var filePath = Path.Combine(LogDirectory, fileName);

            File.AppendAllText(filePath, timestamped + Environment.NewLine);
        }
        catch (Exception ex)
        {
            // If logging itself fails, don't crash the app over it — just note it on console
            Console.WriteLine($"[Logger] Failed to write to log file: {ex.Message}");
        }
    }
}
