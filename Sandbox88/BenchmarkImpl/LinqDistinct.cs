using Microsoft.Management.Services.Common;
using System.Runtime.InteropServices;

public struct LinqDistinct : IBenchmarkTarget
{
    private List<BaseObject> _baseObjects;

    public LinqDistinct()
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

            // `AddRange` is faster and more efficient than single `Add`s if the
            // source enumerable is a collection (count is known in advance).
            _baseObjects.AddRange(value);

            Span<BaseObject> baseObjects = CollectionsMarshal.AsSpan(_baseObjects);
            for (int i = 0; i < baseObjects.Length; ++i)
            {
                ref BaseObject item = ref baseObjects[i];
                if (item is null)
                {
                    // For backwards compatibility, throw the exact same exception as we are currently in this case.
                    _ = item!.GetObjectId();
                }
            }

            if (_baseObjects.Count != _baseObjects.DistinctBy(static baseObject => baseObject.GetObjectId()).Count())
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
        }
    }
}
