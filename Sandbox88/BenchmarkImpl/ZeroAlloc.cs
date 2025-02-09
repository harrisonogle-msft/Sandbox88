using Microsoft.Management.Services.Common;
using System.Buffers;
using System.Diagnostics;

public struct ZeroAlloc : IBenchmarkTarget
{
    private List<BaseObject> _baseObjects;

    public ZeroAlloc()
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

            // See this: https://github.com/dotnet/runtime/blob/3998832b9727592c3a9e863d962e97f6ba61d42c/src/libraries/System.Private.CoreLib/src/System/IO/Stream.cs#L120-L126
            // Same rationale, but with wiggle room before hitting the LOH threshold.
            // Also: ArrayPool<T>.Shared only hands out arrays in powers of 2.
            const int DefaultBufferSize = 32768;

            BaseObject[]? buffer = null;
            BaseObject[]? sorted = null;
            try
            {
                int count;
                if (value.TryGetNonEnumeratedCount(out count))
                {
                    if (count == 0)
                    {
                        _baseObjects.Clear();
                        return;
                    }

                    buffer = ArrayPool<BaseObject>.Shared.Rent(count);
                }
                else
                {
                    buffer = ArrayPool<BaseObject>.Shared.Rent(DefaultBufferSize);
                }

                {
                    int i = 0;
                    foreach (BaseObject item in value)
                    {
                        if (item is null)
                        {
                            // For backwards compatibility, throw the exact same exception as we are currently in this case.
                            _ = item!.GetObjectId();
                        }

                        if (i == buffer.Length)
                        {
                            // Grow the array. This is called "geometric expansion".
                            // https://en.wikipedia.org/wiki/Dynamic_array#Geometric_expansion_and_amortized_cost
                            BaseObject[] temp = ArrayPool<BaseObject>.Shared.Rent(buffer.Length * 2);
                            Array.Copy(buffer, 0, temp, 0, buffer.Length);
                            ArrayPool<BaseObject>.Shared.Return(buffer);
                            buffer = temp;
                        }

                        buffer[i] = item;
                        i++;
                    }

                    count = i;
                }

                if (count == 0)
                {
                    _baseObjects.Clear();
                    return;
                }
                else if (count == 1)
                {
                    _baseObjects.Clear();
                    _baseObjects.Add(buffer[0]);
                    return;
                }

                Debug.Assert(count >= 2);
                Debug.Assert(buffer.Take(count).All(static item => item is not null));

                // We can take advantage of the fact that `ObjectKey` implements `IComparable`
                // in the following way:
                // 1. sort the elements using `ObjectKey.CompareTo`
                // 2. iterate the elements in sorted order
                //     - if any consecutive elements are equal, we have a duplicate

                // 1. Make a copy (to preserve original order) and sort the copy.

                // The copy is pooled, too. This implementation does not allocate!
                sorted = ArrayPool<BaseObject>.Shared.Rent(count);
                Array.Copy(buffer, 0, sorted, 0, count);
                new Span<BaseObject>(sorted, 0, count).Sort(static (lhs, rhs) =>
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

                // 2. Check for duplicates.

                for (int i = 0; i < count - 1; ++i)
                {
                    // We can use ref locals here as a small optimization.
                    // https://blog.marcgravell.com/2022/05/unusual-optimizations-ref-foreach-and.html
                    ref BaseObject curr = ref sorted[i];
                    ref BaseObject next = ref sorted[i + 1];

                    // ObjectKey implements the == operator, so let's use it.
                    if (curr.GetObjectId() == next.GetObjectId())
                    {
                        // Note: We currently throw if there are duplicate nulls, so make sure to retain that behavior.
                        throw new InvalidClientRequestException(ApiErrorCode.DuplicateObjectKeyInRequest, $"Duplicate object key found in request: {curr.GetObjectId()?.ToString() ?? "null"}");
                    }
                }

                // We are done using this to validate - return it to the pool ASAP.
                ArrayPool<BaseObject>.Shared.Return(sorted);
                sorted = null;

                // Now that we've validated everything, clear the existing collection.
                // We've waited until now because someone could still have a reference
                // to this collection (from a previous invocation of the getter), and
                // doing this mutates it "under their nose", so at least it's valid now.
                // Again, not ideal, but mutating the same collection is the existing behavior.
                _baseObjects.Clear();

                // `AddRange` is faster and more efficient than single `Add`s if the
                // source enumerable is a collection (count is known in advance).
                _baseObjects.AddRange(new ReadOnlySpan<BaseObject>(buffer, 0, count));

                ArrayPool<BaseObject>.Shared.Return(buffer);
                buffer = null;

                // Let's double check our work.
                // Note: in release builds, the compiler omits debug assertions.
                Debug.Assert(_baseObjects.Select(o => o?.GetObjectId()).ToHashSet().Count == _baseObjects.Count);
                Debug.Assert(_baseObjects.Count == count);
            }
            finally
            {
                // Ensure we always return the pooled buffers, even if an exception occurred.

                if (buffer is BaseObject[] rented)
                {
                    ArrayPool<BaseObject>.Shared.Return(rented);
                }
                if (sorted is BaseObject[] rented2)
                {
                    ArrayPool<BaseObject>.Shared.Return(rented2);
                }
            }
        }
    }
}
