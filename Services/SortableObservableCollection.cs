using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Nellie.Services
{
    /// <summary>
    /// An <see cref="ObservableCollection{T}"/> that can reorder itself in place and
    /// raise a single <see cref="NotifyCollectionChangedAction.Reset"/>. Avalonia's
    /// DataGrid ignores Move notifications, so a plain <c>Move</c>-based reorder never
    /// repaints — a Reset makes it re-render the rows in the new order.
    /// </summary>
    public sealed class SortableObservableCollection<T> : ObservableCollection<T>
    {
        public void Sort(Comparison<T> comparison)
        {
            if (Items is not List<T> list || list.Count < 2)
                return;

            list.Sort(comparison);
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
