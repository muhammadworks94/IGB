using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class UserDocument : BaseEntity
{
    public long UserId { get; set; }
    public User? User { get; set; }

    public string Type { get; set; } = string.Empty; // e.g. "TutorCv", "TutorCertificate"
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string FilePath { get; set; } = string.Empty; // relative under wwwroot/uploads
}


