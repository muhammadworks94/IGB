using IGB.Application.DTOs;
using IGB.Application.Services;
using IGB.Infrastructure.Data;
using IGB.Shared.Security;
using IGB.Web.Security;
using IGB.Web.Services;
using IGB.Web.ViewModels.Admin;
using IGB.Web.ViewModels.Components;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IGB.Web.Controllers;

[Authorize(Policy = "AdminOnly")]
public sealed class AdminUsersController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IUserService _users;
    private readonly AdminDashboardRealtimeBroadcaster _rt;

    public AdminUsersController(ApplicationDbContext db, IUserService users, AdminDashboardRealtimeBroadcaster rt)
    {
        _db = db;
        _users = users;
        _rt = rt;
    }

    [RequirePermission(PermissionCatalog.Permissions.UsersRead)]
    public Task<IActionResult> Students(string? q, int page = 1, int pageSize = 10, CancellationToken ct = default)
        => List("Student", q, page, pageSize, ct);

    [RequirePermission(PermissionCatalog.Permissions.UsersRead)]
    public Task<IActionResult> Tutors(string? q, int page = 1, int pageSize = 10, CancellationToken ct = default)
        => List("Tutor", q, page, pageSize, ct);

    [HttpGet]
    [RequirePermission(PermissionCatalog.Permissions.UsersWrite)]
    public async Task<IActionResult> Edit(long id, string? returnTo = null, CancellationToken ct = default)
    {
        var r = await _users.GetByIdAsync(id, ct);
        if (r.IsFailure || r.Value == null)
        {
            TempData["Error"] = r.Error ?? "User not found.";
            return RedirectToAction(nameof(Students));
        }

        // Only allow editing Student/Tutor from this admin page
        var role = (r.Value.Role ?? "").Trim();
        if (!role.Equals("Student", StringComparison.OrdinalIgnoreCase) && !role.Equals("Tutor", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Only Students and Tutors can be edited here.";
            return RedirectToAction(nameof(Students));
        }

        ViewBag.ReturnTo = string.IsNullOrWhiteSpace(returnTo)
            ? (role.Equals("Tutor", StringComparison.OrdinalIgnoreCase) ? "Tutors" : "Students")
            : returnTo;

        // Get user entity for additional fields
        var userEntity = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => !u.IsDeleted && u.Id == id, ct);
        
        var vm = new IGB.Web.ViewModels.EditUserViewModel
        {
            Id = r.Value.Id,
            Role = role,
            FirstName = r.Value.FirstName,
            LastName = r.Value.LastName,
            PhoneNumber = r.Value.PhoneNumber,
            IsActive = r.Value.IsActive,
            TimeZoneId = userEntity?.TimeZoneId,
            CurrentProfileImagePath = userEntity?.ProfileImagePath
        };

        // Load tutor-specific data
        if (role == "Tutor")
        {
            var tutorProfile = await _db.TutorProfiles.FirstOrDefaultAsync(x => !x.IsDeleted && x.UserId == id, ct);
            if (tutorProfile != null && !string.IsNullOrWhiteSpace(tutorProfile.TimeZoneId))
            {
                vm.TimeZoneId = tutorProfile.TimeZoneId;
            }

            var assignedCourses = await _db.Courses.AsNoTracking()
                .Where(c => !c.IsDeleted && c.TutorUserId == id)
                .Select(c => c.Id)
                .ToListAsync(ct);
            vm.CourseIds = assignedCourses;

            // Load course options for dropdown
            var courseOptions = await _db.Courses.AsNoTracking()
                .Where(c => !c.IsDeleted && c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => new { c.Id, c.Name })
                .Take(250)
                .ToListAsync(ct);
            ViewBag.CourseOptions = courseOptions;
        }

        // Check if request is for modal (AJAX)
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return PartialView("_EditUserModal", vm);
        }

        return View("Edit", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionCatalog.Permissions.UsersWrite)]
    public async Task<IActionResult> Edit(long id, IGB.Web.ViewModels.EditUserViewModel model, string? returnTo = null, CancellationToken ct = default)
    {
        // Handle CourseIds from form collection (multi-select)
        if (Request.Form.ContainsKey("CourseIds"))
        {
            var courseIdValues = Request.Form["CourseIds"];
            if (courseIdValues.Count > 0)
            {
                var courseIds = new List<long>();
                foreach (var val in courseIdValues)
                {
                    if (long.TryParse(val, out var courseId))
                    {
                        courseIds.Add(courseId);
                    }
                }
                model.CourseIds = courseIds;
            }
        }

        if (id != model.Id) return NotFound();
        if (!ModelState.IsValid)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                // Reload course options for tutor
                if (model.Role == "Tutor")
                {
                    var courseOptions = await _db.Courses.AsNoTracking()
                        .Where(c => !c.IsDeleted && c.IsActive)
                        .OrderBy(c => c.Name)
                        .Select(c => new { c.Id, c.Name })
                        .Take(250)
                        .ToListAsync(ct);
                    ViewBag.CourseOptions = courseOptions;
                }
                return PartialView("_EditUserModal", model);
            }
            return View("Edit", model);
        }

        var r = await _users.GetByIdAsync(id, ct);
        if (r.IsFailure || r.Value == null)
        {
            TempData["Error"] = r.Error ?? "User not found.";
            return RedirectToAction(nameof(Students));
        }

        var role = (r.Value.Role ?? "").Trim();
        var dto = new UpdateUserDto
        {
            FirstName = model.FirstName,
            LastName = model.LastName,
            PhoneNumber = model.PhoneNumber,
            IsActive = model.IsActive
        };

        var upd = await _users.UpdateAsync(id, dto, ct);
        if (upd.IsFailure)
        {
            ModelState.AddModelError("", upd.Error ?? "Unable to update user.");
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_EditUserModal", model);
            return View("Edit", model);
        }

        // Handle tutor-specific updates
        if (role == "Tutor")
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => !u.IsDeleted && u.Id == id, ct);
            if (user != null)
            {
                // Update timezone
                if (!string.IsNullOrWhiteSpace(model.TimeZoneId))
                {
                    user.TimeZoneId = model.TimeZoneId.Trim();
                }

                // Handle profile image upload
                if (model.ProfileImage != null && model.ProfileImage.Length > 0)
                {
                    var ext = Path.GetExtension(model.ProfileImage.FileName).ToLowerInvariant();
                    var allowed = new HashSet<string> { ".jpg", ".jpeg", ".png" };
                    if (allowed.Contains(ext) && model.ProfileImage.Length <= 5 * 1024 * 1024)
                    {
                        var webRoot = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootPath;
                        var uploadsDir = Path.Combine(webRoot, "uploads", "profiles", user.Id.ToString());
                        Directory.CreateDirectory(uploadsDir);
                        var fileName = $"profile_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
                        var fullPath = Path.Combine(uploadsDir, fileName);
                        await using (var stream = System.IO.File.Create(fullPath))
                        {
                            await model.ProfileImage.CopyToAsync(stream, ct);
                        }
                        user.ProfileImagePath = $"/uploads/profiles/{user.Id}/{fileName}";
                    }
                }

                // Update tutor profile timezone
                var tutorProfile = await _db.TutorProfiles.FirstOrDefaultAsync(x => !x.IsDeleted && x.UserId == id, ct);
                if (tutorProfile == null)
                {
                    tutorProfile = new IGB.Domain.Entities.TutorProfile { UserId = id, CreatedAt = DateTime.UtcNow };
                    _db.TutorProfiles.Add(tutorProfile);
                }
                if (!string.IsNullOrWhiteSpace(model.TimeZoneId))
                    tutorProfile.TimeZoneId = model.TimeZoneId.Trim();
                tutorProfile.UpdatedAt = DateTime.UtcNow;

                // Update course assignments
                var currentCourses = await _db.Courses.Where(c => !c.IsDeleted && c.TutorUserId == id).ToListAsync(ct);
                foreach (var c in currentCourses)
                {
                    c.TutorUserId = null;
                    c.UpdatedAt = DateTime.UtcNow;
                }

                if (model.CourseIds is { Count: > 0 })
                {
                    var courses = await _db.Courses.Where(c => !c.IsDeleted && model.CourseIds.Contains(c.Id)).ToListAsync(ct);
                    foreach (var c in courses)
                    {
                        c.TutorUserId = id;
                        c.UpdatedAt = DateTime.UtcNow;
                    }
                }

                await _db.SaveChangesAsync(ct);
            }
        }

        TempData["Success"] = "User updated.";
        var dest = string.IsNullOrWhiteSpace(returnTo)
            ? (role.Equals("Tutor", StringComparison.OrdinalIgnoreCase) ? "Tutors" : "Students")
            : returnTo;

        // If AJAX request, return JSON for modal
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new { success = true, message = "User updated successfully.", redirect = Url.Action(dest) });
        }

        return RedirectToAction(dest);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionCatalog.Permissions.UsersWrite)]
    public async Task<IActionResult> Delete(long id, string? returnTo = null, CancellationToken ct = default)
    {
        var r = await _users.GetByIdAsync(id, ct);
        if (r.IsFailure || r.Value == null)
        {
            TempData["Error"] = r.Error ?? "User not found.";
            return RedirectToAction(nameof(Students));
        }

        var role = (r.Value.Role ?? "").Trim();
        if (!role.Equals("Student", StringComparison.OrdinalIgnoreCase) && !role.Equals("Tutor", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Only Students and Tutors can be deleted here.";
            return RedirectToAction(nameof(Students));
        }

        var del = await _users.DeleteAsync(id, ct);
        TempData[del.IsFailure ? "Error" : "Success"] = del.IsFailure ? (del.Error ?? "Unable to delete user.") : "User deleted.";

        var dest = string.IsNullOrWhiteSpace(returnTo)
            ? (role.Equals("Tutor", StringComparison.OrdinalIgnoreCase) ? "Tutors" : "Students")
            : returnTo;
        return RedirectToAction(dest);
    }

    private async Task<IActionResult> List(string role, string? q, int page, int pageSize, CancellationToken ct)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 5 or > 100 ? 10 : pageSize;

        var query = _db.Users.AsNoTracking()
            .Where(u => !u.IsDeleted && u.Role == role);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(u => u.FirstName.Contains(term) || u.LastName.Contains(term) || u.Email.Contains(term));
        }

        var total = await query.CountAsync(ct);
        var baseRows = await query
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email,
                u.ProfileImagePath,
                u.IsActive,
                u.CreatedAt,
                Approval = u.Role == "Tutor" ? u.ApprovalStatus.ToString() : null
            })
            .ToListAsync(ct);

        var ids = baseRows.Select(x => x.Id).ToList();
        Dictionary<long, int> coursesCount = new();
        Dictionary<long, string?> coursesPreview = new();
        Dictionary<long, (double avg, int count)> ratings = new();
        Dictionary<long, int> enrolledCoursesCount = new();
        Dictionary<long, string?> enrolledCoursesPreview = new();
        Dictionary<long, (double avg, int count)> studentRatings = new();
        Dictionary<long, int> guardianCount = new();
        Dictionary<long, string?> guardianNamesPreview = new();

        if (role == "Tutor" && ids.Count > 0)
        {
            // Assigned courses per tutor
            var courses = await _db.Courses.AsNoTracking()
                .Where(c => !c.IsDeleted && c.TutorUserId.HasValue && ids.Contains(c.TutorUserId.Value))
                .Select(c => new { TutorId = c.TutorUserId!.Value, c.Name })
                .ToListAsync(ct);

            coursesCount = courses.GroupBy(c => c.TutorId).ToDictionary(g => g.Key, g => g.Count());
            coursesPreview = courses.GroupBy(c => c.TutorId).ToDictionary(
                g => g.Key,
                g =>
                {
                    var names = g.Select(x => x.Name).Distinct().OrderBy(n => n).Take(3).ToList();
                    return names.Count == 0 ? null : string.Join(", ", names);
                }
            );

            // Rating per tutor (from student->tutor feedback)
            var ratingAgg = await _db.TutorFeedbacks.AsNoTracking()
                .Where(f => !f.IsDeleted && !f.IsFlagged && ids.Contains(f.TutorUserId))
                .GroupBy(f => f.TutorUserId)
                .Select(g => new { TutorId = g.Key, Avg = g.Average(x => (double)x.Rating), Cnt = g.Count() })
                .ToListAsync(ct);

            ratings = ratingAgg.ToDictionary(x => x.TutorId, x => (x.Avg, x.Cnt));
        }
        else if (role == "Student" && ids.Count > 0)
        {
            // Enrolled courses per student (from CourseBookings)
            var enrollments = await _db.CourseBookings.AsNoTracking()
                .Include(b => b.Course)
                .Where(b => !b.IsDeleted && ids.Contains(b.StudentUserId) && b.Course != null)
                .Select(b => new { StudentId = b.StudentUserId, CourseName = b.Course!.Name })
                .ToListAsync(ct);

            enrolledCoursesCount = enrollments.GroupBy(e => e.StudentId).ToDictionary(g => g.Key, g => g.Count());
            enrolledCoursesPreview = enrollments.GroupBy(e => e.StudentId).ToDictionary(
                g => g.Key,
                g =>
                {
                    var names = g.Select(x => x.CourseName).Distinct().OrderBy(n => n).Take(3).ToList();
                    return names.Count == 0 ? null : string.Join(", ", names);
                }
            );

            // Student rating per student (from tutor->student feedback)
            var studentRatingAgg = await _db.StudentFeedbacks.AsNoTracking()
                .Where(f => !f.IsDeleted && !f.IsFlagged && ids.Contains(f.StudentUserId))
                .GroupBy(f => f.StudentUserId)
                .Select(g => new { StudentId = g.Key, Avg = g.Average(x => (double)x.Rating), Cnt = g.Count() })
                .ToListAsync(ct);

            studentRatings = studentRatingAgg.ToDictionary(x => x.StudentId, x => (x.Avg, x.Cnt));

            // Guardians per student
            var guardians = await _db.GuardianWards.AsNoTracking()
                .Include(g => g.GuardianUser)
                .Where(g => !g.IsDeleted && ids.Contains(g.StudentUserId) && g.GuardianUser != null)
                .Select(g => new { StudentId = g.StudentUserId, GuardianName = g.GuardianUser!.FullName })
                .ToListAsync(ct);

            guardianCount = guardians.GroupBy(g => g.StudentId).ToDictionary(g => g.Key, g => g.Count());
            guardianNamesPreview = guardians.GroupBy(g => g.StudentId).ToDictionary(
                g => g.Key,
                g =>
                {
                    var names = g.Select(x => x.GuardianName).Distinct().OrderBy(n => n).Take(2).ToList();
                    return names.Count == 0 ? null : string.Join(", ", names);
                }
            );
        }

        var items = baseRows.Select(u =>
        {
            coursesCount.TryGetValue(u.Id, out var cc);
            coursesPreview.TryGetValue(u.Id, out var cp);
            ratings.TryGetValue(u.Id, out var r);
            enrolledCoursesCount.TryGetValue(u.Id, out var ecc);
            enrolledCoursesPreview.TryGetValue(u.Id, out var ecp);
            studentRatings.TryGetValue(u.Id, out var sr);
            guardianCount.TryGetValue(u.Id, out var gc);
            guardianNamesPreview.TryGetValue(u.Id, out var gnp);
            
            return new AdminUserListViewModel.Row(
                u.Id,
                $"{u.FirstName} {u.LastName}".Trim(),
                u.Email,
                u.ProfileImagePath,
                u.IsActive,
                u.CreatedAt,
                u.Approval,
                role == "Tutor" ? cc : 0,
                role == "Tutor" ? cp : null,
                role == "Tutor" ? r.avg : (role == "Student" ? sr.avg : 0),
                role == "Tutor" ? r.count : (role == "Student" ? sr.count : 0),
                role == "Student" ? ecc : 0,
                role == "Student" ? ecp : null,
                role == "Student" ? gc : 0,
                role == "Student" ? gnp : null
            );
        }).ToList();

        var pending = role == "Tutor"
            ? await _db.Users.AsNoTracking().CountAsync(u => !u.IsDeleted && u.Role == "Tutor" && u.ApprovalStatus == IGB.Domain.Enums.UserApprovalStatus.Pending, ct)
            : 0;

        // For tutor list: load a small catalog of courses so the "Add Tutor" offcanvas can assign on create.
        if (role == "Tutor")
        {
            var courseOptions = await _db.Courses.AsNoTracking()
                .Where(c => !c.IsDeleted && c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => new { c.Id, c.Name })
                .Take(250)
                .ToListAsync(ct);
            ViewBag.CourseOptions = courseOptions;
        }

        return View("List", new AdminUserListViewModel
        {
            Title = role == "Tutor" ? "Tutors" : "Students",
            Role = role,
            Query = q,
            PendingApprovals = pending,
            Pagination = new PaginationViewModel(page, pageSize, total, Action: role == "Tutor" ? "Tutors" : "Students", Controller: "AdminUsers", RouteValues: new { q }),
            Rows = items
        });
    }

    [HttpGet]
    [RequirePermission(PermissionCatalog.Permissions.UsersWrite)]
    public IActionResult CreateStudent() => View("Create", new AdminCreateUserViewModel { Role = "Student" });

    [HttpGet]
    [RequirePermission(PermissionCatalog.Permissions.UsersWrite)]
    public IActionResult CreateTutor() => View("Create", new AdminCreateUserViewModel { Role = "Tutor" });

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(PermissionCatalog.Permissions.UsersWrite)]
    public async Task<IActionResult> Create(AdminCreateUserViewModel model, CancellationToken ct)
    {
        if (model.Role is not ("Student" or "Tutor"))
            ModelState.AddModelError(nameof(model.Role), "Role must be Student or Tutor.");
        if (string.IsNullOrWhiteSpace(model.Password) || model.Password.Length < 8)
            ModelState.AddModelError(nameof(model.Password), "Password must be at least 8 characters.");

        if (!ModelState.IsValid) return View("Create", model);

        var dto = new CreateUserDto
        {
            Email = model.Email.Trim(),
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            Password = model.Password,
            Role = model.Role,
            PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim()
        };

        var result = await _users.CreateAsync(dto, ct);
        if (result.IsFailure)
        {
            TempData["Error"] = result.Error ?? "Unable to create user.";
            return View("Create", model);
        }

        // Post-create extras (profile image + tutor profile details + course assignment)
        try
        {
            var userId = result.Value;
            var user = await _db.Users.FirstOrDefaultAsync(u => !u.IsDeleted && u.Id == userId, ct);
            if (user != null)
            {
                // Save timezone if provided
                if (!string.IsNullOrWhiteSpace(model.TimeZoneId))
                {
                    user.TimeZoneId = model.TimeZoneId.Trim();
                }

                // Image upload (reuse same logic as ProfileController)
                if (model.ProfileImage != null && model.ProfileImage.Length > 0)
                {
                    var ext = Path.GetExtension(model.ProfileImage.FileName).ToLowerInvariant();
                    var allowed = new HashSet<string> { ".jpg", ".jpeg", ".png" };
                    if (!allowed.Contains(ext) || model.ProfileImage.Length > 5 * 1024 * 1024)
                    {
                        // Don't fail creation; just skip image and show message.
                        TempData["Error"] = "Tutor created, but profile image was not saved (only JPG/PNG up to 5MB).";
                    }
                    else
                    {
                        var webRoot = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().WebRootPath;
                        var uploadsDir = Path.Combine(webRoot, "uploads", "profiles", user.Id.ToString());
                        Directory.CreateDirectory(uploadsDir);
                        var fileName = $"profile_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
                        var fullPath = Path.Combine(uploadsDir, fileName);
                        await using (var stream = System.IO.File.Create(fullPath))
                        {
                            await model.ProfileImage.CopyToAsync(stream, ct);
                        }
                        user.ProfileImagePath = $"/uploads/profiles/{user.Id}/{fileName}";
                    }
                }

                if (model.Role == "Tutor")
                {
                    // Ensure tutor profile exists
                    var tp = await _db.TutorProfiles.FirstOrDefaultAsync(x => !x.IsDeleted && x.UserId == user.Id, ct);
                    if (tp == null)
                    {
                        tp = new IGB.Domain.Entities.TutorProfile { UserId = user.Id, CreatedAt = DateTime.UtcNow };
                        _db.TutorProfiles.Add(tp);
                    }
                    if (!string.IsNullOrWhiteSpace(model.TimeZoneId))
                        tp.TimeZoneId = model.TimeZoneId.Trim();
                    tp.UpdatedAt = DateTime.UtcNow;

                    // Assign courses (if selected)
                    if (model.CourseIds is { Count: > 0 })
                    {
                        var courses = await _db.Courses.Where(c => !c.IsDeleted && model.CourseIds.Contains(c.Id)).ToListAsync(ct);
                        foreach (var c in courses)
                        {
                            c.TutorUserId = user.Id;
                            c.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                }

                await _db.SaveChangesAsync(ct);
            }
        }
        catch
        {
            // non-fatal: user is created already
        }

        TempData["Success"] = $"{model.Role} created.";
        await _rt.SendToAdminsAsync("user:registered", new
        {
            timeUtc = DateTimeOffset.UtcNow.ToString("O"),
            relative = "Just now",
            type = "User",
            badge = "purple",
            text = $"New {model.Role.ToLowerInvariant()} created: {dto.FirstName} {dto.LastName}",
            url = model.Role == "Tutor" ? "/AdminUsers/Tutors" : "/AdminUsers/Students"
        }, ct);
        await _rt.SendToAdminsAsync("activity:new", new
        {
            timeUtc = DateTimeOffset.UtcNow.ToString("O"),
            relative = "Just now",
            type = "User",
            badge = "purple",
            text = $"New {model.Role.ToLowerInvariant()} created: {dto.FirstName} {dto.LastName}",
            url = model.Role == "Tutor" ? "/AdminUsers/Tutors" : "/AdminUsers/Students"
        }, ct);

        if (model.Role == "Tutor")
        {
            await _rt.SendToAdminsAsync("approval:pending", new
            {
                timeUtc = DateTimeOffset.UtcNow.ToString("O"),
                relative = "Just now",
                type = "Approval",
                badge = "warning",
                text = $"New tutor pending approval: {dto.FirstName} {dto.LastName}",
                url = "/Approvals"
            }, ct);
            await _rt.SendToAdminsAsync("alert:new", new { key = "pendingTutorApprovals" }, ct);
        }
        if (model.Role == "Tutor" && model.GoToAvailabilityAfterCreate)
        {
            return RedirectToAction("Index", "AdminTutorAvailability", new { tutorId = result.Value });
        }
        return RedirectToAction(model.Role == "Tutor" ? "Tutors" : "Students");
    }

    [HttpGet]
    [RequirePermission(PermissionCatalog.Permissions.UsersRead)]
    public async Task<IActionResult> TutorRatings(long tutorId, CancellationToken ct = default)
    {
        var tutor = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => !u.IsDeleted && u.Id == tutorId && u.Role == "Tutor", ct);
        if (tutor == null) return NotFound();

        var ratings = await _db.TutorFeedbacks.AsNoTracking()
            .Include(f => f.Course)
            .Include(f => f.StudentUser)
            .Include(f => f.LessonBooking)
            .Where(f => !f.IsDeleted && !f.IsFlagged && f.TutorUserId == tutorId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new
            {
                f.Id,
                f.Rating,
                f.SubjectKnowledge,
                f.Communication,
                f.Punctuality,
                f.TeachingMethod,
                f.Friendliness,
                f.Comments,
                f.IsAnonymous,
                f.CreatedAt,
                CourseName = f.Course != null ? f.Course.Name : "Course",
                StudentName = f.IsAnonymous ? "Anonymous" : (f.StudentUser != null ? f.StudentUser.FullName : "Student"),
                StudentEmail = f.IsAnonymous ? null : (f.StudentUser != null ? f.StudentUser.Email : null),
                LessonDate = f.LessonBooking != null && f.LessonBooking.ScheduledStart.HasValue 
                    ? f.LessonBooking.ScheduledStart.Value 
                    : (DateTimeOffset?)null
            })
            .ToListAsync(ct);

        var avgRating = ratings.Count > 0 ? ratings.Average(r => (double)r.Rating) : 0;
        var totalRatings = ratings.Count;

        return Json(new
        {
            tutorName = tutor.FullName,
            averageRating = avgRating,
            totalRatings = totalRatings,
            ratings = ratings.Select(r => new
            {
                r.Id,
                r.Rating,
                r.SubjectKnowledge,
                r.Communication,
                r.Punctuality,
                r.TeachingMethod,
                r.Friendliness,
                r.Comments,
                r.IsAnonymous,
                r.CourseName,
                r.StudentName,
                r.StudentEmail,
                createdAt = r.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                lessonDate = r.LessonDate?.ToString("yyyy-MM-dd HH:mm")
            })
        });
    }
}


