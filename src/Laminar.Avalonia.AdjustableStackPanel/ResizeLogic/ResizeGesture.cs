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
    None                         = 0,
    DisableResizeAfter    = 1 << 0,
    DisableResizeBefore   = 1 << 1,
    HasTransition                = 1 << 2,
}

public delegate double ResizeAmountTransformation(double resizeAmount, double originalResizeSpace, double currentResizeSpace, double totalResizeSpace);

public readonly record struct ResizeGesture(Resize[] Resizes, ResizerMode Mode, ResizerModifier Modifier, ResizeFlags Flags = ResizeFlags.None)
{
    public static readonly ResizeAmountTransformation MaintainResizeAmount = (x, _, _, _) => x;
    public static readonly ResizeAmountTransformation NegateResizeAmount = (x, _, _, _) => -x;

    private static readonly Dictionary<(ResizerMode mode, ResizerModifier modifier), ResizeGesture> GestureDictionary = [];

    private static readonly ResizeGesture EmptyGesture = new() { Mode = ResizerMode.None, Modifier = ResizerModifier.None, Resizes = [], };

    private static readonly ResizeGesture[] AllGestures =
    [
        new ResizeGesture()
        {
            Mode = ResizerMode.Default,
            Modifier = ResizerModifier.None,
            Flags = ResizeFlags.DisableResizeAfter | ResizeFlags.DisableResizeBefore,
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
            Flags = ResizeFlags.DisableResizeAfter,
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
            Flags = ResizeFlags.DisableResizeBefore,
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
    {
        foreach (ResizeGesture resizeGesture in AllGestures)
        {
            GestureDictionary.Add((resizeGesture.Mode, resizeGesture.Modifier), resizeGesture);
        }
    }

    public static bool TryGetGesture(ResizerMode? mode, ResizerModifier? modifier, out ResizeGesture gesture)
        => GestureDictionary.TryGetValue((mode ?? ResizerMode.None, modifier ?? ResizerModifier.None), out gesture);

    public static ResizeGesture GetGesture(ResizerMode? mode, ResizerModifier? modifier)
    {
        if (TryGetGesture(mode, modifier, out ResizeGesture gesture))
        {
            return gesture;
        }

        return EmptyGesture;
    }

    public readonly IEnumerable<(int index, Resize resize)> AccessibleResizes<T>(IList<T> resizeElements, int index)
    {
        foreach (Resize resize in Resizes)
        {
            int indexOfCurrentResize = index + resize.IndexOffset;
            if (indexOfCurrentResize < -1 || indexOfCurrentResize >= resizeElements.Count)
            {
                continue;
            }

            yield return (indexOfCurrentResize, resize);
        }
    }

    public readonly double Execute<T>(IList<T> resizeElements, int activeResizerIndex, double resizeAmount, Span<double> spaceBeforeResizers, IResizingHarness<T> harness, ResizeFlags currentResizeFlags)
    {
        double changeInStackSize = 0;
        foreach ((int indexOfCurrentResize, Resize resize) in AccessibleResizes(resizeElements, activeResizerIndex))
        {
            changeInStackSize += resize.Execute(resizeElements, harness, resizeAmount, indexOfCurrentResize, activeResizerIndex, spaceBeforeResizers, Flags & currentResizeFlags);
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