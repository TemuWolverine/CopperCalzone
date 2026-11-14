using Avalonia.Markup.Xaml;

namespace CopperCalzone.Markup;

public sealed class PlatformTipExtension : MarkupExtension
{
    public string BaseText { get; set; } = "";
    public string Shortcut { get; set; } = "";

    public PlatformTipExtension()
    {
    }

    public PlatformTipExtension(string baseText, string shortcut)
    {
        BaseText = baseText;
        Shortcut = shortcut;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return OperatingSystem.IsWindows() && !string.IsNullOrEmpty(Shortcut)
            ? $"{BaseText} {Shortcut}"
            : BaseText;
    }
}

