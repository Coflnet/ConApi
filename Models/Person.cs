using Cassandra.Mapping.Attributes;
using Coflnet.Connections.Models;

namespace Coflnet.Connections;

/// <summary>
/// Core person entity with unique identifier
/// </summary>
public class Person : BaseEntity
{
    /// <summary>
    /// Birth name (never changes, used for uniqueness with birthday and birthplace)
    /// </summary>
    [PartitionKey(1)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Date of birth
    /// </summary>
    [ClusteringKey(0)]
    public DateTime Birthday { get; set; }
    
    /// <summary>
    /// Place of birth (helps uniqueness)
    /// </summary>
    [ClusteringKey(1)]
    public string BirthPlace { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name (can change - married name, nickname, etc.)
    /// </summary>
    public string? DisplayName { get; set; }
    
    /// <summary>
    /// Gender
    /// </summary>
    public Gender Gender { get; set; }
    
    /// <summary>
    /// Date of death (null if alive or unknown)
    /// </summary>
    public DateTime? DeathDate { get; set; }
    
    /// <summary>
    /// Place of death
    /// </summary>
    public string? DeathPlace { get; set; }

    /// <summary>
    /// Flexible user-defined attributes stored as a map in the Person row.
    /// Key/value pairs are both strings.
    /// </summary>
    public IDictionary<string, string>? Attributes { get; set; }
}

/// <summary>
/// Gender options
/// </summary>
public enum Gender
{
    Unknown = 0,
    Male = 1,
    Female = 2,
    Other = 3
}

// Keep the existing PersonData for flexible attributes
