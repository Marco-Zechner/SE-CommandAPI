using System;
using System.Collections;
using System.Collections.Generic;
namespace Mz.ApiProtocol
{
    internal sealed class ReadOnlyListView<T>
        : IReadOnlyList<T>
    {
        private readonly IList<T> _items;
        internal ReadOnlyListView(IList<T> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));
            _items = items;
        }
        public int Count
        {
            get
            {
                return _items.Count;
            }
        }
        public T this[int index]
        {
            get
            {
                return _items[index];
            }
        }
        public IEnumerator<T> GetEnumerator()
        {
            return _items.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
