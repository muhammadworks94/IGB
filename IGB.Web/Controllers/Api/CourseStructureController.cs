using IGB.Infrastructure.Data;
using IGB.Shared.Security;
using IGB.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IGB.Web.Controllers.Api;

[ApiController]
[Route("api/course-structure")]
[Authorize(Policy = "AdminOnly")]
[RequirePermission(PermissionCatalog.Permissions.CoursesManage)]
public class CourseStructureController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public CourseStructureController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("curricula")]
    public async Task<IActionResult> Curricula(CancellationToken ct)
    {
        var items = await _db.Curricula.AsNoTracking()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.Description, c.IsActive })
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("grades")]
    public async Task<IActionResult> Grades(long curriculumId, CancellationToken ct)
    {
        var items = await _db.Grades.AsNoTracking()
            .Where(g => !g.IsDeleted && g.CurriculumId == curriculumId)
            .OrderBy(g => g.Level ?? 9999).ThenBy(g => g.Name)
            .Select(g => new { g.Id, g.Name, g.Level, g.IsActive, g.CurriculumId })
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("courses")]
    public async Task<IActionResult> Courses(string? q, long? curriculumId, long? gradeId, bool? active, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is < 5 or > 200 ? 20 : pageSize;

        var query = _db.Courses.AsNoTracking()
            .Include(c => c.Curriculum)
            .Include(c => c.Grade)
            .Where(c => !c.IsDeleted);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c => c.Name.Contains(term) || (c.Description != null && c.Description.Contains(term)));
        }
        if (curriculumId.HasValue) query = query.Where(c => c.CurriculumId == curriculumId.Value);
        if (gradeId.HasValue) query = query.Where(c => c.GradeId == gradeId.Value);
        if (active.HasValue) query = query.Where(c => c.IsActive == active.Value);

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Description,
                c.CreditCost,
                c.IsActive,
                c.ImagePath,
                Curriculum = c.Curriculum != null ? new { c.Curriculum.Id, c.Curriculum.Name } : null,
                Grade = c.Grade != null ? new { c.Grade.Id, c.Grade.Name, c.Grade.Level } : null
            })
            .ToListAsync(ct);

        return Ok(new { page, pageSize, total, items });
    }

    public record BulkActiveRequest(long[] Ids, bool Active);

    [HttpPost("courses/bulk-active")]
    public async Task<IActionResult> BulkActive([FromBody] BulkActiveRequest req, CancellationToken ct)
    {
        var courses = await _db.Courses.Where(c => !c.IsDeleted && req.Ids.Contains(c.Id)).ToListAsync(ct);
        foreach (var c in courses)
        {
            c.IsActive = req.Active;
            c.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        return Ok(new { updated = courses.Count });
    }

    [HttpPost("courses/{id:long}/duplicate")]
    public async Task<IActionResult> Duplicate(long id, CancellationToken ct)
    {
        // Delegate to MVC logic path via simple inline duplication (topics included)
        var course = await _db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
        if (course == null) return NotFound();

        var copy = new IGB.Domain.Entities.Course
        {
            Name = $"{course.Name} (Copy)",
            Description = course.Description,
            CurriculumId = course.CurriculumId,
            GradeId = course.GradeId,
            CreditCost = course.CreditCost,
            IsActive = course.IsActive,
            ImagePath = course.ImagePath,
            CreatedAt = DateTime.UtcNow
        };
        _db.Courses.Add(copy);
        await _db.SaveChangesAsync(ct);

        var topics = await _db.CourseTopics.AsNoTracking().Where(t => !t.IsDeleted && t.CourseId == id).OrderBy(t => t.SortOrder).ToListAsync(ct);
        var main = topics.Where(t => t.ParentTopicId == null).ToList();
        var map = new Dictionary<long, long>();
        foreach (var mt in main)
        {
            var mtCopy = new IGB.Domain.Entities.CourseTopic { CourseId = copy.Id, Title = mt.Title, SortOrder = mt.SortOrder, CreatedAt = DateTime.UtcNow };
            _db.CourseTopics.Add(mtCopy);
            await _db.SaveChangesAsync(ct);
            map[mt.Id] = mtCopy.Id;

            foreach (var st in topics.Where(x => x.ParentTopicId == mt.Id))
                _db.CourseTopics.Add(new IGB.Domain.Entities.CourseTopic { CourseId = copy.Id, ParentTopicId = mtCopy.Id, Title = st.Title, SortOrder = st.SortOrder, CreatedAt = DateTime.UtcNow });
            await _db.SaveChangesAsync(ct);
        }

        return Ok(new { id = copy.Id });
    }
}


