using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace PCWord.WindowsProbe;

internal static class Program
{
    private const int PrecedingCharacterLimit = 128;
    private const int FollowingCharacterLimit = 32;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(750);

    private static async Task<int> Main()
    {
        Console.WriteLine("PCWord Windows UI Automation feasibility probe");
        Console.WriteLine("Read-only: no keys are captured, no text is printed, and nothing is saved.");
        Console.WriteLine("Focus a test field in another app. Press Ctrl+C here to stop.");
        Console.WriteLine();

        string? lastIdentity = null;
        DateTime lastSummary = DateTime.MinValue;

        while (true)
        {
            try
            {
                ProbeResult result = SampleFocusedElement();
                string identity = result.IdentityKey;

                if (!StringComparer.Ordinal.Equals(identity, lastIdentity) ||
                    DateTime.UtcNow - lastSummary >= TimeSpan.FromSeconds(3))
                {
                    PrintResult(result);
                    lastIdentity = identity;
                    lastSummary = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            catch (Exception ex)
            {
                // Exception messages may contain provider details. Only print the exception type.
                Console.WriteLine($"Probe error: {ex.GetType().Name}; context treated as unavailable.");
                lastIdentity = null;
            }

            await Task.Delay(PollInterval);
        }
    }

    private static ProbeResult SampleFocusedElement()
    {
        AutomationElement? element = AutomationElement.FocusedElement;
        if (element is null)
            return ProbeResult.Unavailable("No focused UI Automation element.");

        AutomationElement.AutomationElementInformation current;
        try
        {
            current = element.Current;
        }
        catch (ElementNotAvailableException)
        {
            return ProbeResult.Unavailable("Focused element disappeared during sampling.");
        }

        string processName = GetProcessName(current.ProcessId);
        string controlType = current.ControlType?.ProgrammaticName ?? "Unknown";
        string identity = $"{current.ProcessId}:{current.NativeWindowHandle}:{controlType}";

        if (current.IsPassword)
        {
            return new ProbeResult(identity, processName, controlType, current.IsPassword,
                current.HasKeyboardFocus, false, false, false, null, null,
                "Password field detected; text access skipped.");
        }

        bool textPatternAvailable = false;
        bool selectionPresent = false;
        int? precedingLength = null;
        int? followingLength = null;
        bool caretGeometryAvailable = false;
        string status = "TextPattern unavailable.";

        try
        {
            if (element.TryGetCurrentPattern(TextPattern.Pattern, out object patternObject) &&
                patternObject is TextPattern textPattern)
            {
                textPatternAvailable = true;
                TextPatternRange[] selections = textPattern.GetSelection();
                if (selections.Length == 1)
                {
                    TextPatternRange caretOrSelection = selections[0];
                    int endpointComparison = caretOrSelection.CompareEndpoints(
                        TextPatternRangeEndpoint.Start,
                        caretOrSelection,
                        TextPatternRangeEndpoint.End);
                    selectionPresent = endpointComparison != 0;

                    // Never inspect surrounding context when text is selected. The user must
                    // collapse the selection before this probe considers the field usable.
                    if (!selectionPresent)
                    {
                        TextPatternRange before = caretOrSelection.Clone();
                        before.MoveEndpointByUnit(
                            TextPatternRangeEndpoint.Start,
                            TextUnit.Character,
                            -PrecedingCharacterLimit);
                        string beforeText = before.GetText(PrecedingCharacterLimit);
                        precedingLength = beforeText.Length;

                        TextPatternRange after = caretOrSelection.Clone();
                        after.MoveEndpointByUnit(
                            TextPatternRangeEndpoint.End,
                            TextUnit.Character,
                            FollowingCharacterLimit);
                        string afterText = after.GetText(FollowingCharacterLimit);
                        followingLength = afterText.Length;

                        caretGeometryAvailable = caretOrSelection.GetBoundingRectangles().Length >= 4;
                        status = "Caret context read in memory; content suppressed.";
                    }
                    else
                    {
                        status = "Selection present; surrounding context skipped.";
                    }
                }
                else
                {
                    status = $"Provider returned {selections.Length} selection ranges; context skipped.";
                }
            }
        }
        catch (ElementNotAvailableException)
        {
            status = "Element disappeared while reading; context unavailable.";
        }
        catch (InvalidOperationException)
        {
            status = "Provider rejected a text operation; context unavailable.";
        }
        catch (COMException)
        {
            status = "UI Automation provider error; context unavailable.";
        }

        return new ProbeResult(identity, processName, controlType, false,
            current.HasKeyboardFocus, textPatternAvailable, selectionPresent,
            caretGeometryAvailable, precedingLength, followingLength, status);
    }

    private static string GetProcessName(int processId)
    {
        try
        {
            return Process.GetProcessById(processId).ProcessName;
        }
        catch
        {
            return "Unknown process";
        }
    }

    private static void PrintResult(ProbeResult result)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Focused process: {result.ProcessName}");
        Console.WriteLine($"  Control type: {result.ControlType}");
        Console.WriteLine($"  Password field: {result.IsPassword}");
        Console.WriteLine($"  Keyboard focus confirmed: {result.HasKeyboardFocus}");
        Console.WriteLine($"  UIA TextPattern: {result.TextPatternAvailable}");
        Console.WriteLine($"  Selection present: {result.SelectionPresent}");
        Console.WriteLine($"  Caret geometry available: {result.CaretGeometryAvailable}");
        Console.WriteLine($"  Preceding chars available: {FormatLength(result.PrecedingLength)}");
        Console.WriteLine($"  Following chars available: {FormatLength(result.FollowingLength)}");
        Console.WriteLine($"  Result: {result.Status}");
        Console.WriteLine("  Text content: [never displayed]");
        Console.WriteLine();
    }

    private static string FormatLength(int? value) => value?.ToString() ?? "n/a";

    private sealed record ProbeResult(
        string IdentityKey,
        string ProcessName,
        string ControlType,
        bool IsPassword,
        bool HasKeyboardFocus,
        bool TextPatternAvailable,
        bool SelectionPresent,
        bool CaretGeometryAvailable,
        int? PrecedingLength,
        int? FollowingLength,
        string Status)
    {
        public static ProbeResult Unavailable(string status) =>
            new("unavailable", "Unknown", "Unknown", false, false, false, false,
                false, null, null, status);
    }
}
