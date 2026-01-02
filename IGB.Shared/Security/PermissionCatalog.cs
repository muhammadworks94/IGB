namespace IGB.Shared.Security;

public static class PermissionCatalog
{
    public const string ClaimType = "perm";

    public static class Categories
    {
        public const string UserManagement = "User Management";
        public const string CourseManagement = "Course Management";
        public const string LessonManagement = "Lesson Management";
        public const string FinanceCredits = "Finance/Credits";
        public const string Feedback = "Feedback";
        public const string Progress = "Progress";
        public const string Reports = "Reports";
        public const string Tests = "Tests";
        public const string Announcements = "Announcements";
        public const string Settings = "Settings";
    }

    // Permissions (key strings). Keep keys stable.
    public static class Permissions
    {
        // User Management
        public const string UsersRead = "users.read";
        public const string UsersWrite = "users.write";
        public const string RolesManage = "roles.manage";
        public const string ApprovalsManage = "approvals.manage";

        // Course Management
        public const string CurriculaManage = "curricula.manage";
        public const string CoursesManage = "courses.manage";
        public const string TopicsManage = "topics.manage";
        public const string CourseBookingsManage = "coursebookings.manage";

        // Lesson Management
        public const string LessonRequestsManage = "lessons.manage";
        public const string LessonsViewOwn = "lessons.view.own";

        // Finance/Credits (placeholders)
        public const string CreditsView = "credits.view";
        public const string CreditsManage = "credits.manage";
        public const string EarningsView = "earnings.view";

        // Feedback
        public const string FeedbackViewOwn = "feedback.view.own";
        public const string FeedbackSubmit = "feedback.submit";
        public const string FeedbackManage = "feedback.manage";

        // Progress
        public const string ProgressViewOwn = "progress.view.own";
        public const string ProgressManage = "progress.manage";

        // Reports (placeholder)
        public const string ReportsView = "reports.view";

        // Tests / Test Reports
        public const string TestReportsViewOwn = "testreports.view.own";
        public const string TestReportsManage = "testreports.manage";
        public const string TestAnalyticsView = "testanalytics.view";

        // Announcements
        public const string AnnouncementsManage = "announcements.manage";

        // Settings
        public const string SettingsManage = "settings.manage";
    }

    public record PermissionDef(string Key, string Category, string Description);

    public static readonly IReadOnlyList<PermissionDef> All = new List<PermissionDef>
    {
        new(Permissions.UsersRead, Categories.UserManagement, "View users"),
        new(Permissions.UsersWrite, Categories.UserManagement, "Create/edit/delete users"),
        new(Permissions.RolesManage, Categories.UserManagement, "Create/edit/delete roles and assign permissions"),
        new(Permissions.ApprovalsManage, Categories.UserManagement, "Approve/reject users and tutors"),

        new(Permissions.CurriculaManage, Categories.CourseManagement, "Manage curricula and grades"),
        new(Permissions.CoursesManage, Categories.CourseManagement, "Manage courses"),
        new(Permissions.TopicsManage, Categories.CourseManagement, "Manage course topics"),
        new(Permissions.CourseBookingsManage, Categories.CourseManagement, "Approve/reject course bookings"),

        new(Permissions.LessonRequestsManage, Categories.LessonManagement, "Manage lesson requests / scheduling"),
        new(Permissions.LessonsViewOwn, Categories.LessonManagement, "View own lesson schedule"),

        new(Permissions.CreditsView, Categories.FinanceCredits, "View credits"),
        new(Permissions.CreditsManage, Categories.FinanceCredits, "Manage credits"),
        new(Permissions.EarningsView, Categories.FinanceCredits, "View earnings"),

        new(Permissions.FeedbackViewOwn, Categories.Feedback, "View feedback (own)"),
        new(Permissions.FeedbackSubmit, Categories.Feedback, "Submit feedback"),
        new(Permissions.FeedbackManage, Categories.Feedback, "View/flag feedback (admin)"),

        new(Permissions.ProgressViewOwn, Categories.Progress, "View progress (own / ward)"),
        new(Permissions.ProgressManage, Categories.Progress, "Manage progress (mark topics / notes)"),

        new(Permissions.ReportsView, Categories.Reports, "View reports"),

        new(Permissions.TestReportsViewOwn, Categories.Tests, "View own test reports"),
        new(Permissions.TestReportsManage, Categories.Tests, "Create/edit/delete test reports"),
        new(Permissions.TestAnalyticsView, Categories.Tests, "View test analytics"),

        new(Permissions.AnnouncementsManage, Categories.Announcements, "Create/publish announcements"),

        new(Permissions.SettingsManage, Categories.Settings, "Manage system settings"),
    };
}


