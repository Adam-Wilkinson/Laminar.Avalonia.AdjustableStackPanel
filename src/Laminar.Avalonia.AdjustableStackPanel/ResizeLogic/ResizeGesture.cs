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
    None                        = 0,
    IgnoreResizeAfter           = 1 << 0,
    IgnoreResizeBefore          = 1 << 1,
}

public delegate double ResizeAmountTransformation(double resizeAmount, double originalResizeSpace, double currentResizeSpace, double totalResizeSpace);

public readonly record struct ResizeGesture(ResizerMovement[] Resizes, ResizerMode Mode, ResizerModifier Modifier, ResizeFlags Flags = ResizeFlags.None)
{
    public static readonly ResizeAmountTransformation MaintainResizeAmount = (x, _, _, _) => x;
    public static readonly ResizeAmountTransformation NegateResizeAmount = (x, _, _, _) => -x;

    private static Dictionary<(ResizerMode mode, ResizerModifier modifier), ResizeGesture> GestureDictionary = [];

    private static readonly ResizeGesture EmptyGesture = new([], new(ResizeMethod.None, ResizeMethod.None, "", "", (int _, int _, ResizeFlags flags) => false), ResizerModifier.None);

    private static readonly ResizeGesture[] DefaultGestures =
    [
        new ResizeGesture()
        {
            Mode = ResizerMode.Default,
            Modifier = ResizerModifier.None,
            Flags = ResizeFlags.IgnoreResizeAfter | ResizeFlags.IgnoreResizeBefore,
            Resizes =
            [
                new(0, MaintainResizeAmount, ResizerMode.Default),
            ],
        },

        new ResizeGesture()
        {
            Mode = ResizerMode.Default,
            Modifier = ResizerModifier.Move,
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
            Flags = ResizeFlags.IgnoreResizeAfter,
            Resizes =
            [
                new(0, MaintainResizeAmount, ResizerMode.ArrowBefore), 
            ],
        },

        new ResizeGesture()
        {
            Mode = ResizerMode.ArrowBefore,
            Modifier = ResizerModifier.Move,
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
            Flags = ResizeFlags.IgnoreResizeBefore,
            Resizes =
            [
                new(0, MaintainResizeAmount, ResizerMode.ArrowAfter), 
            ],
        },

        new ResizeGesture()
        {
            Mode = ResizerMode.ArrowAfter,
            Modifier = ResizerModifier.Move,
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

    public readonly IEnumerable<(int index, ResizerMovement resize)> AccessibleResizes<T>(IList<T> resizeElements, int index)
    {
        int minimumResizerIndex = Flags.HasFlag(ResizeFlags.IgnoreResizeBefore) ? -1 : 0;
        int maximumResizerIndex = Flags.HasFlag(ResizeFlags.IgnoreResizeAfter) ? resizeElements.Count : (resizeElements.Count - 1);

        foreach (ResizerMovement resize in Resizes)
        {
            int indexOfCurrentResize = index + resize.IndexOffset;
            if (indexOfCurrentResize < minimumResizerIndex || indexOfCurrentResize > maximumResizerIndex)
            {
                continue;
            }

            yield return (indexOfCurrentResize, resize);
        }
    }

    public readonly double Execute<T>(IList<T> resizeElements, ResizeInfo<T> resizeInfo)
    {
        double changeInStackSize = 0;
        resizeInfo.Flags = Flags & resizeInfo.Flags;

        foreach ((int indexOfCurrentResize, ResizerMovement resize) in AccessibleResizes(resizeElements, resizeInfo.ActiveResizerIndex))
        {
            if (!resize.IsValid(resizeElements, indexOfCurrentResize, resizeInfo))
            {
                return 0;
            }
        }

        foreach ((int indexOfCurrentResize, ResizerMovement resize) in AccessibleResizes(resizeElements, resizeInfo.ActiveResizerIndex))
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