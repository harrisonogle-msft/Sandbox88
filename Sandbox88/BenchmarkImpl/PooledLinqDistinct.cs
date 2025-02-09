using Microsoft.Management.Services.Common;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;

public struct PooledLinqDistinct : IBenchmarkTarget
{
    private List<BaseObject> _baseObjects;

    public PooledLinqDistinct()
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

                var baseObjects = new ArraySegment<BaseObject>(buffer, 0, count);
                int distinctCount = DistinctCount(baseObjects);

                static int DistinctCount<TObjects>(TObjects items) where TObjects : struct, IReadOnlyList<BaseObject> =>
                    items.DistinctBy(static baseObject => baseObject.GetObjectId()).Count();

                if (distinctCount != count)
                {
                    var hashSet = new HashSet<ObjectKey>();
                    foreach (BaseObject baseObject in baseObjects)
                    {
                        ObjectKey objectKey = baseObject.GetObjectId();
                        if (hashSet.Contains(objectKey))
                        {
                            // Note: We currently throw if there are duplicate nulls, so make sure to retain that behavior.
                            throw new InvalidClientRequestException(ApiErrorCode.DuplicateObjectKeyInRequest, $"Duplicate object key found in request: {objectKey?.ToString() ?? "null"}");
                        }
                        hashSet.Add(objectKey);
                    }
                    throw new InvalidClientRequestException(ApiErrorCode.DuplicateObjectKeyInRequest, "Duplicate object key found in request.");
                }

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
            }
        }
    }
}
