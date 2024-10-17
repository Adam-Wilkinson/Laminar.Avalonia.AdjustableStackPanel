namespace Laminar.Avalonia.AdjustableStackPanel.ResizeLogic;

public enum ResizerModifier
{
    None,
    Move,
    ShrinkGrow,
}

[Flags]
public enum ResizeFlags
{
    None                = 0,
    CanMoveStackStart   = 1 << 0,
    PreferResizeBefore  = 1 << 1,
    CanMoveStackEnd     = 1 << 2,
    PreferResizeAfter   = 1 << 3,
    PreferResize        = PreferResizeBefore | PreferResizeAfter,
}

public delegate double ResizeAmountTransformation(double resizeAmount, double originalResizeSpace, double currentResizeSpace, double totalResizeSpace);

public readonly record struct ResizeGesture(ResizerMovement[] Resizes, ResizerMode Mode, ResizerModifier Modifier, ResizeFlags Flags = ResizeFlags.None)
{
    public static readonly ResizeAmountTransformation MaintainResizeAmount = (x, _, _, _) => x;
    public static readonly ResizeAmountTransformation NegateResizeAmount = (x, _, _, _) => -x;

    private static Dictionary<(ResizerMode mode, ResizerModifier modifier), ResizeGesture> GestureDictionary = [];

    private static readonly ResizeGesture EmptyGesture = new([], new(new NoneResizeMethod(), new NoneResizeMethod(), "", "", (int _, int _, ResizeFlags flags) => false), ResizerModifier.None);

    private static readonly ResizeGesture[] DefaultGestures =
    [
        new ResizeGesture()
        {
            Mode = ResizerMode.Default,
            Modifier = ResizerModifier.None,
            Flags = ResizeFlags.None,
            Resizes =
            [
                new(0, MaintainResizeAmount, ResizerMode.Default),
            ],
        },

        new ResizeGesture()
        {
            Mode = ResizerMode.Default,
            Modifier = ResizerModifier.Move,
            Flags = ResizeFlags.PreferResize,
            Resizes =
            [
                new(-1, MaintainResizeAmount, ResizerMode.Default),
                new(0, MaintainResizeAmount, ResizerMode.Default),
            ]
        },

        new ResizeGesture()
        {
            Mode = ResizerMode.Default,
            Modifier = ResizerModifier.ShrinkGrow,
            Flags = ResizeFlags.PreferResize,
            Resizes =
            [
                new(-1, NegateResizeAmount, ResizerMode.Default),
                new(0, MaintainResizeAmount, ResizerMode.Default),
            ]
        },

        new ResizeGesture()
        {
            Mode = ResizerMode.ArrowBefore,
            Modifier = ResizerModifier.None,
            Flags = ResizeFlags.PreferResizeBefore,
            Resizes =
            [
                new(0, MaintainResizeAmount, ResizerMode.ArrowBefore), 
            ],
        },

        new ResizeGesture()
        {
            Mode = ResizerMode.ArrowBefore,
            Modifier = ResizerModifier.Move,
            Flags = ResizeFlags.PreferResize,
            Resizes =
            [
                new(0, MaintainResizeAmount, ResizerMode.ArrowBefore),
                new(1, MaintainResizeAmount, ResizerMode.ArrowAfter),
            ]
        },

        new ResizeGesture()
        {
            Mode = ResizerMode.ArrowBefore,
            Modifier = ResizerModifier.ShrinkGrow,
            Flags = ResizeFlags.PreferResize,
            Resizes =
            [
                new(0, MaintainResizeAmount, ResizerMode.ArrowBefore),
                new(1, (resizeAmount, originalResizeSpace, currentResizeSpace, totalResizeSpace) => -Scale(resizeAmount, originalResizeSpace, totalResizeSpace - currentResizeSpace), ResizerMode.ArrowAfter),
            ]
        },

        new ResizeGesture()
        {
            Mode = ResizerMode.ArrowAfter,
            Modifier = ResizerModifier.None,
            Flags = ResizeFlags.PreferResizeAfter,
            Resizes =
            [
                new(0, MaintainResizeAmount, ResizerMode.ArrowAfter), 
            ],
        },

        new ResizeGesture()
        {
            Mode = ResizerMode.ArrowAfter,
            Modifier = ResizerModifier.Move,
            Flags = ResizeFlags.PreferResize,
            Resizes =
            [
                new(-1, MaintainResizeAmount, ResizerMode.ArrowBefore),
                new(0, MaintainResizeAmount, ResizerMode.ArrowAfter),
            ],
        },

        new ResizeGesture()
        {
            Mode = ResizerMode.ArrowAfter,
            Modifier = ResizerModifier.ShrinkGrow,
            Flags = ResizeFlags.PreferResize,
            Resizes =
            [
                new(-1, (resizeAmount, originalResizeSpace, currentResizeSpace, totalResizeSpace) => -Scale(resizeAmount, totalResizeSpace - originalResizeSpace, currentResizeSpace), ResizerMode.ArrowBefore),
                new(0, MaintainResizeAmount, ResizerMode.ArrowAfter),
            ],
        },
    ];

    static ResizeGesture()
        => SetGestures(DefaultGestures);

    public static void SetGestures(ResizeGesture[] gestures)
    {
        GestureDictionary = [];
        foreach (ResizeGesture resizeGesture in gestures)
        {
            GestureDictionary.Add((resizeGesture.Mode, resizeGesture.Modifier), resizeGesture);
        }
    }

    public static bool TryGetGesture(ResizerMode mode, ResizerModifier? modifier, out ResizeGesture gesture)
        => GestureDictionary.TryGetValue((mode, modifier ?? ResizerModifier.None), out gesture);

    public static ResizeGesture GetGesture(ResizerMode? mode, ResizerModifier? modifier)
    {
        if (mode is null)
        {
            return EmptyGesture;
        }

        if (TryGetGesture(mode.Value, modifier, out ResizeGesture gesture))
        {
            return gesture;
        }

        return EmptyGesture;
    }

    public readonly IEnumerable<(int index, ResizerMovement resize)> AccessibleResizes<T>(IList<T> resizeElements, int index, ResizeFlags flags)
    {
        int minimumResizerIndex = flags.HasFlag(ResizeFlags.CanMoveStackStart) ? -1 : 0;
        int maximumResizerIndex = flags.HasFlag(ResizeFlags.CanMoveStackEnd) ? (resizeElements.Count - 1) : (resizeElements.Count - 2);

        foreach (ResizerMovement resize in Resizes)
        {
            int indexOfCurrentResize = index + resize.IndexOffset;
            if (indexOfCurrentResize <= maximumResizerIndex && indexOfCurrentResize >= minimumResizerIndex)
            {
                yield return (indexOfCurrentResize, resize);
            }
        }
    }

    public readonly double Execute<T>(IList<T> resizeElements, ResizeInfo<T> resizeInfo)
    {
        double changeInStackSize = 0;
        resizeInfo.Flags |= Flags;

        foreach ((int indexOfCurrentResize, ResizerMovement resize) in AccessibleResizes(resizeElements, resizeInfo.ActiveResizerIndex, resizeInfo.Flags))
        {
            if (!resize.IsValid(resizeElements, indexOfCurrentResize, resizeInfo))
            {
                return 0;
            }
        }

        foreach ((int indexOfCurrentResize, ResizerMovement resize) in AccessibleResizes(resizeElements, resizeInfo.ActiveResizerIndex, resizeInfo.Flags))
        {
            changeInStackSize += resize.Execute(resizeElements, indexOfCurrentResize, resizeInfo);
        }

        return changeInStackSize;
    }

    private static double Scale(double input, double originalSpace, double currentSpace)
    {
        if (originalSpace == 0 || currentSpace == 0)
        {
            return input;
        }

        return input * currentSpace / originalSpace;
    }
}