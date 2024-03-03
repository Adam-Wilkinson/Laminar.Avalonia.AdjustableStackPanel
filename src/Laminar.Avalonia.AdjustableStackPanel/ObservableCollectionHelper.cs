using System.Collections;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;

namespace Laminar.Avalonia.AdjustableStackPanel;

internal static class ObservableCollectionHelper
{
    private static readonly Dictionary<INotifyCollectionChanged, object> CollectionWrappers = new();

    public static ObservableCollectionWrapper<TItem> Events<TItem>(this IList<TItem> collection)
        //where TList : IList<TItem>, INotifyCollectionChanged
    {
        if (collection is not INotifyCollectionChanged collectionChanged)
        {
            throw new ArgumentException();
        }

        if (CollectionWrappers.TryGetValue(collectionChanged, out object? value) && value is ObservableCollectionWrapper<TItem> collectionWrapper)
        {
            return collectionWrapper;
        }

        ObservableCollectionWrapper<TItem> newWrapper = new(collectionChanged);
        CollectionWrappers[collectionChanged] = newWrapper;
        return newWrapper;
    }

    public class ObservableCollectionWrapper<TItem>
    {
        public event EventHandler<ItemAddedEventArgs<TItem>>? ItemAdded;

        public event EventHandler<ItemRemovedEventArgs<TItem>>? ItemRemoved;

        public ObservableCollectionWrapper(INotifyCollectionChanged collection)
        {
            collection.CollectionChanged += CollectionChanged;
        }

        private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) 
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    ItemsAdded(sender, e.NewItems!, e.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    ItemsRemoved(sender, e.OldItems!, e.OldStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    ItemsRemoved(sender, e.OldItems!, e.OldStartingIndex);
                    ItemsAdded(sender, e.NewItems!, e.NewStartingIndex);
                    break;
            }
        }

        private void ItemsAdded(object? sender, IList items, int index)
        {
            foreach (TItem item in items.Cast<TItem>())
            {
                ItemAdded?.Invoke(sender, new ItemAddedEventArgs<TItem>() { Item = item, Index = index, TotalItemsAdded = items.Count });
                index++;
            }
        }

        private void ItemsRemoved(object? sender, IList items, int index)
        {
            foreach (TItem item in items.Cast<TItem>())
            {
                ItemRemoved?.Invoke(sender, new ItemRemovedEventArgs<TItem>() { Item = item, Index = index, TotalItemsRemoved = items.Count });
                index++;
            }
        }
    }
}

public class ItemAddedEventArgs<TITem> : EventArgs
{
    public required TITem Item { get; init; }
    public required int Index { get; init; }
    public required int TotalItemsAdded { get; init; }
}

public class ItemRemovedEventArgs<TItem> : EventArgs
{
    public required TItem Item { get; init; }
    public required int Index { get; init; }
    public required int TotalItemsRemoved { get; init; }
}