using Microsoft.Management.Services.Common;

public interface IBenchmarkTarget
{
    /// <summary>
    /// Resets the internal state of this class for benchmarking purposes.
    /// </summary>
    public void Reset();

    /// <summary>
    /// The property to be benchmarked.
    /// </summary>
    public IEnumerable<BaseObject> BaseObjects { get; set; }
}
