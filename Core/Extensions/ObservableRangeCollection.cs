using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace MoleculeEfficienceTracker.Core.Extensions;

public class ObservableRangeCollection<T> : ObservableCollection<T>
{
    private bool _suppressNotification = false;

    public ObservableRangeCollection() : base() { }

    public ObservableRangeCollection(IEnumerable<T> collection) : base(collection) { }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_suppressNotification)
            base.OnCollectionChanged(e);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (!_suppressNotification)
            base.OnPropertyChanged(e);
    }

    public void AddRange(IEnumerable<T> list)
    {
        if (list == null)
            throw new ArgumentNullException(nameof(list));

        _suppressNotification = true;

        foreach (T item in list)
        {
            Add(item);
        }

        _suppressNotification = false;
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public void ReplaceRange(IEnumerable<T> collection)
    {
        if (collection == null)
            throw new ArgumentNullException(nameof(collection));

        _suppressNotification = true;
        Clear();
        foreach (var item in collection)
            Add(item);
        _suppressNotification = false;

        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public void RemoveRange(IEnumerable<T> list)
    {
        if (list == null)
            throw new ArgumentNullException(nameof(list));

        _suppressNotification = true;

        foreach (T item in list)
        {
            Remove(item);
        }

        _suppressNotification = false;
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public void Replace(T item)
    {
        ReplaceRange(new T[] { item });
    }
}

