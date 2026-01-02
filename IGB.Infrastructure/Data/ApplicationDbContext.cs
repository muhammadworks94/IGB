using IGB.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IGB.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Guardian> Guardians { get; set; }
    public DbSet<StudentProfile> StudentProfiles { get; set; }
    public DbSet<TutorProfile> TutorProfiles { get; set; }
    public DbSet<UserDocument> UserDocuments { get; set; }
    public DbSet<RbacRole> RbacRoles { get; set; }
    public DbSet<RbacPermission> RbacPermissions { get; set; }
    public DbSet<RbacRolePermission> RbacRolePermissions { get; set; }
    public DbSet<UserRbacRole> UserRbacRoles { get; set; }
    public DbSet<CreditLedgerEntry> CreditLedgerEntries { get; set; }
    public DbSet<CreditsBalance> CreditsBalances { get; set; }
    public DbSet<CreditTransaction> CreditTransactions { get; set; }
    public DbSet<CourseCreditLedger> CourseCreditLedgers { get; set; }
    public DbSet<CourseLedgerTransaction> CourseLedgerTransactions { get; set; }
    public DbSet<TutorEarningTransaction> TutorEarningTransactions { get; set; }
    public DbSet<TutorFeedback> TutorFeedbacks { get; set; }
    public DbSet<StudentFeedback> StudentFeedbacks { get; set; }
    public DbSet<FeedbackAttachment> FeedbackAttachments { get; set; }
    public DbSet<LessonTopicCoverage> LessonTopicCoverages { get; set; }
    public DbSet<StudentProgressNote> StudentProgressNotes { get; set; }
    public DbSet<GuardianWard> GuardianWards { get; set; }
    public DbSet<CourseReview> CourseReviews { get; set; }
    public DbSet<TutorAvailabilityRule> TutorAvailabilityRules { get; set; }
    public DbSet<TutorAvailabilityBlock> TutorAvailabilityBlocks { get; set; }
    public DbSet<LessonChangeLog> LessonChangeLogs { get; set; }
    public DbSet<Curriculum> Curricula { get; set; }
    public DbSet<Grade> Grades { get; set; }
    public DbSet<Course> Courses { get; set; }
    public DbSet<CourseTopic> CourseTopics { get; set; }
    public DbSet<CourseBooking> CourseBookings { get; set; }
    public DbSet<LessonBooking> LessonBookings { get; set; }
    public DbSet<Announcement> Announcements { get; set; }
    public DbSet<TestReport> TestReports { get; set; }
    public DbSet<TestReportTopic> TestReportTopics { get; set; }
    public DbSet<DashboardPreference> DashboardPreferences { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => new { e.Role, e.IsDeleted });
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(200);

            entity.Property(e => e.LocalNumber).HasMaxLength(25);
            entity.Property(e => e.WhatsappNumber).HasMaxLength(25);
            entity.Property(e => e.CountryCode).HasMaxLength(2);
            entity.Property(e => e.TimeZoneId).HasMaxLength(64);
            entity.Property(e => e.ProfileImagePath).HasMaxLength(512);

            entity.Property(e => e.EmailConfirmed).IsRequired();
            entity.Property(e => e.EmailConfirmationTokenHash).HasMaxLength(256);
            entity.Property(e => e.ApprovalStatus).IsRequired();
            entity.Property(e => e.ApprovalNote).HasMaxLength(500);

            entity.HasMany(e => e.Guardians)
                .WithOne(g => g.StudentUser!)
                .HasForeignKey(g => g.StudentUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Guardian>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.StudentUserId, e.IsDeleted });
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Relationship).HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.LocalNumber).HasMaxLength(25);
            entity.Property(e => e.WhatsappNumber).HasMaxLength(25);
        });

        modelBuilder.Entity<StudentProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.IsDeleted }).IsUnique();
            entity.Property(e => e.TimeZoneId).HasMaxLength(64);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Curriculum)
                .WithMany()
                .HasForeignKey(e => e.CurriculumId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Grade)
                .WithMany()
                .HasForeignKey(e => e.GradeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TutorProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.IsDeleted }).IsUnique();
            entity.Property(e => e.TimeZoneId).HasMaxLength(64);
            entity.Property(e => e.SpecialitiesJson).HasMaxLength(8000);
            entity.Property(e => e.EducationHistoryJson).HasMaxLength(12000);
            entity.Property(e => e.WorkExperienceJson).HasMaxLength(12000);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserDocument>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.IsDeleted });
            entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.FilePath).IsRequired().HasMaxLength(512);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RbacRole>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsSystem).IsRequired();
        });

        modelBuilder.Entity<RbacPermission>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Key).IsUnique();
            entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<RbacRolePermission>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.RoleId, e.PermissionId, e.IsDeleted }).IsUnique();
            entity.HasOne(e => e.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(e => e.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserRbacRole>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.RoleId, e.IsDeleted }).IsUnique();
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Curriculum>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Name, e.IsDeleted });
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.IsActive).IsRequired();
        });

        modelBuilder.Entity<Grade>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.CurriculumId, e.IsDeleted });
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IsActive).IsRequired();
            entity.HasOne(e => e.Curriculum)
                .WithMany(c => c.Grades)
                .HasForeignKey(e => e.CurriculumId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.GradeId, e.IsDeleted });
            entity.HasIndex(e => new { e.CurriculumId, e.IsDeleted });
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.ImagePath).HasMaxLength(512);
            entity.Property(e => e.CreditCost).IsRequired();
            entity.Property(e => e.IsActive).IsRequired();
            entity.HasOne(e => e.Curriculum)
                .WithMany()
                .HasForeignKey(e => e.CurriculumId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.TutorUser)
                .WithMany()
                .HasForeignKey(e => e.TutorUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Grade)
                .WithMany(g => g.Courses)
                .HasForeignKey(e => e.GradeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CreditLedgerEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.IsDeleted });
            entity.Property(e => e.DeltaCredits).IsRequired();
            entity.Property(e => e.Reason).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ReferenceType).HasMaxLength(50);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CreditsBalance>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.IsDeleted }).IsUnique();
            entity.Property(e => e.TotalCredits).IsRequired();
            entity.Property(e => e.UsedCredits).IsRequired();
            entity.Property(e => e.RemainingCredits).IsRequired();
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CreditTransaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.CreatedAt, e.IsDeleted });
            entity.Property(e => e.Amount).IsRequired();
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.ReferenceType).HasMaxLength(50);
            entity.Property(e => e.Reason).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.BalanceAfter).IsRequired();
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CourseCreditLedger>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.StudentUserId, e.CourseId, e.IsDeleted }).IsUnique();
            entity.Property(e => e.CreditsAllocated).IsRequired();
            entity.Property(e => e.CreditsUsed).IsRequired();
            entity.Property(e => e.CreditsRemaining).IsRequired();
            entity.HasOne(e => e.StudentUser)
                .WithMany()
                .HasForeignKey(e => e.StudentUserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Course)
                .WithMany()
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CourseLedgerTransaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.StudentUserId, e.CourseId, e.CreatedAt, e.IsDeleted });
            entity.Property(e => e.Amount).IsRequired();
            entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.HasOne(e => e.StudentUser)
                .WithMany()
                .HasForeignKey(e => e.StudentUserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Course)
                .WithMany()
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TutorEarningTransaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TutorUserId, e.CreatedAt, e.IsDeleted });
            entity.Property(e => e.CreditsEarned).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.HasOne(e => e.TutorUser)
                .WithMany()
                .HasForeignKey(e => e.TutorUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TutorFeedback>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.LessonBookingId, e.IsDeleted }).IsUnique();
            entity.HasIndex(e => new { e.TutorUserId, e.CreatedAt, e.IsDeleted });
            entity.Property(e => e.Comments).HasMaxLength(2000);
            entity.Property(e => e.FlagReason).HasMaxLength(500);

            entity.HasOne(e => e.LessonBooking).WithMany().HasForeignKey(e => e.LessonBookingId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Course).WithMany().HasForeignKey(e => e.CourseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.StudentUser).WithMany().HasForeignKey(e => e.StudentUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.TutorUser).WithMany().HasForeignKey(e => e.TutorUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StudentFeedback>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.LessonBookingId, e.IsDeleted }).IsUnique();
            entity.HasIndex(e => new { e.StudentUserId, e.CreatedAt, e.IsDeleted });
            entity.Property(e => e.Comments).HasMaxLength(2000);
            entity.Property(e => e.FlagReason).HasMaxLength(500);

            entity.HasOne(e => e.LessonBooking).WithMany().HasForeignKey(e => e.LessonBookingId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Course).WithMany().HasForeignKey(e => e.CourseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.StudentUser).WithMany().HasForeignKey(e => e.StudentUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.TutorUser).WithMany().HasForeignKey(e => e.TutorUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FeedbackAttachment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.StudentFeedbackId, e.IsDeleted });
            entity.Property(e => e.Kind).IsRequired().HasMaxLength(50);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.FilePath).IsRequired().HasMaxLength(512);
            entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
            entity.HasOne(e => e.StudentFeedback)
                .WithMany(f => f.Attachments)
                .HasForeignKey(e => e.StudentFeedbackId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LessonTopicCoverage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.LessonBookingId, e.CourseTopicId, e.IsDeleted }).IsUnique();
            entity.HasIndex(e => new { e.StudentUserId, e.CourseId, e.CreatedAt, e.IsDeleted });
            entity.HasOne(e => e.LessonBooking).WithMany().HasForeignKey(e => e.LessonBookingId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Course).WithMany().HasForeignKey(e => e.CourseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CourseTopic).WithMany().HasForeignKey(e => e.CourseTopicId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.StudentUser).WithMany().HasForeignKey(e => e.StudentUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.TutorUser).WithMany().HasForeignKey(e => e.TutorUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StudentProgressNote>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Note).IsRequired().HasMaxLength(2000);
            entity.HasIndex(e => new { e.StudentUserId, e.CourseId, e.CreatedAt, e.IsDeleted });
            entity.HasOne(e => e.StudentUser).WithMany().HasForeignKey(e => e.StudentUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.TutorUser).WithMany().HasForeignKey(e => e.TutorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Course).WithMany().HasForeignKey(e => e.CourseId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GuardianWard>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.GuardianUserId, e.StudentUserId, e.IsDeleted }).IsUnique();
            entity.HasOne(e => e.GuardianUser).WithMany().HasForeignKey(e => e.GuardianUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.StudentUser).WithMany().HasForeignKey(e => e.StudentUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CourseReview>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.CourseId, e.IsDeleted });
            entity.HasIndex(e => new { e.CourseId, e.StudentUserId, e.IsDeleted }).IsUnique();
            entity.Property(e => e.Rating).IsRequired();
            entity.Property(e => e.Comment).HasMaxLength(2000);
            entity.HasOne(e => e.Course)
                .WithMany()
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.StudentUser)
                .WithMany()
                .HasForeignKey(e => e.StudentUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TutorAvailabilityRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TutorUserId, e.DayOfWeek, e.IsDeleted });
            entity.Property(e => e.DayOfWeek).IsRequired();
            entity.Property(e => e.StartMinutes).IsRequired();
            entity.Property(e => e.EndMinutes).IsRequired();
            entity.Property(e => e.SlotMinutes).IsRequired();
            entity.Property(e => e.IsActive).IsRequired();
            entity.HasOne(e => e.TutorUser)
                .WithMany()
                .HasForeignKey(e => e.TutorUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TutorAvailabilityBlock>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TutorUserId, e.StartUtc, e.IsDeleted });
            entity.Property(e => e.StartUtc).IsRequired();
            entity.Property(e => e.EndUtc).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(200);
            entity.HasOne(e => e.TutorUser)
                .WithMany()
                .HasForeignKey(e => e.TutorUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CourseTopic>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.CourseId, e.IsDeleted });
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.SortOrder).IsRequired();
            entity.HasOne(e => e.Course)
                .WithMany(c => c.Topics)
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ParentTopic)
                .WithMany(p => p.SubTopics)
                .HasForeignKey(e => e.ParentTopicId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CourseBooking>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.CourseId, e.StudentUserId, e.IsDeleted });
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.HasOne(e => e.Course)
                .WithMany(c => c.Bookings)
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.StudentUser)
                .WithMany()
                .HasForeignKey(e => e.StudentUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.TutorUser)
                .WithMany()
                .HasForeignKey(e => e.TutorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LessonBooking>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.CourseId, e.StudentUserId, e.IsDeleted });
            entity.Property(e => e.DurationMinutes).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.RescheduleNote).HasMaxLength(500);
            entity.Property(e => e.DecisionNote).HasMaxLength(1000);
            entity.Property(e => e.CancellationNote).HasMaxLength(500);
            entity.Property(e => e.CancelReason).HasMaxLength(500);
            entity.Property(e => e.ZoomMeetingId).HasMaxLength(100);
            entity.Property(e => e.ZoomJoinUrl).HasMaxLength(1000);
            entity.Property(e => e.ZoomPassword).HasMaxLength(50);
            entity.Property(e => e.AttendanceNote).HasMaxLength(500);

            entity.HasOne(e => e.Course)
                .WithMany()
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.StudentUser)
                .WithMany()
                .HasForeignKey(e => e.StudentUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.TutorUser)
                .WithMany()
                .HasForeignKey(e => e.TutorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CourseBooking)
                .WithMany()
                .HasForeignKey(e => e.CourseBookingId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<LessonChangeLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.LessonBookingId, e.IsDeleted });
            entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.HasOne(e => e.LessonBooking)
                .WithMany()
                .HasForeignKey(e => e.LessonBookingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Announcement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Audience, e.IsPublished, e.PublishAtUtc, e.IsDeleted });
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Body).IsRequired().HasMaxLength(5000);
            entity.Property(e => e.IsPublished).IsRequired();
            entity.Property(e => e.PublishAtUtc).IsRequired();

            entity.HasOne(e => e.TargetStudentUser)
                .WithMany()
                .HasForeignKey(e => e.TargetStudentUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.TargetTutorUser)
                .WithMany()
                .HasForeignKey(e => e.TargetTutorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.TargetGuardianUser)
                .WithMany()
                .HasForeignKey(e => e.TargetGuardianUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TestReport>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.StudentUserId, e.CourseId, e.TestDate, e.IsDeleted });
            entity.HasIndex(e => new { e.TutorUserId, e.CourseId, e.TestDate, e.IsDeleted });
            entity.Property(e => e.TestName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Grade).IsRequired().HasMaxLength(4);
            entity.Property(e => e.Percentage).IsRequired().HasPrecision(5, 2);
            entity.Property(e => e.Strengths).HasMaxLength(500);
            entity.Property(e => e.AreasForImprovement).HasMaxLength(500);
            entity.Property(e => e.TutorComments).HasMaxLength(1000);
            entity.Property(e => e.TestFileUrl).HasMaxLength(512);
            entity.Property(e => e.TestFileName).HasMaxLength(255);
            entity.Property(e => e.TestFileContentType).HasMaxLength(100);

            entity.HasOne(e => e.StudentUser).WithMany().HasForeignKey(e => e.StudentUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.TutorUser).WithMany().HasForeignKey(e => e.TutorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Course).WithMany().HasForeignKey(e => e.CourseId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TestReportTopic>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TestReportId, e.CourseTopicId, e.IsDeleted }).IsUnique();
            entity.HasOne(e => e.TestReport)
                .WithMany(r => r.Topics)
                .HasForeignKey(e => e.TestReportId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.CourseTopic)
                .WithMany()
                .HasForeignKey(e => e.CourseTopicId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DashboardPreference>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Scope).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Json).IsRequired();
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.UserId, x.Scope, x.IsDeleted }).IsUnique();
        });
    }
}

