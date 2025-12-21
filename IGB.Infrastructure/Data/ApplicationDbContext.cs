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
    public DbSet<Curriculum> Curricula { get; set; }
    public DbSet<Grade> Grades { get; set; }
    public DbSet<Course> Courses { get; set; }
    public DbSet<CourseTopic> CourseTopics { get; set; }
    public DbSet<CourseBooking> CourseBookings { get; set; }
    public DbSet<LessonBooking> LessonBookings { get; set; }
    public DbSet<Announcement> Announcements { get; set; }

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
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.CreditCost).IsRequired();
            entity.Property(e => e.IsActive).IsRequired();
            entity.HasOne(e => e.Grade)
                .WithMany(g => g.Courses)
                .HasForeignKey(e => e.GradeId)
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
            entity.Property(e => e.ZoomMeetingId).HasMaxLength(100);
            entity.Property(e => e.ZoomJoinUrl).HasMaxLength(1000);
            entity.Property(e => e.ZoomPassword).HasMaxLength(50);

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
    }
}

