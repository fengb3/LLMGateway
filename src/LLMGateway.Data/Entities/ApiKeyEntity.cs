using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LLMGateway.Data.Entities;

[Table("ApiKeys")]
public class ApiKeyEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// SHA256 hash of the full API key.
    /// </summary>
    [Required]
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>
    /// Short prefix for display purposes (e.g. "sk-gw-ab12...").
    /// </summary>
    [Required]
    public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name / owner of this key.
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional expiration date. Null means the key never expires.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
}
