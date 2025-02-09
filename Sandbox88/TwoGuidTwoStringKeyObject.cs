using Microsoft.Management.Services.Common;
using System.Runtime.Serialization;

[DataContract]
public sealed class TwoGuidTwoStringKeyObject : TwoGuidTwoStringKeyBaseObject
{
    /// <summary>
    /// Initializes a new instance of the TestTwoGuidTwoStringKeyObject class.
    /// </summary>
    /// <param name="key">Object key for the device (TwoGuidTwoStringKey).</param>
    public TwoGuidTwoStringKeyObject(Guid key1, Guid key2, string key3, string key4)
        : base(key1, key2, key3, key4)
    {
    }

    /// <summary>
    /// Gets or sets the GuidKey1 property
    /// </summary>
    [DataMember, FirstSupportedIn(1, 0)]
    public override Guid GuidKey1 { get; set; }

    /// <summary>
    /// Gets or sets the GuidKey2 property
    /// </summary>
    [DataMember, FirstSupportedIn(1, 0)]
    public override Guid GuidKey2 { get; set; }

    /// <summary>
    /// Gets or sets the StringKey1
    /// </summary>
    [DataMember, FirstSupportedIn(1, 0), EstimatedSize(204, 800)]
    public override string? StringKey1 { get; set; }

    /// <summary>
    /// Gets or sets the StringKey2
    /// </summary>
    [DataMember, FirstSupportedIn(1, 0), EstimatedSize(204, 800)]
    public override string? StringKey2 { get; set; }
}
