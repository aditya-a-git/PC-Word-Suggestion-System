using System.Drawing;

namespace PCWord.WindowsUI.Logic;

public static class PopupPlacement
{
    public static Rectangle Calculate(
        Rectangle caret,
        Size popupSize,
        Rectangle workArea,
        bool rightToLeft = false,
        int gap = 2)
    {
        if (caret.Width < 0 || caret.Height < 0)
            throw new ArgumentOutOfRangeException(nameof(caret));
        if (workArea.Width <= 0 || workArea.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(workArea));
        if (popupSize.Width <= 0 || popupSize.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(popupSize));
        if (gap < 0)
            throw new ArgumentOutOfRangeException(nameof(gap));

        int width = Math.Min(popupSize.Width, workArea.Width);
        int height = Math.Min(popupSize.Height, workArea.Height);
        int left = rightToLeft ? caret.Right - width : caret.Left;
        int below = caret.Bottom + gap;
        int above = caret.Top - height - gap;
        int top = below + height <= workArea.Bottom
            ? below
            : above >= workArea.Top
                ? above
                : Math.Clamp(below, workArea.Top, workArea.Bottom - height);

        left = Math.Clamp(left, workArea.Left, workArea.Right - width);
        top = Math.Clamp(top, workArea.Top, workArea.Bottom - height);
        return new Rectangle(left, top, width, height);
    }
}
