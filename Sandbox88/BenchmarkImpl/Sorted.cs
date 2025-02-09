using Microsoft.Management.Services.Common;
using System.Diagnostics;
using System.Runtime.InteropServices;

public struct Sorted : IBenchmarkTarget
{
    private List<BaseObject> _baseObjects;

    public Sorted()
    {
        _baseObjects = new();
    }

    /// <summary>
    /// Resets the internal state of this class for benchmarking purposes.
    /// </summary>
    public void Reset()
    {
        _baseObjects = new List<BaseObject>();
    }

    public IEnumerable<BaseObject> BaseObjects
    {
        get => _baseObjects;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (object.ReferenceEquals(value, _baseObjects))
            {
                // Clearing the input would be a bug.
                return;
            }

            // A note on `_baseObjects.Clear()` usage:
            //
            // For backwards compatibility, we cannot create a new collection
            // and replace the old one.
            //
            // Currently, callers who already have a reference to the collection
            // returned by this method will actually observe changes when this
            // method is called again (even if by a different caller).
            //
            // That is not ideal, but to change it now would be a behavioral
            // breaking change.
            _baseObjects.Clear();

            if (value.TryGetNonEnumeratedCount(out int nonEnumeratedCount) && nonEnumeratedCount == 0)
            {
                return;
            }

            // `AddRange` is faster and more efficient than single `Add`s if the
            // source enumerable is a collection (count is known in advance).
            _baseObjects.AddRange(value);

            // Happy case: single item
            if (_baseObjects.Count == 1)
            {
                // For backwards compatibility, throw the exact same exception as we are currently in this case.
                _ = _baseObjects[0].GetObjectId();

                return;
            }

            // We can use ref locals here as a small optimization.
            // https://blog.marcgravell.com/2022/05/unusual-optimizations-ref-foreach-and.html
            Span<BaseObject> items = CollectionsMarshal.AsSpan(_baseObjects);
            Debug.Assert(items.Length > 1); // handled in happy case
            for (int i = 1; i < items.Length; ++i)
            {
                ref BaseObject baseObject = ref items[i];

                // For backwards compatibility, throw the exact same exception as we are currently in this case.
                _ = baseObject.GetObjectId();
            }

            // We can take advantage of the fact that `ObjectKey` implements `IComparable`
            // in the following way:
            // 1. sort the elements using `ObjectKey.CompareTo`
            // 2. iterate the elements in sorted order
            //     - if any consecutive elements are equal, we have a duplicate

            // Now that we know it has at least two items, let's allocate a copy.
            var baseObjects = new List<BaseObject>(_baseObjects);

            baseObjects.Sort(static (lhs, rhs) =>
            {
                // In a generalized Comparison<T> implementation we would
                // normally handle the null cases, but in this instance
                // we have already validated none are null.
                Debug.Assert(lhs is not null);
                Debug.Assert(rhs is not null);

                if (lhs.GetObjectId() is not ObjectKey lhsKey)
                {
                    // This groups all nulls to one side.
                    return -1;
                }

                if (rhs.GetObjectId() is not ObjectKey rhsKey)
                {
                    // This groups all nulls to one side.
                    return 1;
                }

                return lhsKey.CompareTo(rhsKey);
            });

            // We can use ref locals here as a small optimization.
            // https://blog.marcgravell.com/2022/05/unusual-optimizations-ref-foreach-and.html
            Span<BaseObject> sortedItems = CollectionsMarshal.AsSpan(baseObjects);
            Debug.Assert(sortedItems.Length > 1); // handled in happy case
            for (int i = 0; i < sortedItems.Length - 1; ++i)
            {
                ref BaseObject curr = ref sortedItems[i];
                ref BaseObject next = ref sortedItems[i + 1];

                // ObjectKey implements the == operator, so let's use it.
                if (curr.GetObjectId() == next.GetObjectId())
                {
                    // Note: We currently throw if there are duplicate nulls, so make sure to retain that behavior.
                    throw new InvalidClientRequestException(ApiErrorCode.DuplicateObjectKeyInRequest, $"Duplicate object key found in request: {curr.GetObjectId()?.ToString() ?? "null"}");
                }
            }

            // Let's double check our work.
            // Note: in release builds, the compiler omits debug assertions.
            Debug.Assert(_baseObjects.Select(o => o?.GetObjectId()).ToHashSet().Count == _baseObjects.Count);
            Debug.Assert(_baseObjects.Count == baseObjects.Count);
        }
    }
}
