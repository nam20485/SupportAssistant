using System.Collections.ObjectModel;

namespace SupportAssistant.ViewModels;

/// <summary>
/// Thread-safe wrapper around <see cref="ObservableCollection{T}"/> that serializes
/// all mutating operations with a lock, preventing <see cref="System.IndexOutOfRangeException"/>
/// when multiple threads add or remove items concurrently.
/// </summary>
public class ThreadSafeObservableCollection<T> : ObservableCollection<T>
{
    private readonly object _syncRoot = new();

    protected override void InsertItem(int index, T item)
    {
        lock (_syncRoot)
        {
            base.InsertItem(index, item);
        }
    }

    protected override void RemoveItem(int index)
    {
        lock (_syncRoot)
        {
            base.RemoveItem(index);
        }
    }

    protected override void SetItem(int index, T item)
    {
        lock (_syncRoot)
        {
            base.SetItem(index, item);
        }
    }

    protected override void MoveItem(int oldIndex, int newIndex)
    {
        lock (_syncRoot)
        {
            base.MoveItem(oldIndex, newIndex);
        }
    }

    protected override void ClearItems()
    {
        lock (_syncRoot)
        {
            base.ClearItems();
        }
    }
}
