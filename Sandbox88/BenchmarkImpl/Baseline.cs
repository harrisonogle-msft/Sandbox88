using Microsoft.Management.Services.Common;

public struct Baseline : IBenchmarkTarget
{
    private IList<BaseObject> _baseObjects;

    public Baseline()
    {
        _baseObjects = new List<BaseObject>();
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
            if (object.ReferenceEquals(value, _baseObjects))
            {
                // Clearing the input would be a bug.
                return;
            }

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

            var keys = new HashSet<ObjectKey>();
            foreach (BaseObject item in value)
            {
                ObjectKey key = item.GetObjectId();
                if (keys.Contains(key))
                {
                    throw new InvalidClientRequestException(ApiErrorCode.DuplicateObjectKeyInRequest, "Duplicate object key found in request: " + key);
                }
                keys.Add(key);
                _baseObjects.Add(item);
            }
        }
    }
}
