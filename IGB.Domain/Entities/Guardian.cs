using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class Guardian : BaseEntity
{
    public long StudentUserId { get; set; }
    public User? StudentUser { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string? Relationship { get; set; } // e.g. Father, Mother, Guardian
    public string? Email { get; set; }
    public string? LocalNumber { get; set; }
    public string? WhatsappNumber { get; set; }
    public bool IsPrimary { get; set; } = false;
}


