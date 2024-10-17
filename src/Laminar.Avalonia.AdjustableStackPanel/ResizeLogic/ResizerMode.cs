namespace Laminar.Avalonia.AdjustableStackPanel.ResizeLogic;

public record struct ResizerMode(
    ResizeMethod MethodBefore, 
    ResizeMethod MethodAfter, 
    string IsAccessiblePseudoclass,
    string IsActivePseudoclass,
    ResizerMode.IsAccessibleCheck IsAccessible)
{
    public delegate bool IsAccessibleCheck(int indexInParent, int totalChildren, ResizeFlags flags);

    public static ResizerMode ArrowBefore { get; } = new(
        ResizeMethod.SqueezeExpand,
        ResizeMethod.Cascade,
        ":ArrowBeforeAccessible",
        ":ArrowBefore",
        (int indexInParent, int totalChildren, ResizeFlags flags) => indexInParent > -1 && !(indexInParent == 0 && !flags.HasFlag(ResizeFlags.IgnoreResizeBefore)));

    public static ResizerMode Default { get; } = new(
        ResizeMethod.Cascade,
        ResizeMethod.Cascade,
        ":DefaultAccessible",
        ":Default",
        (int _, int _, ResizeFlags _) => true);

    public static ResizerMode ArrowAfter { get; } = new(
        ResizeMethod.Cascade,
        ResizeMethod.SqueezeExpand,
        ":ArrowAfterAccessible",
        ":ArrowAfter",
        (int indexInParent, int totalChildren, ResizeFlags flags) => indexInParent <= totalChildren - 2 && !(indexInParent == totalChildren - 2 && !flags.HasFlag(ResizeFlags.IgnoreResizeAfter)));

    /// <summary>
    /// The Dictionary used to check the <see cref="ResizeWidget"/> template for resize zones. Controls whos name match the key will be marked as a resize zone for the value.
    /// </summary>
    public static readonly Dictionary<string, ResizerMode> All = new()
    {
        { "PART_ArrowBeforeZone", ArrowBefore },
        { "PART_DefaultResizeZone", Default },
        { "PART_ArrowAfterZone", ArrowAfter },
    };
}