using IGB.Domain.Common;

namespace IGB.Domain.Entities;

public class DashboardPreference : BaseEntity
{
    public long UserId { get; set; }
    public User? User { get; set; }

    // e.g. "TutorDashboard", "AdminDashboard", etc.
    public string Scope { get; set; } = string.Empty;

    // Arbitrary JSON blob for widget order/visibility, default view, notification settings, etc.
    public string Json { get; set; } = "{}";
}


