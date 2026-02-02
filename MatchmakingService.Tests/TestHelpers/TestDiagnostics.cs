using System.Diagnostics;
using System.Text;
using Xunit.Abstractions;

namespace MatchmakingService.Tests.TestHelpers;

/// <summary>
/// Diagnostic utilities for test debugging and stability
/// </summary>
public class TestDiagnostics
{
    private readonly ITestOutputHelper? _output;
    private readonly StringBuilder _log = new();
    private readonly Stopwatch _timer = new();

    public TestDiagnostics(ITestOutputHelper? output = null)
    {
        _output = output;
        _timer.Start();
    }

    /// <summary>
    /// Log a diagnostic message with timestamp
    /// </summary>
    public void Log(string message)
    {
        var timestamped = $"[{_timer.ElapsedMilliseconds}ms] {message}";
        _log.AppendLine(timestamped);
        _output?.WriteLine(timestamped);
    }

    /// <summary>
    /// Log an object's state for debugging
    /// </summary>
    public void LogObject<T>(string name, T obj)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(obj, new System.Text.Json.JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        Log($"{name}: {json}");
    }

    /// <summary>
    /// Mark a checkpoint in test execution
    /// </summary>
    public void Checkpoint(string name)
    {
        Log($"✓ Checkpoint: {name}");
    }

    /// <summary>
    /// Get elapsed time since test start
    /// </summary>
    public long ElapsedMs => _timer.ElapsedMilliseconds;

    /// <summary>
    /// Get full diagnostic log
    /// </summary>
    public string GetLog() => _log.ToString();

    /// <summary>
    /// Assert with diagnostic context
    /// </summary>
    public void AssertTrue(bool condition, string message, Func<string>? contextProvider = null)
    {
        if (!condition)
        {
            var context = contextProvider?.Invoke() ?? "No additional context";
            var errorMsg = $"{message}\nContext: {context}\nTest Log:\n{GetLog()}";
            throw new Xunit.Sdk.XunitException(errorMsg);
        }
    }

    /// <summary>
    /// Assert equality with diagnostic context
    /// </summary>
    public void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
        {
            var errorMsg = $"{message}\nExpected: {expected}\nActual: {actual}\nTest Log:\n{GetLog()}";
            throw new Xunit.Sdk.XunitException(errorMsg);
        }
    }
}
