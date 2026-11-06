using System.Drawing;
using PCWord.WindowsUI.Logic;
using Xunit;

namespace PCWord.Tests;

public sealed class SuggestionPopupUiLogicTests
{
    [Fact]
    public void State_DeduplicatesCapsAndMovesSelectionWithWraparound()
    {
        var state = new SuggestionPopupState();
        Assert.True(state.TryUpdate(1, "field", ["apple", "APPLE", "apply", "application", "apples"]));
        Assert.Equal(new[] { "apple", "apply", "application" }, state.Candidates);
        Assert.True(state.MoveSelection(-1));
        Assert.Equal(2, state.SelectedIndex);
        Assert.True(state.TryGetSelected(out string candidate));
        Assert.Equal("application", candidate);
    }

    [Fact]
    public void State_RejectsOutOfOrderAndClearsOnMissingField()
    {
        var state = new SuggestionPopupState();
        Assert.True(state.TryUpdate(2, "field", ["apple"]));
        Assert.False(state.TryUpdate(2, "field", ["apply"]));
        Assert.False(state.TryUpdate(1, "field", ["apply"]));
        Assert.True(state.TryUpdate(3, null, ["hidden"]));
        Assert.False(state.IsVisible);
        Assert.Empty(state.Candidates);
    }

    [Fact]
    public void State_RequiresMatchingIdentityBeforeAcceptance()
    {
        var state = new SuggestionPopupState();
        state.TryUpdate(7, "field-a", ["hello"]);
        Assert.True(state.IsCurrent(7, "field-a"));
        Assert.False(state.IsCurrent(8, "field-a"));
        Assert.False(state.IsCurrent(7, "field-b"));
        Assert.False(state.IsCurrent(7, null));
    }

    [Fact]
    public void Placement_DefaultsBelowAndMovesAboveWhenNeeded()
    {
        var work = new Rectangle(0, 0, 800, 600);
        Rectangle below = PopupPlacement.Calculate(new Rectangle(100, 100, 2, 20),
            new Size(200, 100), work);
        Assert.Equal(new Rectangle(100, 122, 200, 100), below);

        Rectangle above = PopupPlacement.Calculate(new Rectangle(100, 540, 2, 20),
            new Size(200, 100), work);
        Assert.Equal(new Rectangle(100, 438, 200, 100), above);
    }

    [Fact]
    public void Placement_ClampsOnNegativeCoordinateMonitorAndSupportsRightToLeft()
    {
        var work = new Rectangle(-1600, -200, 1600, 900);
        Rectangle leftToRight = PopupPlacement.Calculate(
            new Rectangle(-5, 100, 2, 20), new Size(300, 120), work);
        Assert.Equal(-5, leftToRight.Left);
        Assert.InRange(leftToRight.Left, work.Left, work.Right - leftToRight.Width);

        Rectangle rightToLeft = PopupPlacement.Calculate(
            new Rectangle(-10, 100, 2, 20), new Size(300, 120), work, rightToLeft: true);
        Assert.Equal(-308, rightToLeft.Left);
    }

    [Fact]
    public void Placement_ClampsOversizedWindowToWorkArea()
    {
        var work = new Rectangle(-100, -50, 320, 200);
        Rectangle actual = PopupPlacement.Calculate(
            new Rectangle(500, 500, 1, 1), new Size(900, 500), work);
        Assert.Equal(work, actual);
    }
}
