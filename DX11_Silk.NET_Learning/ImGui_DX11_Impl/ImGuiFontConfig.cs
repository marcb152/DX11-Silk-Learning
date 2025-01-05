using ImGuiNET;

namespace DX11_Silk.NET_Learning.ImGui_DX11_Impl;

public readonly struct ImGuiFontConfig
{
    public ImGuiFontConfig(string fontPath, int fontSize, Func<ImGuiIOPtr, IntPtr> getGlyphRange = null)
    {
        if (fontSize <= 0) throw new ArgumentOutOfRangeException(nameof(fontSize));
        FontPath = fontPath ?? throw new ArgumentNullException(nameof(fontPath));
        FontSize = fontSize;
        GetGlyphRange = getGlyphRange;
    }

    public string FontPath { get; }
    public int FontSize { get; }
    public Func<ImGuiIOPtr, IntPtr> GetGlyphRange { get; }
}