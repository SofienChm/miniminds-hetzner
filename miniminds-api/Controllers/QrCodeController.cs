using DaycareAPI.Data;
using DaycareAPI.Models;
using DaycareAPI.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DaycareAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class QrCodeController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public QrCodeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/QrCode/CheckIn
        [HttpGet("CheckIn")]
        public async Task<ActionResult<QrCodeDto>> GetCheckInQrCode()
        {
            var qrCode = await _context.QrCodes
                .Where(q => q.Type == "CheckIn" && q.IsActive)
                .OrderByDescending(q => q.CreatedAt)
                .FirstOrDefaultAsync();

            if (qrCode == null)
            {
                // Auto-generate if not exists
                qrCode = await GenerateQrCodeInternal("CheckIn");
            }

            return Ok(new QrCodeDto
            {
                Id = qrCode.Id,
                Code = qrCode.Code,
                Type = qrCode.Type,
                IsActive = qrCode.IsActive,
                CreatedAt = qrCode.CreatedAt
            });
        }

        // GET: api/QrCode/CheckOut
        [HttpGet("CheckOut")]
        public async Task<ActionResult<QrCodeDto>> GetCheckOutQrCode()
        {
            var qrCode = await _context.QrCodes
                .Where(q => q.Type == "CheckOut" && q.IsActive)
                .OrderByDescending(q => q.CreatedAt)
                .FirstOrDefaultAsync();

            if (qrCode == null)
            {
                // Auto-generate if not exists
                qrCode = await GenerateQrCodeInternal("CheckOut");
            }

            return Ok(new QrCodeDto
            {
                Id = qrCode.Id,
                Code = qrCode.Code,
                Type = qrCode.Type,
                IsActive = qrCode.IsActive,
                CreatedAt = qrCode.CreatedAt
            });
        }

        // GET: api/QrCode/All
        [HttpGet("All")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<QrCodeDto>>> GetAllQrCodes()
        {
            var qrCodes = await _context.QrCodes
                .OrderByDescending(q => q.CreatedAt)
                .Select(q => new QrCodeDto
                {
                    Id = q.Id,
                    Code = q.Code,
                    Type = q.Type,
                    IsActive = q.IsActive,
                    CreatedAt = q.CreatedAt
                })
                .ToListAsync();

            return Ok(qrCodes);
        }

        // POST: api/QrCode/Generate
        [HttpPost("Generate")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<QrCodeDto>> GenerateQrCode([FromBody] string type)
        {
            if (type != "CheckIn" && type != "CheckOut")
            {
                return BadRequest(new { message = "Type must be 'CheckIn' or 'CheckOut'" });
            }

            // Deactivate existing QR codes of the same type
            var existingCodes = await _context.QrCodes
                .Where(q => q.Type == type && q.IsActive)
                .ToListAsync();

            foreach (var code in existingCodes)
            {
                code.IsActive = false;
                code.UpdatedAt = DateTime.UtcNow;
            }

            var qrCode = await GenerateQrCodeInternal(type);

            return Ok(new QrCodeDto
            {
                Id = qrCode.Id,
                Code = qrCode.Code,
                Type = qrCode.Type,
                IsActive = qrCode.IsActive,
                CreatedAt = qrCode.CreatedAt
            });
        }

        // POST: api/QrCode/RegenerateAll
        [HttpPost("RegenerateAll")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<object>> RegenerateAllQrCodes()
        {
            // Deactivate all existing QR codes
            var existingCodes = await _context.QrCodes
                .Where(q => q.IsActive)
                .ToListAsync();

            foreach (var code in existingCodes)
            {
                code.IsActive = false;
                code.UpdatedAt = DateTime.UtcNow;
            }

            // Generate new codes
            var checkInCode = await GenerateQrCodeInternal("CheckIn");
            var checkOutCode = await GenerateQrCodeInternal("CheckOut");

            return Ok(new
            {
                checkIn = new QrCodeDto
                {
                    Id = checkInCode.Id,
                    Code = checkInCode.Code,
                    Type = checkInCode.Type,
                    IsActive = checkInCode.IsActive,
                    CreatedAt = checkInCode.CreatedAt
                },
                checkOut = new QrCodeDto
                {
                    Id = checkOutCode.Id,
                    Code = checkOutCode.Code,
                    Type = checkOutCode.Type,
                    IsActive = checkOutCode.IsActive,
                    CreatedAt = checkOutCode.CreatedAt
                }
            });
        }

        // GET: api/QrCode/Validate/{code}
        [HttpGet("Validate/{code}")]
        public async Task<ActionResult<QrValidationResponseDto>> ValidateQrCode(string code)
        {
            var qrCode = await _context.QrCodes
                .FirstOrDefaultAsync(q => q.Code == code && q.IsActive);

            if (qrCode == null)
            {
                return Ok(new QrValidationResponseDto
                {
                    IsValid = false,
                    Type = "",
                    Message = "Invalid or expired QR code"
                });
            }

            return Ok(new QrValidationResponseDto
            {
                IsValid = true,
                Type = qrCode.Type,
                Message = $"Valid {qrCode.Type} QR code"
            });
        }

        // GET: api/QrCode/Settings
        [HttpGet("Settings")]
        public async Task<ActionResult<SchoolSettingsDto>> GetSchoolSettings()
        {
            var settings = await _context.SchoolSettings.FirstOrDefaultAsync();

            if (settings == null)
            {
                // Return default settings if none exist
                return Ok(new SchoolSettingsDto
                {
                    Id = 0,
                    SchoolName = "MiniMinds Daycare",
                    Latitude = 0,
                    Longitude = 0,
                    GeofenceRadiusMeters = 100,
                    GeofenceEnabled = true
                });
            }

            return Ok(new SchoolSettingsDto
            {
                Id = settings.Id,
                SchoolName = settings.SchoolName,
                Latitude = settings.Latitude,
                Longitude = settings.Longitude,
                GeofenceRadiusMeters = settings.GeofenceRadiusMeters,
                GeofenceEnabled = settings.GeofenceEnabled
            });
        }

        // PUT: api/QrCode/Settings
        [HttpPut("Settings")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<SchoolSettingsDto>> UpdateSchoolSettings([FromBody] UpdateSchoolSettingsDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var settings = await _context.SchoolSettings.FirstOrDefaultAsync();

            if (settings == null)
            {
                // Create new settings
                settings = new SchoolSettings
                {
                    SchoolName = dto.SchoolName ?? "MiniMinds Daycare",
                    Latitude = dto.Latitude,
                    Longitude = dto.Longitude,
                    GeofenceRadiusMeters = dto.GeofenceRadiusMeters,
                    GeofenceEnabled = dto.GeofenceEnabled,
                    CreatedAt = DateTime.UtcNow
                };
                _context.SchoolSettings.Add(settings);
            }
            else
            {
                // Update existing settings
                if (!string.IsNullOrEmpty(dto.SchoolName))
                    settings.SchoolName = dto.SchoolName;
                settings.Latitude = dto.Latitude;
                settings.Longitude = dto.Longitude;
                settings.GeofenceRadiusMeters = dto.GeofenceRadiusMeters;
                settings.GeofenceEnabled = dto.GeofenceEnabled;
                settings.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Ok(new SchoolSettingsDto
            {
                Id = settings.Id,
                SchoolName = settings.SchoolName,
                Latitude = settings.Latitude,
                Longitude = settings.Longitude,
                GeofenceRadiusMeters = settings.GeofenceRadiusMeters,
                GeofenceEnabled = settings.GeofenceEnabled
            });
        }

        private async Task<QrCode> GenerateQrCodeInternal(string type)
        {
            var qrCode = new QrCode
            {
                Code = $"MINIMINDS-{type.ToUpper()}-{Guid.NewGuid():N}".Substring(0, 50),
                Type = type,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.QrCodes.Add(qrCode);
            await _context.SaveChangesAsync();

            return qrCode;
        }
    }
}
