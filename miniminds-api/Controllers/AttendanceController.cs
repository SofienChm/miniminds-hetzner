using DaycareAPI.Data;
using DaycareAPI.Models;
using DaycareAPI.DTOs;
using DaycareAPI.Hubs;
using DaycareAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DaycareAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AttendanceController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IGeofenceService _geofenceService;

        public AttendanceController(
            ApplicationDbContext context,
            IHubContext<NotificationHub> hubContext,
            IGeofenceService geofenceService)
        {
            _context = context;
            _hubContext = hubContext;
            _geofenceService = geofenceService;
        }

        // GET: api/Attendance
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Attendance>>> GetAttendances()
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            
            if (userRole == "Parent")
            {
                var parentIdClaim = User.FindFirst("ParentId")?.Value;
                if (int.TryParse(parentIdClaim, out int parentId))
                {
                    return await _context.Attendances
                        .Include(a => a.Child)
                        .ThenInclude(c => c.Parent)
                        .Where(a => a.Child.ParentId == parentId)
                        .OrderByDescending(a => a.Date)
                        .ThenByDescending(a => a.CheckInTime)
                        .ToListAsync();
                }
                return Forbid();
            }
            
            // Admin and Teacher can see all attendance
            return await _context.Attendances
                .Include(a => a.Child)
                .ThenInclude(c => c.Parent)
                .OrderByDescending(a => a.Date)
                .ThenByDescending(a => a.CheckInTime)
                .ToListAsync();
        }

        // GET: api/Attendance/Weekly
        [HttpGet("Weekly")]
        public async Task<ActionResult<IEnumerable<object>>> GetWeeklyAttendance()
        {
            var today = DateTime.UtcNow.Date;
            var startDate = today.AddDays(-6); // Last 7 days including today

            // Get total number of active children
            var totalChildren = await _context.Children.CountAsync(c => c.IsActive);

            // If no active children, fall back to all children
            if (totalChildren == 0)
            {
                totalChildren = await _context.Children.CountAsync();
            }

            // Get attendance data for the last 7 days grouped by date
            var weeklyData = await _context.Attendances
                .Where(a => a.Date.Date >= startDate && a.Date.Date <= today)
                .GroupBy(a => a.Date.Date)
                .Select(g => new
                {
                    date = g.Key,
                    presentCount = g.Count()
                })
                .ToListAsync();

            // Build result for last 7 days (from 6 days ago to today)
            var result = Enumerable.Range(0, 7).Select(i =>
            {
                var date = startDate.AddDays(i);
                var dayData = weeklyData.FirstOrDefault(w => w.date == date);
                var presentCount = dayData?.presentCount ?? 0;

                return new
                {
                    date = date.ToString("yyyy-MM-dd"),
                    day = date.ToString("ddd"), // Mon, Tue, Wed, etc.
                    dayFull = date.DayOfWeek.ToString(),
                    presentCount = presentCount,
                    totalChildren = totalChildren,
                    percentage = totalChildren > 0
                        ? Math.Round((double)presentCount / totalChildren * 100, 1)
                        : 0
                };
            });

            return Ok(result);
        }

        // GET: api/Attendance/ByChild/5
        [HttpGet("ByChild/{childId}")]
        public async Task<ActionResult<IEnumerable<Attendance>>> GetAttendanceByChild(int childId)
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            
            if (userRole == "Parent")
            {
                var parentIdClaim = User.FindFirst("ParentId")?.Value;
                if (int.TryParse(parentIdClaim, out int parentId))
                {
                    var child = await _context.Children.FirstOrDefaultAsync(c => c.Id == childId && c.ParentId == parentId);
                    if (child == null)
                        return Forbid();
                }
                else
                {
                    return Forbid();
                }
            }
            
            return await _context.Attendances
                .Where(a => a.ChildId == childId)
                .Include(a => a.Child)
                .OrderByDescending(a => a.Date)
                .ToListAsync();
        }

        // GET: api/Attendance/ByDate?date=2024-01-01
        [HttpGet("ByDate")]
        public async Task<ActionResult<IEnumerable<Attendance>>> GetAttendanceByDate([FromQuery] DateTime date)
        {
            var targetDate = date.Date;

            return await _context.Attendances
                .Where(a => a.Date.Date == targetDate)
                .Include(a => a.Child)
                .ThenInclude(c => c.Parent)
                .OrderBy(a => a.CheckInTime)
                .ToListAsync();
        }

        // GET: api/Attendance/Today
        [HttpGet("Today")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> GetTodayAttendance()
        {
            // Return sample attendance data for testing
            var sampleData = new
            {
                totalPresent = 15,
                totalAbsent = 3,
                checkInsToday = 15,
                checkOutsToday = 12
            };
            return Ok(sampleData);
        }

        // GET: api/Attendance/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Attendance>> GetAttendance(int id)
        {
            var attendance = await _context.Attendances
                .Include(a => a.Child)
                .ThenInclude(c => c.Parent)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (attendance == null)
                return NotFound();
                
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole == "Parent")
            {
                var parentIdClaim = User.FindFirst("ParentId")?.Value;
                if (int.TryParse(parentIdClaim, out int parentId) && attendance.Child.ParentId != parentId)
                {
                    return Forbid();
                }
            }

            return attendance;
        }

        // POST: api/Attendance/CheckIn
        [HttpPost("CheckIn")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<ActionResult<Attendance>> CheckIn([FromBody] Attendance attendance)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Verify child exists
            var childExists = await _context.Children.AnyAsync(c => c.Id == attendance.ChildId);
            if (!childExists)
                return BadRequest(new { message = "Child not found" });

            // Check if already checked in today
            var today = DateTime.UtcNow.Date;
            var existingAttendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.ChildId == attendance.ChildId && 
                                         a.Date.Date == today && 
                                         a.CheckOutTime == null);

            if (existingAttendance != null)
                return BadRequest(new { message = "Child is already checked in today" });

            attendance.Date = DateTime.UtcNow.Date;
            attendance.CheckInTime = DateTime.UtcNow;
            attendance.CreatedAt = DateTime.UtcNow;

            _context.Attendances.Add(attendance);
            
            // Create a daily activity for check-in
            var checkInActivity = new DailyActivity
            {
                ChildId = attendance.ChildId,
                ActivityType = "Check-in",
                ActivityTime = attendance.CheckInTime,
                Notes = "Child arrived at daycare",
                CreatedAt = DateTime.UtcNow
            };
            _context.DailyActivities.Add(checkInActivity);
            
            await _context.SaveChangesAsync();
            
            // Notify all clients about attendance update
            await _hubContext.Clients.All.SendAsync("AttendanceUpdated");

            return CreatedAtAction(nameof(GetAttendance), new { id = attendance.Id }, attendance);
        }

        // POST: api/Attendance/CheckOut/5
        [HttpPost("CheckOut/{id}")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> CheckOut(int id, [FromBody] string? checkOutNotes)
        {
            var attendance = await _context.Attendances.FindAsync(id);
            if (attendance == null)
                return NotFound();

            if (attendance.CheckOutTime != null)
                return BadRequest(new { message = "Child is already checked out" });

            attendance.CheckOutTime = DateTime.UtcNow;
            attendance.CheckOutNotes = checkOutNotes;
            attendance.UpdatedAt = DateTime.UtcNow;
            
            // Create a daily activity for check-out
            var checkOutActivity = new DailyActivity
            {
                ChildId = attendance.ChildId,
                ActivityType = "Check-out",
                ActivityTime = attendance.CheckOutTime.Value,
                Notes = !string.IsNullOrEmpty(checkOutNotes) ? checkOutNotes : "Child left daycare",
                CreatedAt = DateTime.UtcNow
            };
            _context.DailyActivities.Add(checkOutActivity);

            await _context.SaveChangesAsync();
            
            // Notify all clients about attendance update
            await _hubContext.Clients.All.SendAsync("AttendanceUpdated");

            return Ok(attendance);
        }

        // PUT: api/Attendance/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> UpdateAttendance(int id, Attendance attendance)
        {
            if (id != attendance.Id)
                return BadRequest();

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            attendance.UpdatedAt = DateTime.UtcNow;
            _context.Entry(attendance).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AttendanceExists(id))
                    return NotFound();
                throw;
            }

            return NoContent();
        }

        // DELETE: api/Attendance/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> DeleteAttendance(int id)
        {
            var attendance = await _context.Attendances.FindAsync(id);
            if (attendance == null)
                return NotFound();

            _context.Attendances.Remove(attendance);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool AttendanceExists(int id)
        {
            return _context.Attendances.Any(e => e.Id == id);
        }

        // POST: api/Attendance/QrCheckIn
        [HttpPost("QrCheckIn")]
        [Authorize(Roles = "Admin,Teacher,Parent")]
        public async Task<ActionResult<QrAttendanceResultDto>> QrCheckIn([FromBody] QrCheckInDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = new QrAttendanceResultDto
            {
                Success = false,
                Results = new List<AttendanceResultItem>()
            };

            // Validate QR code
            var qrCode = await _context.QrCodes
                .FirstOrDefaultAsync(q => q.Code == dto.QrCode && q.IsActive && q.Type == "CheckIn");

            if (qrCode == null)
            {
                result.Message = "Invalid or expired check-in QR code";
                return BadRequest(result);
            }

            // Validate geofence
            var settings = await _context.SchoolSettings.FirstOrDefaultAsync();
            if (settings != null && settings.GeofenceEnabled)
            {
                var isWithinGeofence = _geofenceService.IsWithinRadius(
                    dto.Latitude, dto.Longitude,
                    settings.Latitude, settings.Longitude,
                    settings.GeofenceRadiusMeters);

                if (!isWithinGeofence)
                {
                    var distance = _geofenceService.CalculateDistance(
                        dto.Latitude, dto.Longitude,
                        settings.Latitude, settings.Longitude);
                    result.Message = $"You must be within {settings.GeofenceRadiusMeters}m of the school to check in. Current distance: {Math.Round(distance)}m";
                    return BadRequest(result);
                }
            }

            // Verify parent owns these children
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var parentIdClaim = User.FindFirst("ParentId")?.Value;

            if (userRole == "Parent" && int.TryParse(parentIdClaim, out int parentId))
            {
                var parentChildren = await _context.Children
                    .Where(c => c.ParentId == parentId)
                    .Select(c => c.Id)
                    .ToListAsync();

                var unauthorizedChildren = dto.ChildIds.Except(parentChildren).ToList();
                if (unauthorizedChildren.Any())
                {
                    result.Message = "You can only check in your own children";
                    return Forbid();
                }
            }

            var today = DateTime.UtcNow.Date;

            foreach (var childId in dto.ChildIds)
            {
                var resultItem = new AttendanceResultItem { ChildId = childId };

                // Get child info
                var child = await _context.Children.FirstOrDefaultAsync(c => c.Id == childId);
                if (child == null)
                {
                    resultItem.Success = false;
                    resultItem.Message = "Child not found";
                    result.Results.Add(resultItem);
                    continue;
                }

                resultItem.ChildName = $"{child.FirstName} {child.LastName}";

                // Check if already checked in today
                var existingAttendance = await _context.Attendances
                    .FirstOrDefaultAsync(a => a.ChildId == childId &&
                                             a.Date.Date == today &&
                                             a.CheckOutTime == null);

                if (existingAttendance != null)
                {
                    resultItem.Success = false;
                    resultItem.Message = "Already checked in today";
                    result.Results.Add(resultItem);
                    continue;
                }

                // Create attendance record
                var attendance = new Attendance
                {
                    ChildId = childId,
                    Date = today,
                    CheckInTime = DateTime.UtcNow,
                    CheckInNotes = dto.Notes ?? "Checked in via QR code",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Attendances.Add(attendance);

                // Create daily activity
                var checkInActivity = new DailyActivity
                {
                    ChildId = childId,
                    ActivityType = "Check-in",
                    ActivityTime = attendance.CheckInTime,
                    Notes = "Child arrived at daycare (QR check-in)",
                    CreatedAt = DateTime.UtcNow
                };
                _context.DailyActivities.Add(checkInActivity);

                await _context.SaveChangesAsync();

                resultItem.Success = true;
                resultItem.Message = "Checked in successfully";
                resultItem.AttendanceId = attendance.Id;
                result.Results.Add(resultItem);
            }

            // Notify clients
            await _hubContext.Clients.All.SendAsync("AttendanceUpdated");

            result.Success = result.Results.Any(r => r.Success);
            result.Message = result.Success
                ? $"Successfully checked in {result.Results.Count(r => r.Success)} child(ren)"
                : "No children were checked in";

            return Ok(result);
        }

        // POST: api/Attendance/QrCheckOut
        [HttpPost("QrCheckOut")]
        [Authorize(Roles = "Admin,Teacher,Parent")]
        public async Task<ActionResult<QrAttendanceResultDto>> QrCheckOut([FromBody] QrCheckOutDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = new QrAttendanceResultDto
            {
                Success = false,
                Results = new List<AttendanceResultItem>()
            };

            // Validate QR code
            var qrCode = await _context.QrCodes
                .FirstOrDefaultAsync(q => q.Code == dto.QrCode && q.IsActive && q.Type == "CheckOut");

            if (qrCode == null)
            {
                result.Message = "Invalid or expired check-out QR code";
                return BadRequest(result);
            }

            // Validate geofence
            var settings = await _context.SchoolSettings.FirstOrDefaultAsync();
            if (settings != null && settings.GeofenceEnabled)
            {
                var isWithinGeofence = _geofenceService.IsWithinRadius(
                    dto.Latitude, dto.Longitude,
                    settings.Latitude, settings.Longitude,
                    settings.GeofenceRadiusMeters);

                if (!isWithinGeofence)
                {
                    var distance = _geofenceService.CalculateDistance(
                        dto.Latitude, dto.Longitude,
                        settings.Latitude, settings.Longitude);
                    result.Message = $"You must be within {settings.GeofenceRadiusMeters}m of the school to check out. Current distance: {Math.Round(distance)}m";
                    return BadRequest(result);
                }
            }

            // Verify parent owns these children
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var parentIdClaim = User.FindFirst("ParentId")?.Value;

            if (userRole == "Parent" && int.TryParse(parentIdClaim, out int parentId))
            {
                var parentChildren = await _context.Children
                    .Where(c => c.ParentId == parentId)
                    .Select(c => c.Id)
                    .ToListAsync();

                var unauthorizedChildren = dto.ChildIds.Except(parentChildren).ToList();
                if (unauthorizedChildren.Any())
                {
                    result.Message = "You can only check out your own children";
                    return Forbid();
                }
            }

            var today = DateTime.UtcNow.Date;

            foreach (var childId in dto.ChildIds)
            {
                var resultItem = new AttendanceResultItem { ChildId = childId };

                // Get child info
                var child = await _context.Children.FirstOrDefaultAsync(c => c.Id == childId);
                if (child == null)
                {
                    resultItem.Success = false;
                    resultItem.Message = "Child not found";
                    result.Results.Add(resultItem);
                    continue;
                }

                resultItem.ChildName = $"{child.FirstName} {child.LastName}";

                // Find active check-in
                var attendance = await _context.Attendances
                    .FirstOrDefaultAsync(a => a.ChildId == childId &&
                                             a.Date.Date == today &&
                                             a.CheckOutTime == null);

                if (attendance == null)
                {
                    resultItem.Success = false;
                    resultItem.Message = "Not checked in today";
                    result.Results.Add(resultItem);
                    continue;
                }

                // Update attendance record
                attendance.CheckOutTime = DateTime.UtcNow;
                attendance.CheckOutNotes = dto.Notes ?? "Checked out via QR code";
                attendance.UpdatedAt = DateTime.UtcNow;

                // Create daily activity
                var checkOutActivity = new DailyActivity
                {
                    ChildId = childId,
                    ActivityType = "Check-out",
                    ActivityTime = attendance.CheckOutTime.Value,
                    Notes = "Child left daycare (QR check-out)",
                    CreatedAt = DateTime.UtcNow
                };
                _context.DailyActivities.Add(checkOutActivity);

                await _context.SaveChangesAsync();

                resultItem.Success = true;
                resultItem.Message = "Checked out successfully";
                resultItem.AttendanceId = attendance.Id;
                result.Results.Add(resultItem);
            }

            // Notify clients
            await _hubContext.Clients.All.SendAsync("AttendanceUpdated");

            result.Success = result.Results.Any(r => r.Success);
            result.Message = result.Success
                ? $"Successfully checked out {result.Results.Count(r => r.Success)} child(ren)"
                : "No children were checked out";

            return Ok(result);
        }

        // GET: api/Attendance/MyChildren
        [HttpGet("MyChildren")]
        [Authorize(Roles = "Parent")]
        public async Task<ActionResult<object>> GetMyChildrenAttendanceStatus()
        {
            var parentIdClaim = User.FindFirst("ParentId")?.Value;
            if (!int.TryParse(parentIdClaim, out int parentId))
            {
                return Forbid();
            }

            var today = DateTime.UtcNow.Date;

            var children = await _context.Children
                .Where(c => c.ParentId == parentId && c.IsActive)
                .Select(c => new
                {
                    c.Id,
                    c.FirstName,
                    c.LastName,
                    c.ProfilePicture,
                    TodayAttendance = _context.Attendances
                        .Where(a => a.ChildId == c.Id && a.Date.Date == today)
                        .OrderByDescending(a => a.CheckInTime)
                        .FirstOrDefault()
                })
                .ToListAsync();

            var result = children.Select(c => new
            {
                c.Id,
                c.FirstName,
                c.LastName,
                c.ProfilePicture,
                IsCheckedIn = c.TodayAttendance != null && c.TodayAttendance.CheckOutTime == null,
                IsCheckedOut = c.TodayAttendance != null && c.TodayAttendance.CheckOutTime != null,
                CheckInTime = c.TodayAttendance?.CheckInTime,
                CheckOutTime = c.TodayAttendance?.CheckOutTime,
                AttendanceId = c.TodayAttendance?.Id
            });

            return Ok(result);
        }
    }
}