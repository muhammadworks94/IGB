using IGB.Domain.Common;
using IGB.Domain.Enums;

namespace IGB.Domain.Entities;

public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    // Phone + communication
    public string? LocalNumber { get; set; }
    public string? WhatsappNumber { get; set; }
    public string? CountryCode { get; set; } // ISO2, e.g. "AE", "GB"

    // Locale / timezone
    public string? TimeZoneId { get; set; } // Windows or IANA ID; we validate against server list

    // Account security
    public bool IsActive { get; set; } = true;
    public string PasswordHash { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; } = false;
    public string? EmailConfirmationTokenHash { get; set; }
    public DateTime? EmailConfirmationSentAt { get; set; }

    // Password reset (API + future UI flow)
    public string? PasswordResetTokenHash { get; set; }
    public DateTime? PasswordResetSentAt { get; set; }
    public DateTime? PasswordResetExpiresAt { get; set; }

    // Refresh token (single active session token; can be extended to multi-session later)
    public string? RefreshTokenHash { get; set; }
    public DateTime? RefreshTokenIssuedAt { get; set; }
    public DateTime? RefreshTokenExpiresAt { get; set; }

    // Approval workflow
    public UserApprovalStatus ApprovalStatus { get; set; } = UserApprovalStatus.Pending;
    public DateTime? ApprovedAt { get; set; }
    public long? ApprovedByUserId { get; set; }
    public string? ApprovalNote { get; set; }

    // Profile
    public string? ProfileImagePath { get; set; } // relative path under wwwroot/uploads
    
    public string FullName => $"{FirstName} {LastName}";

    // Relationships
    public ICollection<Guardian> Guardians { get; set; } = new List<Guardian>();
}

