using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace MoleculeEfficienceTracker.Core.Extensions;

/// <summary>
/// Collection observable acceptant des modifications par lots.
///
/// La version précédente muselait aussi les notifications de propriété pendant le
/// lot, puis ne levait qu'un Reset de collection : les liaisons sur Count et sur
/// l'indexeur restaient périmées, contrairement au contrat d'ObservableCollection.
/// Le lot se termine désormais par les trois notifications attendues.
/// </summary>
public class ObservableRangeCollection<T> : ObservableCollection<T>
{
    private bool _suppressNotification;

    public ObservableRangeCollection() { }

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

    private void NotifyReset()
    {
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <summary>Exécute une modification en lot, puis notifie une seule fois.</summary>
    private void InBatch(Action action)
    {
        _suppressNotification = true;
        try
        {
            action();
        }
        finally
        {
            // Le drapeau est rendu même si le lot échoue : sans cela, une exception
            // laissait la collection muette pour le reste de la session.
            _suppressNotification = false;
        }

        NotifyReset();
    }

    public void AddRange(IEnumerable<T> list)
    {
        ArgumentNullException.ThrowIfNull(list);

        InBatch(() =>
        {
            foreach (T item in list) Add(item);
        });
    }

    public void ReplaceRange(IEnumerable<T> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        InBatch(() =>
        {
            Clear();
            foreach (T item in collection) Add(item);
        });
    }

    public void RemoveRange(IEnumerable<T> list)
    {
        ArgumentNullException.ThrowIfNull(list);

        InBatch(() =>
        {
            foreach (T item in list) Remove(item);
        });
    }

    public void Replace(T item) => ReplaceRange(new[] { item });
}
