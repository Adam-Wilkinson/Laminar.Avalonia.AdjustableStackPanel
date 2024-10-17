namespace Laminar.Avalonia.AdjustableStackPanel.ResizeLogic;

public record struct ResizerMode(
    IResizeMethod MethodBefore,
    IResizeMethod MethodAfter, 
    string IsAccessiblePseudoclass,
    string IsActivePseudoclass,
    ResizerMode.IsAccessibleCheck IsAccessible)
{
    public delegate bool IsAccessibleCheck(int indexInParent, int totalChildren, ResizeFlags flags);

    public static ResizerMode ArrowBefore { get; } = new(
        new Scale(),
        new Cascade(),
        ":ArrowBeforeAccessible",
        ":ArrowBefore",
        (int indexInParent, int totalChildren, ResizeFlags flags) => totalChildren > 1 && indexInParent > -1 && (indexInParent > 0 || flags.HasFlag(ResizeFlags.CanMoveStackStart)));

    public static ResizerMode Default { get; } = new(
        new Cascade(),
        new Cascade(),
        ":DefaultAccessible",
        ":Default",
        (int _, int _, ResizeFlags _) => true);

    public static ResizerMode ArrowAfter { get; } = new(
        new Cascade(),
        new Scale(),
        ":ArrowAfterAccessible",
        ":ArrowAfter",
        (int indexInParent, int totalChildren, ResizeFlags flags) => totalChildren > 1 && indexInParent < totalChildren && (indexInParent < totalChildren - 1 || flags.HasFlag(ResizeFlags.CanMoveStackEnd)));

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