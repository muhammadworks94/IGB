using IGB.Domain.Entities;
using IGB.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BCrypt.Net;
using IGB.Shared.Security;
using System.Text;
using System.Text.RegularExpressions;

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
            await SeedRbacAsync();
            await SeedUsersAsync();
            await SeedCoursesAsync();
            await SeedCreditsAsync(); // legacy (kept)

            // Additional demo seed data (at least 10+ records per module)
            // NOTE: we no longer generate "Student7 Demo" / "Course 6" style data.
            // Instead we seed realistic-looking names + subjects + grades.
            await EnsureRealisticBulkSeedAsync(); // real names + bigger catalog (students/tutors/subjects/classes)
            await SeedCreditsV2Async();
            await SeedEnrollmentsAndLessonsAsync();
            await SeedRequestsAsync();
            await SeedProgressAndFeedbackAsync();
            await SeedGuardiansAsync();
            await SeedTestReportsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database");
            throw;
        }
    }

    private async Task SeedRbacAsync()
    {
        // Seed Permissions
        var existingPerms = await _context.RbacPermissions.AsNoTracking().ToListAsync();
        var byKey = existingPerms.ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;

        foreach (var def in PermissionCatalog.All)
        {
            if (byKey.ContainsKey(def.Key)) continue;
            _context.RbacPermissions.Add(new IGB.Domain.Entities.RbacPermission
            {
                Key = def.Key,
                Category = def.Category,
                Description = def.Description,
                CreatedAt = now
            });
        }
        await _context.SaveChangesAsync();

        // Seed System Roles
        async Task<IGB.Domain.Entities.RbacRole> EnsureRole(string name, string desc)
        {
            var role = await _context.RbacRoles.FirstOrDefaultAsync(r => r.Name == name);
            if (role != null) return role;
            role = new IGB.Domain.Entities.RbacRole { Name = name, Description = desc, IsSystem = true, CreatedAt = now };
            _context.RbacRoles.Add(role);
            await _context.SaveChangesAsync();
            return role;
        }

        var admin = await EnsureRole("Admin", "Full administrative access");
        var tutor = await EnsureRole("Tutor", "Tutor access");
        var student = await EnsureRole("Student", "Student access");
        var guardian = await EnsureRole("Guardian", "Guardian access");

        // Assign base permissions
        var perms = await _context.RbacPermissions.AsNoTracking().ToListAsync();
        long P(string key) => perms.First(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Id;

        var rolePerms = await _context.RbacRolePermissions.Where(x => !x.IsDeleted).ToListAsync();
        bool Has(long rid, long pid) => rolePerms.Any(x => x.RoleId == rid && x.PermissionId == pid);

        void Add(long rid, string key)
        {
            var pid = P(key);
            if (Has(rid, pid)) return;
            _context.RbacRolePermissions.Add(new IGB.Domain.Entities.RbacRolePermission
            {
                RoleId = rid,
                PermissionId = pid,
                CreatedAt = now
            });
        }

        // Admin: almost everything
        foreach (var def in PermissionCatalog.All)
            Add(admin.Id, def.Key);

        // Tutor: lesson management + announcements read (not yet a permission) - keep minimal
        Add(tutor.Id, PermissionCatalog.Permissions.LessonRequestsManage);
        Add(tutor.Id, PermissionCatalog.Permissions.LessonsViewOwn);
        Add(tutor.Id, PermissionCatalog.Permissions.TestReportsManage);
        Add(tutor.Id, PermissionCatalog.Permissions.TestReportsViewOwn);

        // Student: view own lessons + credits view
        Add(student.Id, PermissionCatalog.Permissions.LessonsViewOwn);
        Add(student.Id, PermissionCatalog.Permissions.CreditsView);
        Add(student.Id, PermissionCatalog.Permissions.ProgressViewOwn);
        Add(student.Id, PermissionCatalog.Permissions.FeedbackViewOwn);
        Add(student.Id, PermissionCatalog.Permissions.FeedbackSubmit);
        Add(student.Id, PermissionCatalog.Permissions.TestReportsViewOwn);

        // Guardian: reports view (placeholder)
        Add(guardian.Id, PermissionCatalog.Permissions.ReportsView);
        Add(guardian.Id, PermissionCatalog.Permissions.ProgressViewOwn);
        Add(guardian.Id, PermissionCatalog.Permissions.TestReportsViewOwn);

        await _context.SaveChangesAsync();
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

    private async Task EnsureDemoUsersAsync()
    {
        // Ensures we have enough demo users to satisfy "10 records per section" needs.
        const string defaultPassword = "Password123!";
        var now = DateTime.UtcNow;

        var existingEmails = await _context.Users.AsNoTracking().Select(u => u.Email).ToListAsync();
        var set = existingEmails.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toAdd = new List<User>();

        // Add more tutors (teacher4..teacher5)
        for (int i = 4; i <= 5; i++)
        {
            var email = $"teacher{i}@igb.com";
            if (set.Contains(email)) continue;
            toAdd.Add(new User
            {
                Email = email,
                FirstName = $"Tutor{i}",
                LastName = "Demo",
                Role = "Tutor",
                TimeZoneId = "UTC",
                IsActive = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword),
                EmailConfirmed = true,
                ApprovalStatus = UserApprovalStatus.Approved,
                ApprovedAt = now,
                CreatedAt = now
            });
        }

        // Add more students (student6..student10)
        for (int i = 6; i <= 10; i++)
        {
            var email = $"student{i}@igb.com";
            if (set.Contains(email)) continue;
            toAdd.Add(new User
            {
                Email = email,
                FirstName = $"Student{i}",
                LastName = "Demo",
                Role = "Student",
                TimeZoneId = "UTC",
                IsActive = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword),
                EmailConfirmed = true,
                ApprovalStatus = UserApprovalStatus.Approved,
                ApprovedAt = now,
                CreatedAt = now
            });
        }

        // Guardians (guardian1..guardian2) - basic seeding, realistic names added in EnsureRealisticBulkSeedAsync
        for (int i = 1; i <= 2; i++)
        {
            var email = $"guardian{i}@igb.com";
            if (set.Contains(email)) continue;
            toAdd.Add(new User
            {
                Email = email,
                FirstName = $"Guardian{i}",
                LastName = "Demo",
                Role = "Guardian",
                TimeZoneId = "UTC",
                IsActive = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword),
                EmailConfirmed = true,
                ApprovalStatus = UserApprovalStatus.Approved,
                ApprovedAt = now,
                CreatedAt = now
            });
        }

        // Pending approvals (show up in Pending User Approvals page): pending tutors 1..10
        for (int i = 1; i <= 10; i++)
        {
            var email = $"pendingtutor{i}@igb.com";
            if (set.Contains(email)) continue;
            toAdd.Add(new User
            {
                Email = email,
                FirstName = $"PendingTutor{i}",
                LastName = "Approval",
                Role = "Tutor",
                TimeZoneId = "UTC",
                IsActive = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword),
                EmailConfirmed = true,
                ApprovalStatus = UserApprovalStatus.Pending,
                CreatedAt = now.AddMinutes(-i)
            });
        }

        if (toAdd.Count == 0) return;
        await _context.Users.AddRangeAsync(toAdd);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Ensured demo users: added {Count} accounts.", toAdd.Count);
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
            Curriculum = curriculum,
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

    private async Task EnsureDemoCoursesAndTopicsAsync()
    {
        // Ensure larger demo catalog so UI lists/pagination have data.
        // Targets: 10 curricula, 20 grades, 20 courses, 100+ topics.
        var now = DateTime.UtcNow;

        var curriculaCount = await _context.Curricula.AsNoTracking().CountAsync(c => !c.IsDeleted);
        if (curriculaCount < 10)
        {
            for (int i = curriculaCount + 1; i <= 10; i++)
            {
                _context.Curricula.Add(new Curriculum
                {
                    Name = $"Curriculum {i}",
                    Description = "Seeded curriculum",
                    IsActive = true,
                    CreatedAt = now.AddDays(-i)
                });
            }
            await _context.SaveChangesAsync();
        }

        var curricula = await _context.Curricula.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Id).Take(10).ToListAsync();

        var gradesCount = await _context.Grades.AsNoTracking().CountAsync(g => !g.IsDeleted);
        if (gradesCount < 20)
        {
            int idx = gradesCount;
            foreach (var cur in curricula)
            {
                for (int i = 1; i <= 2; i++)
                {
                    idx++;
                    _context.Grades.Add(new Grade
                    {
                        CurriculumId = cur.Id,
                        Name = $"Grade {i} ({cur.Name})",
                        Level = i + 1,
                        IsActive = true,
                        CreatedAt = now.AddDays(-idx)
                    });
                }
            }
            await _context.SaveChangesAsync();
        }

        var grades = await _context.Grades.AsNoTracking().Where(g => !g.IsDeleted).OrderBy(g => g.Id).Take(20).ToListAsync();

        var existingCourses = await _context.Courses.AsNoTracking().Where(c => !c.IsDeleted).ToListAsync();

        // Add courses if fewer than 20
        if (existingCourses.Count < 20)
        {
            var tutorId = await _context.Users.AsNoTracking().Where(u => !u.IsDeleted && u.Role == "Tutor").Select(u => u.Id).FirstOrDefaultAsync();
            var needed = 20 - existingCourses.Count;
            for (int i = 1; i <= needed; i++)
            {
                var g = grades[(i - 1) % grades.Count];
                var curId = g.CurriculumId;
                var c = new Course
                {
                    Name = $"Course {existingCourses.Count + i}",
                    Description = "Seeded course for demos",
                    IsActive = true,
                    CreditCost = 2,
                    CurriculumId = curId,
                    GradeId = g.Id,
                    TutorUserId = tutorId > 0 ? tutorId : null,
                    CreatedAt = now
                };
                _context.Courses.Add(c);
            }
            await _context.SaveChangesAsync();
        }

        var courses = await _context.Courses.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Id).Take(20).ToListAsync();

        // Ensure topics
        var topicCount = await _context.CourseTopics.AsNoTracking().CountAsync(t => !t.IsDeleted);
        if (topicCount >= 120) return;

        var toAdd = new List<CourseTopic>();
        foreach (var c in courses)
        {
            // Create 4 main topics per course
            for (int i = 1; i <= 4; i++)
            {
                var main = new CourseTopic { CourseId = c.Id, Title = $"Topic {i}", SortOrder = i, CreatedAt = now };
                toAdd.Add(main);
            }
        }
        await _context.CourseTopics.AddRangeAsync(toAdd);
        await _context.SaveChangesAsync();

        // Add subtopics to first few main topics to enrich hierarchy
        var mains = await _context.CourseTopics.AsNoTracking().Where(t => !t.IsDeleted && !t.ParentTopicId.HasValue).OrderBy(t => t.Id).Take(10).ToListAsync();
        var subs = new List<CourseTopic>();
        int sidx = 1;
        foreach (var m in mains)
        {
            subs.Add(new CourseTopic { CourseId = m.CourseId, ParentTopicId = m.Id, Title = $"Subtopic {sidx}", SortOrder = 1, CreatedAt = now });
            sidx++;
        }
        await _context.CourseTopics.AddRangeAsync(subs);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Ensured demo courses/topics.");
    }

    private async Task EnsureRealisticBulkSeedAsync()
    {
        _logger.LogInformation("Realistic seed: started.");
        // Goal: realistic-looking data for demos/tests (no "Student1 Demo", no "Course 6", etc.).
        // Targets (minimums):
        // - 100 approved students with real names
        // - 30 approved tutors with real names
        // - 12 grades/classes (Grade 1..12) in British Curriculum
        // - 10-20 subject courses with real names (Math, Biology, etc.)

        const string defaultPassword = "Password123!";
        var now = DateTime.UtcNow;
        var rng = new Random(20251225);

        // ---------- Rename placeholder curricula (Curriculum 2..10) to real names ----------
        var curriculumNames = new[]
        {
            "British Curriculum",
            "Cambridge International",
            "Edexcel",
            "IB (International Baccalaureate)",
            "American Curriculum",
            "Ontario Curriculum",
            "Australian Curriculum",
            "CBSE",
            "ICSE",
            "Singapore Curriculum"
        };

        var curriculaAll = await _context.Curricula.Where(c => !c.IsDeleted).OrderBy(c => c.Id).ToListAsync();
        foreach (var c in curriculaAll)
        {
            if (Regex.IsMatch(c.Name ?? "", @"^Curriculum\s+\d+$", RegexOptions.IgnoreCase))
            {
                // Deterministic rename based on Id so it stays stable
                var pick = curriculumNames[(int)(Math.Abs(c.Id) % curriculumNames.Length)];
                c.Name = pick;
                c.Description ??= "Seeded curriculum";
                c.IsActive = true;
                c.UpdatedAt = now;
            }
        }
        if (curriculaAll.Count > 0) await _context.SaveChangesAsync();

        // ---------- Ensure British Curriculum + Grades 1..12 ----------
        // EF Core can't translate string.Contains(string, StringComparison) to SQL; use LIKE instead.
        var british = await _context.Curricula.FirstOrDefaultAsync(c => !c.IsDeleted && EF.Functions.Like(c.Name, "%British%"));
        if (british == null)
        {
            british = new Curriculum
            {
                Name = "British Curriculum",
                Description = "Seeded curriculum for realistic demos",
                IsActive = true,
                CreatedAt = now
            };
            _context.Curricula.Add(british);
            await _context.SaveChangesAsync();
        }

        var existingGrades = await _context.Grades.Where(g => !g.IsDeleted && g.CurriculumId == british.Id).ToListAsync();
        var gradeByLevel = existingGrades
            .Where(g => g.Level.HasValue && g.Level.Value >= 1 && g.Level.Value <= 12)
            .GroupBy(g => g.Level!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Id).First());

        for (int level = 1; level <= 12; level++)
        {
            if (gradeByLevel.ContainsKey(level)) continue;
            _context.Grades.Add(new Grade
            {
                CurriculumId = british.Id,
                Name = $"Grade {level}",
                Level = level,
                IsActive = true,
                CreatedAt = now.AddMinutes(-level)
            });
        }
        await _context.SaveChangesAsync();

        var grades12 = await _context.Grades.AsNoTracking()
            .Where(g => !g.IsDeleted && g.CurriculumId == british.Id && g.Level.HasValue && g.Level.Value >= 1 && g.Level.Value <= 12)
            .OrderBy(g => g.Level)
            .ToListAsync();

        // ---------- Ensure Tutors + Students with real names ----------
        // We keep existing seed accounts (superadmin/teacher1.. etc) but rename the "Demo" ones to realistic names.
        var firstNames = new[]
        {
            "Ayesha","Hassan","Fatima","Omar","Zainab","Ali","Maryam","Bilal","Sara","Hamza","Noor","Yusuf","Hiba","Ibrahim","Amna","Ahmed","Eman","Usman","Sofia","Adeel",
            "Emily","Michael","Sarah","James","Olivia","David","Emma","Daniel","Sophia","William","Ava","Benjamin","Mia","Lucas","Charlotte","Henry","Amelia","Ethan","Grace","Alexander",
            "Priya","Arjun","Ananya","Rohan","Kavya","Ishaan","Meera","Rahul","Diya","Vikram","Neha","Amit","Sanya","Karan","Nisha","Aditya","Pooja","Suresh","Isha","Manish"
        };
        var lastNames = new[]
        {
            "Khan","Ahmed","Ali","Hussain","Malik","Shaikh","Chaudhry","Butt","Iqbal","Raza","Siddiqui","Qureshi","Sheikh","Farooq","Aslam",
            "Smith","Johnson","Williams","Brown","Jones","Garcia","Miller","Davis","Rodriguez","Martinez","Hernandez","Lopez","Gonzalez","Wilson","Anderson",
            "Taylor","Thomas","Moore","Jackson","Martin","Lee","Perez","Thompson","White","Harris","Sanchez","Clark","Ramirez","Lewis","Robinson",
            "Patel","Sharma","Gupta","Mehta","Kapoor","Singh","Kaur","Iyer","Nair","Reddy","Chopra","Malhotra","Joshi","Bose","Verma"
        };

        static string Slug(string s) =>
            Regex.Replace((s ?? "").Trim().ToLowerInvariant(), @"[^a-z0-9]+", ".").Trim('.');

        string NewEmail(string first, string last, int attempt)
        {
            var basePart = $"{Slug(first)}.{Slug(last)}";
            if (string.IsNullOrWhiteSpace(basePart) || basePart == ".") basePart = $"user{attempt}";
            return $"{basePart}{attempt:000}@igb.com";
        }

        var existingUsers = await _context.Users.Where(u => !u.IsDeleted).ToListAsync();
        var emailSet = existingUsers.Select(u => u.Email).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Rename existing dummy users like "Student9 Demo", "Tutor4 Demo", "PendingTutor1 Approval"
        bool IsGenericUser(User u)
        {
            if (u == null) return false;
            if (!(u.Role == "Student" || u.Role == "Tutor" || u.Role == "Guardian")) return false;
            if (u.LastName != null && u.LastName.Trim().Equals("Demo", StringComparison.OrdinalIgnoreCase)) return true;
            if (u.FirstName != null && Regex.IsMatch(u.FirstName.Trim(), @"^(Student|Tutor|Guardian)\d+$", RegexOptions.IgnoreCase)) return true;
            if (u.Email != null && Regex.IsMatch(u.Email.Trim(), @"^(student|teacher|guardian)\d+@igb\.com$", RegexOptions.IgnoreCase)) return true;
            return false;
        }

        string StableFirst(long id) => firstNames[(int)(Math.Abs(id) % firstNames.Length)];
        string StableLast(long id) => lastNames[(int)(Math.Abs(id * 7) % lastNames.Length)];

        var genericUsers = existingUsers.Where(IsGenericUser).OrderBy(u => u.Id).ToList();
        foreach (var u in genericUsers)
        {
            // Deterministic, stable names (so it doesn't shuffle every restart).
            var fn = StableFirst(u.Id);
            var ln = StableLast(u.Id);
            u.FirstName = fn;
            u.LastName = ln;
            u.EmailConfirmed = true;
            if (u.ApprovalStatus != UserApprovalStatus.Approved)
            {
                u.ApprovalStatus = UserApprovalStatus.Approved;
                u.ApprovedAt = now;
                u.ApprovalNote = "Auto-approved (seed)";
            }
            if (string.IsNullOrWhiteSpace(u.PasswordHash))
                u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword);
            u.IsActive = true;
            u.TimeZoneId = string.IsNullOrWhiteSpace(u.TimeZoneId) ? "UTC" : u.TimeZoneId;
            u.UpdatedAt = now;
        }
        if (genericUsers.Count > 0) await _context.SaveChangesAsync();

        // Pending approvals: keep them, but make names realistic (still pending).
        var pending = await _context.Users.Where(u => !u.IsDeleted && u.Role == "Tutor" && u.ApprovalStatus == UserApprovalStatus.Pending).ToListAsync();
        foreach (var u in pending)
        {
            if (u.FirstName != null && Regex.IsMatch(u.FirstName.Trim(), @"^PendingTutor\d+$", RegexOptions.IgnoreCase))
            {
                u.FirstName = StableFirst(u.Id);
                u.LastName = StableLast(u.Id);
                u.EmailConfirmed = true;
                u.IsActive = true;
                if (string.IsNullOrWhiteSpace(u.PasswordHash))
                    u.PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword);
                u.UpdatedAt = now;
            }
        }
        if (pending.Count > 0) await _context.SaveChangesAsync();

        // Ensure at least 50 approved tutors (real names)
        var tutorCount = await _context.Users.AsNoTracking()
            .CountAsync(u => !u.IsDeleted && u.Role == "Tutor" && u.ApprovalStatus == UserApprovalStatus.Approved);

        var toAddUsers = new List<User>();

        int tutorAttempt = 1;
        while (tutorCount + toAddUsers.Count(u => u.Role == "Tutor") < 50)
        {
            var fn = firstNames[rng.Next(firstNames.Length)];
            var ln = lastNames[rng.Next(lastNames.Length)];
            var email = NewEmail(fn, ln, tutorAttempt++);
            if (emailSet.Contains(email)) continue;
            emailSet.Add(email);

            toAddUsers.Add(new User
            {
                Email = email,
                FirstName = fn,
                LastName = ln,
                Role = "Tutor",
                TimeZoneId = "UTC",
                IsActive = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword),
                EmailConfirmed = true,
                ApprovalStatus = UserApprovalStatus.Approved,
                ApprovedAt = now,
                CreatedAt = now.AddSeconds(-tutorAttempt)
            });
        }

        // Ensure at least 100 approved students (real names)
        var studentCount = await _context.Users.AsNoTracking()
            .CountAsync(u => !u.IsDeleted && u.Role == "Student" && u.ApprovalStatus == UserApprovalStatus.Approved);

        int studentAttempt = 500; // offset so emails are unique vs tutors
        while (studentCount + toAddUsers.Count(u => u.Role == "Student") < 100)
        {
            var fn = firstNames[rng.Next(firstNames.Length)];
            var ln = lastNames[rng.Next(lastNames.Length)];
            var email = NewEmail(fn, ln, studentAttempt++);
            if (emailSet.Contains(email)) continue;
            emailSet.Add(email);

            toAddUsers.Add(new User
            {
                Email = email,
                FirstName = fn,
                LastName = ln,
                Role = "Student",
                TimeZoneId = "UTC",
                IsActive = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword),
                EmailConfirmed = true,
                ApprovalStatus = UserApprovalStatus.Approved,
                ApprovedAt = now,
                CreatedAt = now.AddSeconds(-studentAttempt)
            });
        }

        // Ensure at least 20 approved guardians (real names with contact info)
        var guardianCount = await _context.Users.AsNoTracking()
            .CountAsync(u => !u.IsDeleted && u.Role == "Guardian" && u.ApprovalStatus == UserApprovalStatus.Approved);

        int guardianAttempt = 1000; // offset so emails are unique
        while (guardianCount + toAddUsers.Count(u => u.Role == "Guardian") < 20)
        {
            var fn = firstNames[rng.Next(firstNames.Length)];
            var ln = lastNames[rng.Next(lastNames.Length)];
            var email = NewEmail(fn, ln, guardianAttempt++);
            if (emailSet.Contains(email)) continue;
            emailSet.Add(email);

            var phoneSuffix = (9000 + guardianAttempt).ToString().PadLeft(7, '0');
            toAddUsers.Add(new User
            {
                Email = email,
                FirstName = fn,
                LastName = ln,
                Role = "Guardian",
                LocalNumber = $"+1{phoneSuffix}",
                WhatsappNumber = $"+1{phoneSuffix}",
                CountryCode = "US",
                TimeZoneId = "UTC",
                IsActive = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword),
                EmailConfirmed = true,
                ApprovalStatus = UserApprovalStatus.Approved,
                ApprovedAt = now,
                CreatedAt = now.AddSeconds(-guardianAttempt)
            });
        }

        if (toAddUsers.Count > 0)
        {
            await _context.Users.AddRangeAsync(toAddUsers);
            await _context.SaveChangesAsync();
        }

        // Ensure profiles exist (lightweight but helps profile pages)
        var tutors = await _context.Users.AsNoTracking().Where(u => !u.IsDeleted && u.Role == "Tutor" && u.ApprovalStatus == UserApprovalStatus.Approved).OrderBy(u => u.Id).Take(50).ToListAsync();
        var students = await _context.Users.AsNoTracking().Where(u => !u.IsDeleted && u.Role == "Student" && u.ApprovalStatus == UserApprovalStatus.Approved).OrderBy(u => u.Id).Take(100).ToListAsync();

        var tutorIds = tutors.Select(t => t.Id).ToList();
        var studentIds = students.Select(s => s.Id).ToList();

        var existingTutorProfiles = await _context.TutorProfiles.AsNoTracking().Where(p => !p.IsDeleted && tutorIds.Contains(p.UserId)).Select(p => p.UserId).ToListAsync();
        var existingStudentProfiles = await _context.StudentProfiles.AsNoTracking().Where(p => !p.IsDeleted && studentIds.Contains(p.UserId)).Select(p => p.UserId).ToListAsync();

        var tutorProfileSet = existingTutorProfiles.ToHashSet();
        var studentProfileSet = existingStudentProfiles.ToHashSet();

        foreach (var t in tutors)
        {
            if (tutorProfileSet.Contains(t.Id)) continue;
            var spec = new[] { "Mathematics", "English", "Science", "Computer Science", "History", "Geography" };
            var specPick = spec[rng.Next(spec.Length)];
            _context.TutorProfiles.Add(new TutorProfile
            {
                UserId = t.Id,
                TimeZoneId = "UTC",
                SpecialitiesJson = $"[\"{specPick}\"]",
                CreatedAt = now
            });
        }

        foreach (var s in students)
        {
            if (studentProfileSet.Contains(s.Id)) continue;
            var g = grades12[(int)(s.Id % grades12.Count)];
            _context.StudentProfiles.Add(new StudentProfile
            {
                UserId = s.Id,
                CurriculumId = british.Id,
                GradeId = g.Id,
                TimeZoneId = "UTC",
                CreatedAt = now
            });
        }
        await _context.SaveChangesAsync();

        // ---------- Ensure Subject Courses (10-20) + rename placeholders like "Course 5" ----------
        var subjects = new List<string>
        {
            "Mathematics",
            "English Language",
            "English Literature",
            "Biology",
            "Chemistry",
            "Physics",
            "Computer Science",
            "History",
            "Geography",
            "Economics",
            "Business Studies",
            "Accounting",
            "Art & Design",
            "Music",
            "Physical Education",
            "French",
            "Spanish",
            "Islamic Studies",
            "Urdu",
            "Statistics"
        };

        var allCourses = await _context.Courses
            .Include(c => c.Grade)
            .Where(c => !c.IsDeleted)
            .ToListAsync();

        var existingCourseNames = allCourses.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Rename placeholder courses into real names (deterministic based on course id)
        var placeholderCourses = allCourses
            .Where(c => Regex.IsMatch(c.Name ?? "", @"^(Course|Science)\s+\d+$", RegexOptions.IgnoreCase))
            .OrderBy(c => c.Id)
            .ToList();

        foreach (var c in placeholderCourses)
        {
            var subj = subjects[(int)(Math.Abs(c.Id) % subjects.Count)];
            var gradeSuffix = c.Grade != null ? $" (Grade {c.Grade.Level ?? 0})" : "";
            var newName = subj;
            if (existingCourseNames.Contains(newName))
                newName = subj + gradeSuffix;
            if (existingCourseNames.Contains(newName))
                newName = $"{subj} {c.Id}";

            existingCourseNames.Remove(c.Name);
            c.Name = newName;
            c.Description = $"Core {subj} course";
            c.IsActive = true;
            c.UpdatedAt = now;
            existingCourseNames.Add(newName);
        }
        if (placeholderCourses.Count > 0) await _context.SaveChangesAsync();

        // Ensure at least 10 (or up to 20) real subjects exist
        var targetSubjects = Math.Min(20, Math.Max(10, subjects.Count));
        foreach (var subj in subjects.Take(targetSubjects))
        {
            if (existingCourseNames.Contains(subj)) continue;
            var g = grades12[rng.Next(grades12.Count)];
            var tutorId = tutors.Count == 0 ? (long?)null : tutors[rng.Next(tutors.Count)].Id;
            _context.Courses.Add(new Course
            {
                CurriculumId = british.Id,
                GradeId = g.Id,
                Name = subj,
                Description = $"Core {subj} course",
                IsActive = true,
                CreditCost = 2,
                TutorUserId = tutorId,
                CreatedAt = now
            });
        }
        await _context.SaveChangesAsync();

        // ---------- Ensure Realistic Topic Names (replace Topic 1/Subtopic garbage) ----------
        // We rename in-place to avoid breaking references; only soft-delete truly unused duplicates.
        string CanonicalCourseName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "General";
            // strip suffix like " (Grade 5)"
            var idx = name.IndexOf(" (Grade", StringComparison.OrdinalIgnoreCase);
            if (idx > 0) name = name.Substring(0, idx);
            return name.Trim();
        }

        IReadOnlyList<string> TopicsFor(string subject)
        {
            subject = (subject ?? "").Trim();
            return subject switch
            {
                "Mathematics" => new[] { "Number & Place Value", "Fractions & Decimals", "Ratio & Proportion", "Algebra", "Geometry", "Statistics" },
                "English Language" => new[] { "Reading Comprehension", "Grammar", "Writing Skills", "Vocabulary", "Speaking & Listening", "Creative Writing" },
                "English Literature" => new[] { "Poetry Analysis", "Prose & Fiction", "Drama", "Themes & Context", "Character Study", "Essay Writing" },
                "Biology" => new[] { "Cells & Organisms", "Human Body Systems", "Genetics", "Ecology", "Evolution", "Practical Skills" },
                "Chemistry" => new[] { "Atomic Structure", "Chemical Reactions", "Acids & Bases", "Periodic Table", "Bonding", "Practical Skills" },
                "Physics" => new[] { "Forces & Motion", "Energy", "Waves", "Electricity", "Magnetism", "Practical Skills" },
                "Computer Science" => new[] { "Algorithms", "Programming Basics", "Data Structures", "Computer Systems", "Networks", "Cybersecurity" },
                "History" => new[] { "Source Analysis", "Chronology & Timelines", "Key Events", "Causes & Consequences", "Historical Interpretations", "Essay Skills" },
                "Geography" => new[] { "Maps & Fieldwork", "Rivers & Coasts", "Weather & Climate", "Population", "Resources", "Environmental Issues" },
                "Economics" => new[] { "Supply & Demand", "Markets", "Inflation & Unemployment", "Government Policy", "International Trade", "Data Interpretation" },
                "Business Studies" => new[] { "Entrepreneurship", "Marketing", "Operations", "Finance Basics", "Human Resources", "Business Strategy" },
                "Accounting" => new[] { "Double-Entry", "Ledgers", "Trial Balance", "Income Statement", "Balance Sheet", "Cash Flow" },
                "Art & Design" => new[] { "Drawing Techniques", "Colour Theory", "Composition", "Art History", "Creative Process", "Portfolio Building" },
                "Music" => new[] { "Rhythm & Meter", "Pitch & Melody", "Harmony", "Composition", "Music Theory", "Performance Skills" },
                "Physical Education" => new[] { "Fitness", "Rules & Tactics", "Skill Development", "Teamwork", "Sportsmanship", "Health & Nutrition" },
                "French" => new[] { "Vocabulary", "Grammar", "Reading", "Writing", "Listening", "Speaking" },
                "Spanish" => new[] { "Vocabulary", "Grammar", "Reading", "Writing", "Listening", "Speaking" },
                "Islamic Studies" => new[] { "Aqidah (Belief)", "Seerah (Life of Prophet)", "Qur'an Studies", "Hadith Studies", "Fiqh (Practice)", "Akhlaq (Character)" },
                "Urdu" => new[] { "Reading", "Writing", "Grammar", "Vocabulary", "Poetry", "Comprehension" },
                "Statistics" => new[] { "Data Collection", "Charts & Graphs", "Measures of Central Tendency", "Probability", "Sampling", "Interpretation" },
                _ => new[] { "Fundamentals", "Core Concepts", "Practice", "Assessment Skills", "Problem Solving", "Revision" }
            };
        }

        IReadOnlyList<string> SubtopicsFor(string mainTopic)
        {
            // Simple generic subtopics per main topic (still real-sounding)
            return new[] { $"{mainTopic}: Basics", $"{mainTopic}: Practice" };
        }

        var referencedTopicIds = (await _context.LessonTopicCoverages.AsNoTracking()
                .Where(x => !x.IsDeleted)
                .Select(x => x.CourseTopicId)
                .Distinct()
                .ToListAsync())
            .ToHashSet();

        var referencedInReports = (await _context.TestReportTopics.AsNoTracking()
                .Where(x => !x.IsDeleted)
                .Select(x => x.CourseTopicId)
                .Distinct()
                .ToListAsync())
            .ToHashSet();

        bool IsReferenced(long topicId) => referencedTopicIds.Contains(topicId) || referencedInReports.Contains(topicId);

        var courseList = await _context.Courses.AsNoTracking()
            .Where(c => !c.IsDeleted && c.CurriculumId == british.Id)
            .OrderBy(c => c.Id)
            .ToListAsync();

        foreach (var course in courseList)
        {
            var subject = CanonicalCourseName(course.Name);
            var desired = TopicsFor(subject);

            var existingMain = await _context.CourseTopics
                .Where(t => !t.IsDeleted && t.CourseId == course.Id && !t.ParentTopicId.HasValue)
                .OrderBy(t => t.SortOrder).ThenBy(t => t.Id)
                .ToListAsync();

            // Ensure at least desired.Count main topics with good names
            for (int i = 0; i < desired.Count; i++)
            {
                if (i < existingMain.Count)
                {
                    var t = existingMain[i];
                    // Replace garbage titles like "Topic 1"
                    if (Regex.IsMatch(t.Title ?? "", @"^(Topic|Main Topic)\s*\d*$", RegexOptions.IgnoreCase) || string.IsNullOrWhiteSpace(t.Title))
                        t.Title = desired[i];
                    else
                        t.Title = desired[i]; // enforce consistency per course

                    t.SortOrder = i + 1;
                    t.UpdatedAt = now;
                }
                else
                {
                    _context.CourseTopics.Add(new CourseTopic
                    {
                        CourseId = course.Id,
                        Title = desired[i],
                        SortOrder = i + 1,
                        CreatedAt = now
                    });
                }
            }
            await _context.SaveChangesAsync();

            // Reload mains after potential inserts
            existingMain = await _context.CourseTopics
                .Where(t => !t.IsDeleted && t.CourseId == course.Id && !t.ParentTopicId.HasValue)
                .OrderBy(t => t.SortOrder).ThenBy(t => t.Id)
                .ToListAsync();

            // Handle extra mains beyond desired: delete if unused, otherwise rename to an extension topic
            for (int i = desired.Count; i < existingMain.Count; i++)
            {
                var extra = existingMain[i];
                if (!IsReferenced(extra.Id))
                {
                    extra.IsDeleted = true;
                    extra.UpdatedAt = now;
                }
                else
                {
                    extra.Title = $"Extension: {subject} Skills";
                    extra.SortOrder = i + 1;
                    extra.UpdatedAt = now;
                }
            }
            await _context.SaveChangesAsync();

            // Subtopics: ensure 2 per main topic, rename garbage "Subtopic X"
            var mainsForSubs = await _context.CourseTopics
                .Where(t => !t.IsDeleted && t.CourseId == course.Id && !t.ParentTopicId.HasValue)
                .OrderBy(t => t.SortOrder).ThenBy(t => t.Id)
                .Take(desired.Count)
                .ToListAsync();

            foreach (var main in mainsForSubs)
            {
                var desiredSubs = SubtopicsFor(main.Title ?? "Topic");
                var subs = await _context.CourseTopics
                    .Where(t => !t.IsDeleted && t.CourseId == course.Id && t.ParentTopicId == main.Id)
                    .OrderBy(t => t.SortOrder).ThenBy(t => t.Id)
                    .ToListAsync();

                for (int i = 0; i < desiredSubs.Count; i++)
                {
                    if (i < subs.Count)
                    {
                        var st = subs[i];
                        st.Title = desiredSubs[i];
                        st.SortOrder = i + 1;
                        st.UpdatedAt = now;
                    }
                    else
                    {
                        _context.CourseTopics.Add(new CourseTopic
                        {
                            CourseId = course.Id,
                            ParentTopicId = main.Id,
                            Title = desiredSubs[i],
                            SortOrder = i + 1,
                            CreatedAt = now
                        });
                    }
                }

                // Extra subs: delete if unused; else rename
                for (int i = desiredSubs.Count; i < subs.Count; i++)
                {
                    var extra = subs[i];
                    if (!IsReferenced(extra.Id))
                    {
                        extra.IsDeleted = true;
                        extra.UpdatedAt = now;
                    }
                    else
                    {
                        extra.Title = $"{main.Title}: Extension Practice";
                        extra.SortOrder = i + 1;
                        extra.UpdatedAt = now;
                    }
                }

                await _context.SaveChangesAsync();
            }
        }

        // Summary counts (helps verify seeding actually ran)
        var cStudents = await _context.Users.AsNoTracking().CountAsync(u => !u.IsDeleted && u.Role == "Student" && u.ApprovalStatus == UserApprovalStatus.Approved);
        var cTutors = await _context.Users.AsNoTracking().CountAsync(u => !u.IsDeleted && u.Role == "Tutor" && u.ApprovalStatus == UserApprovalStatus.Approved);
        var cGuardians = await _context.Users.AsNoTracking().CountAsync(u => !u.IsDeleted && u.Role == "Guardian" && u.ApprovalStatus == UserApprovalStatus.Approved);
        var cCurricula = await _context.Curricula.AsNoTracking().CountAsync(c => !c.IsDeleted);
        var cGrades = await _context.Grades.AsNoTracking().CountAsync(g => !g.IsDeleted);
        var cCourses = await _context.Courses.AsNoTracking().CountAsync(c => !c.IsDeleted);
        var cTopics = await _context.CourseTopics.AsNoTracking().CountAsync(t => !t.IsDeleted);
        _logger.LogInformation("Realistic seed: done. Students={Students}, Tutors={Tutors}, Guardians={Guardians}, Curricula={Curricula}, Grades={Grades}, Courses={Courses}, Topics={Topics}",
            cStudents, cTutors, cGuardians, cCurricula, cGrades, cCourses, cTopics);
    }

    private async Task SeedCreditsAsync()
    {
        // Give demo credits to student users if they have none
        var students = await _context.Users.AsNoTracking()
            .Where(u => !u.IsDeleted && u.Role == "Student")
            .Select(u => u.Id)
            .ToListAsync();

        if (students.Count == 0) return;

        var existingAny = await _context.CreditLedgerEntries.AsNoTracking()
            .AnyAsync(e => !e.IsDeleted && students.Contains(e.UserId));

        if (existingAny) return;

        var now = DateTime.UtcNow;
        foreach (var sid in students)
        {
            _context.CreditLedgerEntries.Add(new IGB.Domain.Entities.CreditLedgerEntry
            {
                UserId = sid,
                DeltaCredits = 10,
                Reason = "Initial allocation",
                ReferenceType = "Seed",
                ReferenceId = null,
                CreatedAt = now
            });
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Seeded initial credits for {Count} students", students.Count);
    }

    private async Task SeedCreditsV2Async()
    {
        // Seed new credit system tables if needed: CreditsBalances + CreditTransactions + Course credit ledgers.
        var students = await _context.Users.AsNoTracking()
            .Where(u => !u.IsDeleted && u.Role == "Student")
            .Select(u => u.Id)
            .ToListAsync();

        if (students.Count == 0) return;

        var now = DateTime.UtcNow;
        var existingBalances = await _context.CreditsBalances.AsNoTracking().CountAsync(b => !b.IsDeleted);
        if (existingBalances < 10)
        {
            foreach (var sid in students.Take(10))
            {
                var exists = await _context.CreditsBalances.AnyAsync(b => !b.IsDeleted && b.UserId == sid);
                if (exists) continue;
                _context.CreditsBalances.Add(new CreditsBalance
                {
                    UserId = sid,
                    TotalCredits = 50,
                    UsedCredits = 0,
                    RemainingCredits = 50,
                    CreatedAt = now
                });
            }
            await _context.SaveChangesAsync();
        }

        // Ensure at least 30 wallet transactions
        var txCount = await _context.CreditTransactions.AsNoTracking().CountAsync(t => !t.IsDeleted);
        if (txCount < 30)
        {
            var balances = await _context.CreditsBalances.Where(b => !b.IsDeleted).ToDictionaryAsync(b => b.UserId);
            var toAdd = new List<CreditTransaction>();
            int n = 0;
            foreach (var sid in students.Take(10))
            {
                if (!balances.TryGetValue(sid, out var bal)) continue;

                // 3 transactions per student = 30 total for 10 students
                void AddTx(int amount, CreditTransactionType type, string reason)
                {
                    if (amount == 0) return;
                    if (amount > 0) { bal.TotalCredits += amount; bal.RemainingCredits += amount; }
                    else { var abs = -amount; bal.UsedCredits += abs; bal.RemainingCredits -= abs; }
                    bal.UpdatedAt = now;
                    toAdd.Add(new CreditTransaction
                    {
                        UserId = sid,
                        Amount = amount,
                        Type = type,
                        Reason = reason,
                        Notes = "Seed",
                        BalanceAfter = bal.RemainingCredits,
                        CreatedAt = now.AddMinutes(-n)
                    });
                    n++;
                }

                AddTx(+20, CreditTransactionType.Purchase, "Seed purchase");
                AddTx(+5, CreditTransactionType.Bonus, "Seed bonus");
                AddTx(-3, CreditTransactionType.Adjustment, "Seed usage");
            }

            _context.CreditTransactions.AddRange(toAdd);
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation("Ensured credits V2 seed data.");
    }

    private async Task SeedEnrollmentsAndLessonsAsync()
    {
        // Ensure realistic scale: enrollments + lessons so dashboards/attendance/schedules are full.
        var students = await _context.Users.AsNoTracking()
            .Where(u => !u.IsDeleted && u.Role == "Student" && u.ApprovalStatus == UserApprovalStatus.Approved)
            .OrderBy(u => u.Id)
            .Take(100)
            .ToListAsync();
        var tutors = await _context.Users.AsNoTracking()
            .Where(u => !u.IsDeleted && u.Role == "Tutor" && u.ApprovalStatus == UserApprovalStatus.Approved)
            .OrderBy(u => u.Id)
            .Take(50)
            .ToListAsync();
        var courses = await _context.Courses.AsNoTracking()
            .Where(c => !c.IsDeleted && c.IsActive)
            .OrderBy(c => c.Id)
            .Take(20)
            .ToListAsync();
        if (students.Count == 0 || tutors.Count == 0 || courses.Count == 0) return;

        var now = DateTime.UtcNow;

        // Ensure 2 approved bookings per student (200 total target)
        var studentIds = students.Select(s => s.Id).ToList();
        var existingApproved = await _context.CourseBookings.AsNoTracking()
            .Where(b => !b.IsDeleted && b.Status == BookingStatus.Approved && studentIds.Contains(b.StudentUserId))
            .Select(b => new { b.StudentUserId, b.CourseId })
            .ToListAsync();

        var byStudent = existingApproved
            .GroupBy(x => x.StudentUserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.CourseId).ToHashSet());

        var addBookings = new List<CourseBooking>();
        foreach (var s in students)
        {
            byStudent.TryGetValue(s.Id, out var set);
            set ??= new HashSet<long>();
            var need = Math.Max(0, 2 - set.Count);
            if (need == 0) continue;

            // pick distinct courses for this student
            var startIdx = (int)(s.Id % courses.Count);
            for (int k = 0; k < courses.Count && need > 0; k++)
            {
                var course = courses[(startIdx + k) % courses.Count];
                if (set.Contains(course.Id)) continue;
                set.Add(course.Id);
                need--;

                var tutor = tutors[(int)((s.Id + course.Id) % tutors.Count)];
                addBookings.Add(new CourseBooking
                {
                    CourseId = course.Id,
                    StudentUserId = s.Id,
                    TutorUserId = tutor.Id,
                    Status = BookingStatus.Approved,
                    RequestedAt = now.AddDays(-30),
                    DecisionAt = now.AddDays(-29),
                    CreatedAt = now.AddDays(-30)
                });
            }
        }

        if (addBookings.Count > 0)
        {
            _context.CourseBookings.AddRange(addBookings);
            await _context.SaveChangesAsync();
        }

        var bookings = await _context.CourseBookings.AsNoTracking()
            .Include(b => b.Course)
            .Where(b => !b.IsDeleted && b.Status == BookingStatus.Approved)
            .OrderBy(b => b.Id)
            .Take(200)
            .ToListAsync();

        var lessonCount = await _context.LessonBookings.AsNoTracking().CountAsync(l => !l.IsDeleted);
        if (lessonCount < 800)
        {
            var bookingIds = bookings.Select(b => b.Id).ToList();
            var existingLessonCounts = await _context.LessonBookings.AsNoTracking()
                .Where(l => !l.IsDeleted && l.CourseBookingId.HasValue && bookingIds.Contains(l.CourseBookingId.Value))
                .GroupBy(l => l.CourseBookingId!.Value)
                .Select(g => new { BookingId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.BookingId, x => x.Count);

            var toAdd = new List<LessonBooking>();
            int idx = 0;
            foreach (var b in bookings)
            {
                // Avoid duplicates: if this booking already has enough lessons, skip
                if (existingLessonCounts.TryGetValue(b.Id, out var lc) && lc >= 5) continue;

                // Create 2 lessons per booking: one completed, one scheduled
                var start1 = DateTimeOffset.UtcNow.AddDays(-idx - 2).Date.AddHours(10);
                var start2 = DateTimeOffset.UtcNow.AddDays(idx + 1).Date.AddHours(11);
                idx++;

                toAdd.Add(new LessonBooking
                {
                    CourseBookingId = b.Id,
                    CourseId = b.CourseId,
                    StudentUserId = b.StudentUserId,
                    TutorUserId = b.TutorUserId,
                    DateFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-14)),
                    DateTo = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(14)),
                    Option1 = start1,
                    Option2 = start1.AddHours(1),
                    Option3 = start1.AddHours(2),
                    DurationMinutes = 60,
                    ScheduledStart = start1,
                    ScheduledEnd = start1.AddMinutes(60),
                    Status = LessonStatus.Completed,
                    SessionStartedAt = start1.AddMinutes(1),
                    SessionEndedAt = start1.AddMinutes(60),
                    StudentJoinedAt = start1.AddMinutes(1),
                    TutorJoinedAt = start1.AddMinutes(0),
                    StudentAttended = (idx % 9) != 0, // ~11% absent
                    TutorAttended = (idx % 17) != 0,  // ~6% absent
                    ZoomJoinUrl = "https://zoom.us/j/123456789",
                    ZoomMeetingId = "123456789",
                    CreatedAt = now.AddDays(-idx - 2)
                });

                toAdd.Add(new LessonBooking
                {
                    CourseBookingId = b.Id,
                    CourseId = b.CourseId,
                    StudentUserId = b.StudentUserId,
                    TutorUserId = b.TutorUserId,
                    DateFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date),
                    DateTo = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(14)),
                    Option1 = start2,
                    Option2 = start2.AddHours(1),
                    Option3 = start2.AddHours(2),
                    DurationMinutes = 60,
                    ScheduledStart = start2,
                    ScheduledEnd = start2.AddMinutes(60),
                    Status = LessonStatus.Scheduled,
                    ZoomJoinUrl = "https://zoom.us/j/123456789",
                    ZoomMeetingId = "123456789",
                    CreatedAt = now.AddDays(-1)
                });

                // add one more upcoming to populate calendars
                var start3 = DateTimeOffset.UtcNow.AddDays(idx + 7).Date.AddHours(12);
                toAdd.Add(new LessonBooking
                {
                    CourseBookingId = b.Id,
                    CourseId = b.CourseId,
                    StudentUserId = b.StudentUserId,
                    TutorUserId = b.TutorUserId,
                    DateFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date),
                    DateTo = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(30)),
                    Option1 = start3,
                    Option2 = start3.AddHours(1),
                    Option3 = start3.AddHours(2),
                    DurationMinutes = 60,
                    ScheduledStart = start3,
                    ScheduledEnd = start3.AddMinutes(60),
                    Status = LessonStatus.Scheduled,
                    ZoomJoinUrl = "https://zoom.us/j/123456789",
                    ZoomMeetingId = "123456789",
                    CreatedAt = now.AddDays(-1)
                });

                // add one rescheduled request candidate (scheduled in future)
                var start4 = DateTimeOffset.UtcNow.AddDays((idx % 20) + 3).Date.AddHours(14);
                toAdd.Add(new LessonBooking
                {
                    CourseBookingId = b.Id,
                    CourseId = b.CourseId,
                    StudentUserId = b.StudentUserId,
                    TutorUserId = b.TutorUserId,
                    DateFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date),
                    DateTo = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(30)),
                    Option1 = start4,
                    Option2 = start4.AddHours(1),
                    Option3 = start4.AddHours(2),
                    DurationMinutes = 60,
                    Status = LessonStatus.Pending,
                    CreatedAt = now.AddDays(-2)
                });
            }

            _context.LessonBookings.AddRange(toAdd);
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation("Ensured realistic enrollments and lessons (students={Students}, tutors={Tutors}, courses={Courses}).", students.Count, tutors.Count, courses.Count);
    }

    private async Task SeedRequestsAsync()
    {
        // Seed "requests" workflows: pending enrollments, pending lesson requests, reschedule requests, cancellation requests.
        var now = DateTime.UtcNow;
        var students = await _context.Users.AsNoTracking()
            .Where(u => !u.IsDeleted && u.Role == "Student" && u.ApprovalStatus == UserApprovalStatus.Approved)
            .OrderBy(u => u.Id).Take(50).ToListAsync();
        var tutors = await _context.Users.AsNoTracking()
            .Where(u => !u.IsDeleted && u.Role == "Tutor" && u.ApprovalStatus == UserApprovalStatus.Approved)
            .OrderBy(u => u.Id).Take(20).ToListAsync();
        var courses = await _context.Courses.AsNoTracking()
            .Where(c => !c.IsDeleted && c.IsActive)
            .OrderBy(c => c.Id).Take(10).ToListAsync();
        if (students.Count == 0 || tutors.Count == 0 || courses.Count == 0) return;

        // Pending course bookings: ensure >= 30
        var pendingEnroll = await _context.CourseBookings.AsNoTracking().CountAsync(b => !b.IsDeleted && b.Status == BookingStatus.Pending);
        if (pendingEnroll < 30)
        {
            var add = new List<CourseBooking>();
            int idx = 0;
            foreach (var s in students)
            {
                // Try multiple courses per student to reach the minimum
                var c = courses[(idx++) % courses.Count];
                var exists = await _context.CourseBookings.AnyAsync(b => !b.IsDeleted && b.StudentUserId == s.Id && b.CourseId == c.Id);
                if (exists) continue;
                add.Add(new CourseBooking
                {
                    CourseId = c.Id,
                    StudentUserId = s.Id,
                    TutorUserId = tutors[idx % tutors.Count].Id,
                    Status = BookingStatus.Pending,
                    RequestedAt = now.AddDays(-1),
                    CreatedAt = now.AddDays(-1)
                });
                if (add.Count >= 30) break;
            }
            // If still short, keep filling by pairing students with other courses
            if (add.Count < 30)
            {
                foreach (var c in courses)
                {
                    foreach (var s in students)
                    {
                        if (add.Count >= 30) break;
                        var exists = await _context.CourseBookings.AnyAsync(b => !b.IsDeleted && b.StudentUserId == s.Id && b.CourseId == c.Id);
                        if (exists) continue;
                        add.Add(new CourseBooking
                        {
                            CourseId = c.Id,
                            StudentUserId = s.Id,
                            TutorUserId = tutors[(int)(s.Id % tutors.Count)].Id,
                            Status = BookingStatus.Pending,
                            RequestedAt = now.AddHours(-add.Count),
                            CreatedAt = now.AddHours(-add.Count)
                        });
                    }
                    if (add.Count >= 30) break;
                }
            }
            if (add.Count > 0)
            {
                _context.CourseBookings.AddRange(add);
                await _context.SaveChangesAsync();
            }
        }

        // Pending lesson requests: ensure >= 30
        var pendingLessons = await _context.LessonBookings.AsNoTracking().CountAsync(l => !l.IsDeleted && l.Status == LessonStatus.Pending);
        if (pendingLessons < 30)
        {
            var approvedBookings = await _context.CourseBookings.AsNoTracking()
                .Where(b => !b.IsDeleted && b.Status == BookingStatus.Approved)
                .OrderBy(b => b.Id)
                .Take(30)
                .ToListAsync();

            var add = new List<LessonBooking>();
            int idx = 0;
            foreach (var b in approvedBookings)
            {
                var baseDay = DateTimeOffset.UtcNow.AddDays(3 + idx).Date.AddHours(10);
                add.Add(new LessonBooking
                {
                    CourseBookingId = b.Id,
                    CourseId = b.CourseId,
                    StudentUserId = b.StudentUserId,
                    TutorUserId = b.TutorUserId,
                    DateFrom = DateOnly.FromDateTime(DateTime.UtcNow.Date),
                    DateTo = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(14)),
                    Option1 = baseDay,
                    Option2 = baseDay.AddHours(1),
                    Option3 = baseDay.AddHours(2),
                    DurationMinutes = 60,
                    Status = LessonStatus.Pending,
                    CreatedAt = now.AddHours(-idx)
                });
                idx++;
            }
            _context.LessonBookings.AddRange(add);
            await _context.SaveChangesAsync();
        }

        // Reschedule requests: ensure >= 20
        var resReq = await _context.LessonBookings.AsNoTracking().CountAsync(l => !l.IsDeleted && l.Status == LessonStatus.RescheduleRequested);
        if (resReq < 20)
        {
            var sched = await _context.LessonBookings.AsNoTracking()
                .Where(l => !l.IsDeleted && (l.Status == LessonStatus.Scheduled || l.Status == LessonStatus.Rescheduled) && l.ScheduledStart.HasValue)
                .OrderByDescending(l => l.ScheduledStart)
                .Take(20)
                .ToListAsync();

            foreach (var l in sched)
            {
                var entity = await _context.LessonBookings.FirstOrDefaultAsync(x => x.Id == l.Id);
                if (entity == null) continue;
                entity.Status = LessonStatus.RescheduleRequested;
                entity.RescheduleRequested = true;
                entity.RescheduleRequestedAt = DateTimeOffset.UtcNow;
                entity.RescheduleNote = "Seed reschedule request";
                entity.Option1 = (entity.ScheduledStart!.Value).AddDays(2);
                entity.Option2 = (entity.ScheduledStart!.Value).AddDays(2).AddHours(1);
                entity.Option3 = (entity.ScheduledStart!.Value).AddDays(2).AddHours(2);
                entity.UpdatedAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();
        }

        // Cancellation requests (tutor initiated): ensure >= 20 (uses Status = CancellationRequested)
        var cancelReq = await _context.LessonBookings.AsNoTracking().CountAsync(l => !l.IsDeleted && l.Status == LessonStatus.CancellationRequested);
        if (cancelReq < 20)
        {
            var sched = await _context.LessonBookings.AsNoTracking()
                .Where(l => !l.IsDeleted && l.Status == LessonStatus.Scheduled && l.ScheduledStart.HasValue)
                .OrderBy(l => l.ScheduledStart)
                .Take(20)
                .ToListAsync();

            foreach (var l in sched)
            {
                var entity = await _context.LessonBookings.FirstOrDefaultAsync(x => x.Id == l.Id);
                if (entity == null) continue;
                entity.Status = LessonStatus.CancellationRequested;
                entity.CancellationRequested = true;
                entity.CancellationRequestedAt = DateTimeOffset.UtcNow;
                entity.CancellationRequestedByUserId = entity.TutorUserId;
                entity.CancellationNote = "Seed cancellation request";
                entity.UpdatedAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation("Ensured demo request workflows (pending enrollments, pending lessons, reschedules, cancellations).");
    }

    private async Task SeedProgressAndFeedbackAsync()
    {
        // Seed: GuardianWards (10), ProgressNotes (10), LessonTopicCoverages (>=20), Feedbacks (10+ each), Attachments (10+), TutorEarnings (10+)
        var students = await _context.Users.AsNoTracking()
            .Where(u => !u.IsDeleted && u.Role == "Student" && u.ApprovalStatus == UserApprovalStatus.Approved)
            .OrderBy(u => u.Id).Take(50).ToListAsync();
        var tutors = await _context.Users.AsNoTracking()
            .Where(u => !u.IsDeleted && u.Role == "Tutor" && u.ApprovalStatus == UserApprovalStatus.Approved)
            .OrderBy(u => u.Id).Take(50).ToListAsync();
        var guardians = await _context.Users.AsNoTracking()
            .Where(u => !u.IsDeleted && u.Role == "Guardian" && u.ApprovalStatus == UserApprovalStatus.Approved)
            .OrderBy(u => u.Id).Take(10).ToListAsync();
        var topics = await _context.CourseTopics.AsNoTracking().Where(t => !t.IsDeleted).OrderBy(t => t.Id).Take(200).ToListAsync();
        var completedLessons = await _context.LessonBookings.AsNoTracking()
            .Include(l => l.Course)
            .Where(l => !l.IsDeleted && l.Status == LessonStatus.Completed && l.Course != null && l.TutorUserId.HasValue)
            .OrderBy(l => l.Id)
            .Take(200)
            .ToListAsync();

        var now = DateTime.UtcNow;

        // Guardian wards: map guardian1 -> 50 students
        if (guardians.Count > 0)
        {
            var gid = guardians[0].Id;
            var existing = await _context.GuardianWards.AsNoTracking().CountAsync(w => !w.IsDeleted);
            if (existing < 50)
            {
                foreach (var s in students)
                {
                    var has = await _context.GuardianWards.AnyAsync(w => !w.IsDeleted && w.GuardianUserId == gid && w.StudentUserId == s.Id);
                    if (has) continue;
                    _context.GuardianWards.Add(new GuardianWard { GuardianUserId = gid, StudentUserId = s.Id, CreatedAt = now });
                }
                await _context.SaveChangesAsync();
            }
        }

        // Progress notes: 100
        var noteCount = await _context.StudentProgressNotes.AsNoTracking().CountAsync(n => !n.IsDeleted);
        if (noteCount < 100 && completedLessons.Count > 0)
        {
            var toAdd = new List<StudentProgressNote>();
            for (int i = 0; i < 100; i++)
            {
                var l = completedLessons[i % completedLessons.Count];
                toAdd.Add(new StudentProgressNote
                {
                    StudentUserId = l.StudentUserId,
                    TutorUserId = l.TutorUserId!.Value,
                    CourseId = l.CourseId,
                    Note = "Progress note: focus on revision, practice questions, and improving consistency between sessions.",
                    CreatedAt = now.AddDays(-i)
                });
            }
            _context.StudentProgressNotes.AddRange(toAdd);
            await _context.SaveChangesAsync();
        }

        // Topic coverage: at least 400
        var covCount = await _context.LessonTopicCoverages.AsNoTracking().CountAsync(c => !c.IsDeleted);
        if (covCount < 400 && completedLessons.Count > 0 && topics.Count > 0)
        {
            var toAdd = new List<LessonTopicCoverage>();
            int idx = 0;
            foreach (var l in completedLessons.Take(150))
            {
                // cover 2 topics per lesson
                for (int j = 0; j < 2; j++)
                {
                    var t = topics[(idx + j) % topics.Count];
                    // Ensure topic belongs to course if possible; otherwise skip strictness
                    if (t.CourseId != l.CourseId) continue;
                    var exists = await _context.LessonTopicCoverages.AnyAsync(x => !x.IsDeleted && x.LessonBookingId == l.Id && x.CourseTopicId == t.Id);
                    if (exists) continue;
                    toAdd.Add(new LessonTopicCoverage
                    {
                        LessonBookingId = l.Id,
                        CourseId = l.CourseId,
                        CourseTopicId = t.Id,
                        StudentUserId = l.StudentUserId,
                        TutorUserId = l.TutorUserId!.Value,
                        CreatedAt = now.AddDays(-idx)
                    });
                }
                idx++;
            }
            if (toAdd.Count > 0)
            {
                _context.LessonTopicCoverages.AddRange(toAdd);
                await _context.SaveChangesAsync();
            }
        }

        // Tutor earnings: 50
        var earnCount = await _context.TutorEarningTransactions.AsNoTracking().CountAsync(e => !e.IsDeleted);
        if (earnCount < 50 && completedLessons.Count > 0)
        {
            var toAdd = new List<TutorEarningTransaction>();
            foreach (var l in completedLessons.Take(50))
            {
                if (!l.TutorUserId.HasValue) continue;
                toAdd.Add(new TutorEarningTransaction { TutorUserId = l.TutorUserId.Value, CreditsEarned = 1, LessonBookingId = l.Id, Notes = "Seed earning", CreatedAt = now.AddDays(-1) });
            }
            _context.TutorEarningTransactions.AddRange(toAdd);
            await _context.SaveChangesAsync();
        }

        // Feedback: 50 student->tutor and 50 tutor->student
        var tfCount = await _context.TutorFeedbacks.AsNoTracking().CountAsync(f => !f.IsDeleted);
        if (tfCount < 50 && completedLessons.Count > 0)
        {
            foreach (var l in completedLessons.Take(50))
            {
                var exists = await _context.TutorFeedbacks.AnyAsync(f => !f.IsDeleted && f.LessonBookingId == l.Id);
                if (exists) continue;
                _context.TutorFeedbacks.Add(new TutorFeedback
                {
                    LessonBookingId = l.Id,
                    CourseId = l.CourseId,
                    StudentUserId = l.StudentUserId,
                    TutorUserId = l.TutorUserId!.Value,
                    Rating = 3 + (int)(l.Id % 3),
                    SubjectKnowledge = 3 + (int)((l.Id + 1) % 3),
                    Communication = 3 + (int)((l.Id + 2) % 3),
                    Punctuality = 3 + (int)(l.Id % 3),
                    TeachingMethod = 3 + (int)((l.Id + 1) % 3),
                    Friendliness = 3 + (int)((l.Id + 2) % 3),
                    Comments = "Great session overall. Clear explanations and helpful examples.",
                    IsAnonymous = (l.Id % 7) == 0,
                    CreatedAt = now.AddDays(-2)
                });
            }
            await _context.SaveChangesAsync();
        }

        var sfCount = await _context.StudentFeedbacks.AsNoTracking().CountAsync(f => !f.IsDeleted);
        // Seed more student feedback records with varied ratings (aim for at least 200 to ensure students have ratings)
        if (sfCount < 200 && completedLessons.Count > 0)
        {
            var random = new Random(42); // Fixed seed for reproducibility
            var lessonsToProcess = completedLessons.Take(Math.Min(200, completedLessons.Count)).ToList();
            var toAdd = new List<StudentFeedback>();
            
            foreach (var l in lessonsToProcess)
            {
                var exists = await _context.StudentFeedbacks.AnyAsync(f => !f.IsDeleted && f.LessonBookingId == l.Id);
                if (exists) continue;
                
                // Generate varied ratings (1-5 range, but mostly 3-5 for realistic distribution)
                // Use lesson ID + index to create consistent but varied ratings per student
                var baseRating = 2 + (int)((l.StudentUserId + l.Id) % 4); // Range: 2-5
                var ratingVariation = random.Next(-1, 2); // -1, 0, or 1
                var rating = Math.Clamp(baseRating + ratingVariation, 1, 5);
                
                // Other metrics should be around the same rating with some variation
                var participation = Math.Clamp(rating + random.Next(-1, 2), 1, 5);
                var homeworkCompletion = Math.Clamp(rating + random.Next(-2, 1), 1, 5);
                var attentiveness = Math.Clamp(rating + random.Next(-1, 1), 1, 5);
                var improvement = Math.Clamp(rating + random.Next(-1, 2), 1, 5);
                
                var comments = rating >= 4 
                    ? "Excellent progress. Keep up the great work and continue practicing regularly."
                    : rating >= 3
                    ? "Good effort. Please review homework and practice the key exercises to improve further."
                    : "Needs improvement. Please focus on completing assignments and participating more actively in sessions.";
                
                toAdd.Add(new StudentFeedback
                {
                    LessonBookingId = l.Id,
                    CourseId = l.CourseId,
                    StudentUserId = l.StudentUserId,
                    TutorUserId = l.TutorUserId!.Value,
                    Rating = rating,
                    Participation = participation,
                    HomeworkCompletion = homeworkCompletion,
                    Attentiveness = attentiveness,
                    Improvement = improvement,
                    Comments = comments,
                    CreatedAt = now.AddDays(-random.Next(1, 30)) // Spread out creation dates
                });
            }
            
            if (toAdd.Count > 0)
            {
                _context.StudentFeedbacks.AddRange(toAdd);
                await _context.SaveChangesAsync();
            }
        }

        // Attachments: 50 (create tiny placeholder pdf files if possible)
        var attCount = await _context.FeedbackAttachments.AsNoTracking().CountAsync(a => !a.IsDeleted);
        if (attCount < 50)
        {
            var feedbacks = await _context.StudentFeedbacks.AsNoTracking().Where(f => !f.IsDeleted).OrderBy(f => f.Id).Take(50).ToListAsync();
            if (feedbacks.Count > 0)
            {
                var webRoot = TryFindWebRoot();
                foreach (var f in feedbacks)
                {
                    // 1 attachment per feedback (alternating kinds)
                    var kind = (f.Id % 2 == 0) ? "TestResults" : "Homework";
                    var fileName = $"{kind}_{f.Id}.pdf";
                    var rel = $"/uploads/feedback/student/{f.Id}/{fileName}";

                    if (webRoot != null)
                    {
                        var dir = Path.Combine(webRoot, "uploads", "feedback", "student", f.Id.ToString());
                        Directory.CreateDirectory(dir);
                        var full = Path.Combine(dir, fileName);
                        if (!File.Exists(full))
                        {
                            // minimal pdf header (tiny)
                            await File.WriteAllBytesAsync(full, Encoding.ASCII.GetBytes("%PDF-1.4\n%Seed\n"), CancellationToken.None);
                        }
                    }

                    var exists = await _context.FeedbackAttachments.AnyAsync(a => !a.IsDeleted && a.StudentFeedbackId == f.Id && a.Kind == kind);
                    if (exists) continue;
                    _context.FeedbackAttachments.Add(new FeedbackAttachment
                    {
                        StudentFeedbackId = f.Id,
                        Kind = kind,
                        FileName = fileName,
                        FilePath = rel,
                        ContentType = "application/pdf",
                        FileSize = 12,
                        CreatedAt = now
                    });
                }
                await _context.SaveChangesAsync();
            }
        }

        _logger.LogInformation("Ensured demo progress + feedback seed data.");
    }

    private async Task SeedGuardiansAsync()
    {
        // Seed Guardian entities (linked to StudentUser) with realistic data
        var students = await _context.Users.AsNoTracking()
            .Where(u => !u.IsDeleted && u.Role == "Student" && u.ApprovalStatus == UserApprovalStatus.Approved)
            .OrderBy(u => u.Id)
            .Take(100)
            .ToListAsync();

        if (students.Count == 0) return;

        var rng = new Random(20251225);
        var now = DateTime.UtcNow;

        // Realistic guardian first names (adults/parents)
        var guardianFirstNames = new[]
        {
            "Robert", "Jennifer", "Michael", "Lisa", "William", "Patricia", "David", "Linda", "Richard", "Barbara",
            "Joseph", "Elizabeth", "Thomas", "Susan", "Charles", "Jessica", "Christopher", "Sarah", "Daniel", "Karen",
            "Matthew", "Nancy", "Anthony", "Betty", "Mark", "Margaret", "Donald", "Sandra", "Steven", "Ashley",
            "Paul", "Kimberly", "Andrew", "Emily", "Joshua", "Donna", "Kenneth", "Michelle", "Kevin", "Carol",
            "Brian", "Amanda", "George", "Dorothy", "Edward", "Melissa", "Ronald", "Deborah", "Timothy", "Stephanie",
            "Ahmed", "Fatima", "Hassan", "Ayesha", "Mohammed", "Zainab", "Ali", "Maryam", "Omar", "Noor",
            "Rajesh", "Priya", "Amit", "Kavya", "Rahul", "Ananya", "Vikram", "Meera", "Arjun", "Diya"
        };

        var guardianLastNames = new[]
        {
            "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez",
            "Hernandez", "Lopez", "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin", "Lee",
            "Perez", "Thompson", "White", "Harris", "Sanchez", "Clark", "Ramirez", "Lewis", "Robinson", "Walker",
            "Young", "Allen", "King", "Wright", "Scott", "Torres", "Nguyen", "Hill", "Flores", "Green",
            "Adams", "Nelson", "Baker", "Hall", "Rivera", "Campbell", "Mitchell", "Carter", "Roberts", "Gomez",
            "Khan", "Ahmed", "Ali", "Hussain", "Malik", "Shaikh", "Iqbal", "Raza", "Siddiqui", "Qureshi",
            "Patel", "Sharma", "Gupta", "Mehta", "Kapoor", "Singh", "Kaur", "Iyer", "Nair", "Reddy"
        };

        var relationships = new[] { "Father", "Mother", "Guardian", "Stepfather", "Stepmother" };

        var existingGuardians = await _context.Guardians.AsNoTracking()
            .Where(g => !g.IsDeleted)
            .Select(g => g.StudentUserId)
            .ToListAsync();

        var studentsWithGuardians = existingGuardians.ToHashSet();
        var toAdd = new List<Guardian>();

        foreach (var student in students)
        {
            // Skip if student already has guardians
            if (studentsWithGuardians.Contains(student.Id)) continue;

            // Most students have 1-2 guardians (usually Father and/or Mother)
            var guardianCount = rng.Next(1, 3); // 1 or 2 guardians per student

            for (int i = 0; i < guardianCount; i++)
            {
                var firstName = guardianFirstNames[rng.Next(guardianFirstNames.Length)];
                var lastName = guardianLastNames[rng.Next(guardianLastNames.Length)];
                var fullName = $"{firstName} {lastName}";
                
                // Use student's last name for some guardians (realistic - family members)
                if (rng.Next(2) == 0 && !string.IsNullOrWhiteSpace(student.LastName))
                {
                    lastName = student.LastName;
                    fullName = $"{firstName} {lastName}";
                }

                var relationship = i == 0 
                    ? (rng.Next(2) == 0 ? "Father" : "Mother") // First guardian is usually Father or Mother
                    : relationships[rng.Next(relationships.Length)]; // Second can be any relationship

                // Generate email based on guardian name and student ID for uniqueness
                var emailBase = $"{firstName.ToLowerInvariant().Replace(" ", "")}.{lastName.ToLowerInvariant().Replace(" ", "")}";
                var email = $"{emailBase}.{student.Id}.{i}@guardian.igb.com";

                // Generate phone number
                var phoneSuffix = (8000 + student.Id * 10 + i).ToString().PadLeft(7, '0');
                var phone = $"+1{phoneSuffix}";

                toAdd.Add(new Guardian
                {
                    StudentUserId = student.Id,
                    FullName = fullName,
                    Relationship = relationship,
                    Email = email,
                    LocalNumber = phone,
                    WhatsappNumber = phone,
                    IsPrimary = i == 0, // First guardian is primary
                    CreatedAt = now
                });
            }
        }

        if (toAdd.Count > 0)
        {
            await _context.Guardians.AddRangeAsync(toAdd);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Seeded {Count} guardian entities for students.", toAdd.Count);
        }
    }

    private async Task SeedTestReportsAsync()
    {
        // Seed test reports so Reports > Test Analytics has meaningful charts/tables.
        var existing = await _context.TestReports.AsNoTracking()
            .CountAsync(r => !r.IsDeleted && !r.IsDraft);

        // Avoid runaway growth across re-runs; keep enough for charts to look good.
        var target = 300;
        if (existing >= target) return;

        var students = await _context.Users.AsNoTracking()
            .Where(u => !u.IsDeleted && u.Role == "Student")
            .OrderBy(u => u.Id)
            .Take(100)
            .ToListAsync();

        var tutors = await _context.Users.AsNoTracking()
            .Where(u => !u.IsDeleted && u.Role == "Tutor")
            .OrderBy(u => u.Id)
            .Take(50)
            .ToListAsync();

        var courses = await _context.Courses.AsNoTracking()
            .Where(c => !c.IsDeleted && c.IsActive)
            .OrderBy(c => c.Id)
            .Take(20)
            .ToListAsync();

        if (students.Count == 0 || tutors.Count == 0 || courses.Count == 0) return;

        var courseIds = courses.Select(c => c.Id).ToList();
        var topicsByCourse = await _context.CourseTopics.AsNoTracking()
            .Where(t => !t.IsDeleted && courseIds.Contains(t.CourseId))
            .GroupBy(t => t.CourseId)
            .ToDictionaryAsync(g => g.Key, g => g.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToList());

        var rng = new Random(7331); // stable seed for repeatable demo data
        var nowUtc = DateTime.UtcNow;

        static decimal CalcPct(int obtained, int total)
            => total <= 0 ? 0 : Math.Round((obtained * 100m) / total, 2);

        static string SuggestGrade(decimal pct)
        {
            // Keep aligned with app grading rules used in UI.
            return pct switch
            {
                >= 90 => "A+",
                >= 85 => "A",
                >= 80 => "A-",
                >= 75 => "B+",
                >= 70 => "B",
                >= 65 => "B-",
                >= 60 => "C+",
                >= 55 => "C",
                >= 50 => "C-",
                >= 40 => "D",
                _ => "F"
            };
        }

        string[] testNames =
        [
            "Mid-Term Exam",
            "Chapter Quiz",
            "Unit Test",
            "Mock Test",
            "Weekly Assessment",
            "Final Review Quiz"
        ];

        var reports = new List<TestReport>();

        // Create reports across last 6 months so the "trend" chart looks good.
        foreach (var s in students)
        {
            // 2–4 courses per student
            var pickedCourses = courses.OrderBy(_ => rng.Next()).Take(rng.Next(2, 5)).ToList();
            foreach (var c in pickedCourses)
            {
                // prefer course's assigned tutor when possible
                var tutorId = c.TutorUserId ?? tutors[rng.Next(tutors.Count)].Id;

                // 1–2 tests per course spread across 6 months (keeps volume reasonable)
                for (int i = 0; i < rng.Next(1, 3); i++)
                {
                    var monthsBack = rng.Next(0, 6); // 0..5 months ago
                    var day = rng.Next(1, 25);
                    var d = new DateOnly(nowUtc.Year, nowUtc.Month, 1)
                        .AddMonths(-monthsBack)
                        .AddDays(day - 1);

                    var total = 100;
                    var obtained = rng.Next(32, 101);

                    // Ensure we have a healthy mix of pass/fail for analytics.
                    if (rng.NextDouble() < 0.12) obtained = rng.Next(15, 40);

                    var pct = CalcPct(obtained, total);
                    var grade = SuggestGrade(pct);

                    var hasImprove = rng.NextDouble() < 0.55;
                    var strengths = rng.NextDouble() < 0.8 ? "Good understanding of core concepts; neat working and clear steps." : null;
                    var improve = hasImprove ? "Needs more practice on problem-solving speed and accuracy in multi-step questions." : null;
                    var comments = "Overall performance is improving. Keep revising key formulas and practice daily.";

                    var r = new TestReport
                    {
                        StudentUserId = s.Id,
                        TutorUserId = tutorId,
                        CourseId = c.Id,
                        TestName = testNames[rng.Next(testNames.Length)],
                        TestDate = d,
                        TotalMarks = total,
                        ObtainedMarks = obtained,
                        Percentage = pct,
                        Grade = grade,
                        Strengths = strengths,
                        AreasForImprovement = improve,
                        TutorComments = comments,
                        TestFileUrl = null,
                        TestFileName = null,
                        TestFileContentType = null,
                        IsDraft = false,
                        SubmittedAtUtc = new DateTimeOffset(d.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(12))), TimeSpan.Zero),
                        CreatedAt = nowUtc.AddDays(-rng.Next(1, 160)),
                        UpdatedAt = nowUtc.AddDays(-rng.Next(0, 60))
                    };

                    // attach 2-4 topics (and make sure "improvement needs" has topics to count)
                    if (topicsByCourse.TryGetValue(c.Id, out var topics) && topics.Count > 0)
                    {
                        var n = rng.Next(2, Math.Min(5, topics.Count + 1));
                        foreach (var t in topics.OrderBy(_ => rng.Next()).Take(n))
                        {
                            r.Topics.Add(new TestReportTopic
                            {
                                CourseTopicId = t.Id,
                                CreatedAt = nowUtc
                            });
                        }
                    }

                    reports.Add(r);
                }
            }
        }

        var needed = Math.Clamp(target - existing, 0, reports.Count);
        var toAdd = needed;
        if (toAdd == 0) return;

        await _context.TestReports.AddRangeAsync(reports.Take(toAdd));
        await _context.SaveChangesAsync();

        _logger.LogInformation("Seeded {Count} test reports for analytics (existing={Existing}).", toAdd, existing);
    }

    private string? TryFindWebRoot()
    {
        // Try a few common layouts to locate IGB.Web/wwwroot for attachment placeholder files.
        var start = Directory.GetCurrentDirectory();
        var candidates = new List<string>
        {
            Path.Combine(start, "wwwroot"),
            Path.Combine(start, "IGB.Web", "wwwroot"),
        };

        var dir = new DirectoryInfo(start);
        for (int i = 0; i < 6 && dir != null; i++)
        {
            candidates.Add(Path.Combine(dir.FullName, "IGB.Web", "wwwroot"));
            candidates.Add(Path.Combine(dir.FullName, "wwwroot"));
            dir = dir.Parent;
        }

        foreach (var c in candidates.Distinct())
        {
            if (Directory.Exists(c)) return c;
        }
        return null;
    }
}

