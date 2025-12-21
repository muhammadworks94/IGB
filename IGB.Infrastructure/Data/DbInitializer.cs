using IGB.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BCrypt.Net;

namespace IGB.Infrastructure.Data;

public class DbInitializer
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DbInitializer> _logger;

    public DbInitializer(ApplicationDbContext context, ILogger<DbInitializer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Ensure database is created
            await _context.Database.MigrateAsync();

            // Seed initial data
            await SeedUsersAsync();
            await SeedCoursesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database");
            throw;
        }
    }

    private async Task SeedUsersAsync()
    {
        // Default password for all users: "Password123!"
        const string defaultPassword = "Password123!";

        // If users already exist (older DB), backfill missing PasswordHash so login works.
        if (await _context.Users.AnyAsync())
        {
            var usersNeedingHash = await _context.Users
                .Where(u => string.IsNullOrEmpty(u.PasswordHash))
                .ToListAsync();

            var usersNeedingEmailConfirm = await _context.Users
                .Where(u => !u.EmailConfirmed)
                .ToListAsync();

            // Ensure seeded accounts are approved (SuperAdmin + demo teachers/students) so you can log in.
            var seedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "superadmin@igb.com",
                "teacher1@igb.com",
                "teacher2@igb.com",
                "teacher3@igb.com",
                "student1@igb.com",
                "student2@igb.com",
                "student3@igb.com",
                "student4@igb.com",
                "student5@igb.com"
            };

            var usersNeedingApproval = await _context.Users
                .Where(u => seedEmails.Contains(u.Email) && u.ApprovalStatus != IGB.Domain.Enums.UserApprovalStatus.Approved)
                .ToListAsync();

            if (usersNeedingHash.Count == 0 && usersNeedingEmailConfirm.Count == 0 && usersNeedingApproval.Count == 0)
            {
                _logger.LogInformation("Users already exist and are up-to-date. Skipping seed/backfill.");
                return;
            }

            foreach (var user in usersNeedingHash)
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword);
                user.UpdatedAt = DateTime.UtcNow;
            }

            foreach (var user in usersNeedingEmailConfirm)
            {
                user.EmailConfirmed = true; // auto-confirm for existing DB users (dev-friendly)
                user.EmailConfirmationTokenHash = null;
                user.EmailConfirmationSentAt = null;
                user.UpdatedAt = DateTime.UtcNow;
            }

            foreach (var user in usersNeedingApproval)
            {
                user.ApprovalStatus = IGB.Domain.Enums.UserApprovalStatus.Approved;
                user.ApprovedAt = DateTime.UtcNow;
                user.ApprovedByUserId = null;
                user.ApprovalNote = "Auto-approved seeded account";
                user.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation(
                "Backfilled PasswordHash for {HashCount} users, auto-confirmed email for {ConfirmCount} users, and auto-approved {ApprovedCount} seeded users.",
                usersNeedingHash.Count,
                usersNeedingEmailConfirm.Count,
                usersNeedingApproval.Count);
            return;
        }
        
        var users = new List<User>
        {
            // SuperAdmin
            new User
            {
                Email = "superadmin@igb.com",
                FirstName = "Super",
                LastName = "Admin",
                Role = "SuperAdmin",
                LocalNumber = "+1234567890",
                WhatsappNumber = "+1234567890",
                CountryCode = "US",
                TimeZoneId = "UTC",
                IsActive = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword),
                EmailConfirmed = true,
                ApprovalStatus = IGB.Domain.Enums.UserApprovalStatus.Approved,
                ApprovedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            },
            // Teachers/Tutors
            new User
            {
                Email = "teacher1@igb.com",
                FirstName = "John",
                LastName = "Smith",
                Role = "Tutor",
                LocalNumber = "+1234567891",
                WhatsappNumber = "+1234567891",
                CountryCode = "US",
                TimeZoneId = "UTC",
                IsActive = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword),
                EmailConfirmed = true,
                ApprovalStatus = IGB.Domain.Enums.UserApprovalStatus.Approved,
                ApprovedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                Email = "teacher2@igb.com",
                FirstName = "Sarah",
                LastName = "Johnson",
                Role = "Tutor",
                LocalNumber = "+1234567892",
                WhatsappNumber = "+1234567892",
                CountryCode = "US",
                TimeZoneId = "UTC",
                IsActive = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword),
                EmailConfirmed = true,
                ApprovalStatus = IGB.Domain.Enums.UserApprovalStatus.Approved,
                ApprovedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                Email = "teacher3@igb.com",
                FirstName = "Michael",
                LastName = "Brown",
                Role = "Tutor",
                LocalNumber = "+1234567893",
                WhatsappNumber = "+1234567893",
                CountryCode = "US",
                TimeZoneId = "UTC",
                IsActive = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword),
                EmailConfirmed = true,
                ApprovalStatus = IGB.Domain.Enums.UserApprovalStatus.Approved,
                ApprovedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            },
            // Students
            new User
            {
                Email = "student1@igb.com",
                FirstName = "Emma",
                LastName = "Wilson",
                Role = "Student",
                LocalNumber = "+1234567894",
                WhatsappNumber = "+1234567894",
                CountryCode = "US",
                TimeZoneId = "UTC",
                IsActive = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword),
                EmailConfirmed = true,
                ApprovalStatus = IGB.Domain.Enums.UserApprovalStatus.Approved,
                ApprovedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                Email = "student2@igb.com",
                FirstName = "David",
                LastName = "Martinez",
                Role = "Student",
                LocalNumber = "+1234567895",
                WhatsappNumber = "+1234567895",
                CountryCode = "US",
                TimeZoneId = "UTC",
                IsActive = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword),
                EmailConfirmed = true,
                ApprovalStatus = IGB.Domain.Enums.UserApprovalStatus.Approved,
                ApprovedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                Email = "student3@igb.com",
                FirstName = "Sophia",
                LastName = "Anderson",
                Role = "Student",
                LocalNumber = "+1234567896",
                WhatsappNumber = "+1234567896",
                CountryCode = "US",
                TimeZoneId = "UTC",
                IsActive = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword),
                EmailConfirmed = true,
                ApprovalStatus = IGB.Domain.Enums.UserApprovalStatus.Approved,
                ApprovedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                Email = "student4@igb.com",
                FirstName = "James",
                LastName = "Taylor",
                Role = "Student",
                LocalNumber = "+1234567897",
                WhatsappNumber = "+1234567897",
                CountryCode = "US",
                TimeZoneId = "UTC",
                IsActive = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword),
                EmailConfirmed = true,
                ApprovalStatus = IGB.Domain.Enums.UserApprovalStatus.Approved,
                ApprovedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                Email = "student5@igb.com",
                FirstName = "Olivia",
                LastName = "Thomas",
                Role = "Student",
                LocalNumber = "+1234567898",
                WhatsappNumber = "+1234567898",
                CountryCode = "US",
                TimeZoneId = "UTC",
                IsActive = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword),
                EmailConfirmed = true,
                ApprovalStatus = IGB.Domain.Enums.UserApprovalStatus.Approved,
                ApprovedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            }
        };

        await _context.Users.AddRangeAsync(users);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Seeded {Count} users successfully", users.Count);
    }

    private async Task SeedCoursesAsync()
    {
        if (await _context.Curricula.AnyAsync())
        {
            _logger.LogInformation("Curricula already exist. Skipping courses seed.");
            return;
        }

        var curriculum = new Curriculum
        {
            Name = "British Curriculum",
            Description = "Sample curriculum seeded for development",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var grade = new Grade
        {
            Name = "Grade 5",
            Level = 5,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Curriculum = curriculum
        };

        var course = new Course
        {
            Name = "Mathematics",
            Description = "Core mathematics course",
            IsActive = true,
            CreditCost = 2,
            CreatedAt = DateTime.UtcNow,
            Grade = grade
        };

        var topic1 = new CourseTopic { Title = "Numbers", SortOrder = 1, CreatedAt = DateTime.UtcNow, Course = course };
        var topic2 = new CourseTopic { Title = "Fractions", SortOrder = 2, CreatedAt = DateTime.UtcNow, Course = course };
        var subTopic = new CourseTopic { Title = "Equivalent Fractions", SortOrder = 1, CreatedAt = DateTime.UtcNow, Course = course, ParentTopic = topic2 };

        await _context.Curricula.AddAsync(curriculum);
        await _context.Grades.AddAsync(grade);
        await _context.Courses.AddAsync(course);
        await _context.CourseTopics.AddRangeAsync(topic1, topic2, subTopic);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Seeded sample curriculum/grade/course/topics.");
    }
}

