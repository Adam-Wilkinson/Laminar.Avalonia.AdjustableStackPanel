namespace Laminar.Avalonia.AdjustableStackPanel.ResizeLogic;

public enum ResizerMode
{
    None,
    ArrowBefore,
    Default,
    ArrowAfter
}

public static class ResizerModeExtensions
{
    private static readonly ResizerMode[] _resizerModes = typeof(ResizerMode).GetEnumValues().Cast<ResizerMode>().Where(mode => mode != ResizerMode.None).ToArray();

    public static ResizerMode[] AllModes() => _resizerModes;

    public static (ResizeMethod methodBefore, ResizeMethod methodAfter)? GetResizeMethods(this ResizerMode mode) => mode switch
    {
        ResizerMode.ArrowBefore => (ResizeMethod.SqueezeExpand, ResizeMethod.Cascade),
        ResizerMode.Default => (ResizeMethod.Cascade, ResizeMethod.Cascade),
        ResizerMode.ArrowAfter => (ResizeMethod.Cascade, ResizeMethod.SqueezeExpand),
        _ => null,
    };

    public static bool IsAccessible(this ResizerMode mode, int indexInParent, int totalChildren, ResizeFlags currentFlags) => mode switch
    {
        ResizerMode.Default => true,
        ResizerMode.ArrowBefore => indexInParent > -1 && !(indexInParent == 0 && !currentFlags.HasFlag(ResizeFlags.CanConsumeSpaceBeforeStack)),
        ResizerMode.ArrowAfter => indexInParent < totalChildren && !(indexInParent == totalChildren - 2 && !currentFlags.HasFlag(ResizeFlags.CanConsumeSpaceAfterStack)),
        _ => false
    };
}