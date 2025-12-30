using DaycareAPI.Data;
using DaycareAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using SkiaSharp;

namespace DaycareAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PhotosController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        // Image settings for optimization
        private const int ThumbnailWidth = 300;
        private const int ThumbnailHeight = 300;
        private const int MaxImageWidth = 1920;
        private const int MaxImageHeight = 1080;
        private const int ImageQuality = 80;

        public PhotosController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // GET: api/Photos - Returns thumbnails only for fast gallery loading
        [HttpGet]
        public async Task<ActionResult<object>> GetPhotos(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] int? childId = null,
            [FromQuery] string? category = null)
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var query = _context.Photos
                .Include(p => p.Child)
                .Include(p => p.UploadedBy)
                .AsQueryable();

            // Parents can only see photos of their children
            if (userRole == "Parent")
            {
                var parentIdClaim = User.FindFirst("ParentId")?.Value;
                if (int.TryParse(parentIdClaim, out int parentId))
                {
                    var childIds = await _context.Children
                        .Where(c => c.ParentId == parentId)
                        .Select(c => c.Id)
                        .ToListAsync();
                    query = query.Where(p => childIds.Contains(p.ChildId));
                }
                else
                {
                    return Forbid();
                }
            }

            // Filter by child if specified
            if (childId.HasValue)
            {
                query = query.Where(p => p.ChildId == childId.Value);
            }

            // Filter by category if specified
            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(p => p.Category == category);
            }

            var totalCount = await query.CountAsync();

            // Select only needed fields - exclude ImageData for fast loading
            var photos = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    p.Id,
                    p.FileName,
                    p.FileType,
                    p.FileSize,
                    p.Title,
                    p.Description,
                    p.Category,
                    p.ChildId,
                    ChildName = p.Child != null ? $"{p.Child.FirstName} {p.Child.LastName}" : null,
                    p.UploadedById,
                    UploadedByName = p.UploadedBy != null ? $"{p.UploadedBy.FirstName} {p.UploadedBy.LastName}" : null,
                    p.ThumbnailData, // Small thumbnail for gallery
                    p.CreatedAt,
                    p.UpdatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                data = photos,
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }

        // GET: api/Photos/5 - Returns full image data
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetPhoto(int id)
        {
            var photo = await _context.Photos
                .Include(p => p.Child)
                .Include(p => p.UploadedBy)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (photo == null)
                return NotFound();

            // Check if parent can access this photo
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole == "Parent")
            {
                var parentIdClaim = User.FindFirst("ParentId")?.Value;
                if (int.TryParse(parentIdClaim, out int parentId))
                {
                    var child = await _context.Children.FindAsync(photo.ChildId);
                    if (child == null || child.ParentId != parentId)
                    {
                        return Forbid();
                    }
                }
                else
                {
                    return Forbid();
                }
            }

            return Ok(new
            {
                photo.Id,
                photo.FileName,
                photo.FileType,
                photo.FileSize,
                photo.Title,
                photo.Description,
                photo.Category,
                photo.RelatedEntityType,
                photo.RelatedEntityId,
                photo.ChildId,
                ChildName = photo.Child != null ? $"{photo.Child.FirstName} {photo.Child.LastName}" : null,
                photo.UploadedById,
                UploadedByName = photo.UploadedBy != null ? $"{photo.UploadedBy.FirstName} {photo.UploadedBy.LastName}" : null,
                photo.ImageData, // Full resolution image
                photo.ThumbnailData,
                photo.CreatedAt,
                photo.UpdatedAt
            });
        }

        // GET: api/Photos/5/image - Returns just the image data (for img src)
        [HttpGet("{id}/image")]
        public async Task<ActionResult> GetPhotoImage(int id)
        {
            var photo = await _context.Photos
                .Select(p => new { p.Id, p.ImageData, p.FileType, p.ChildId })
                .FirstOrDefaultAsync(p => p.Id == id);

            if (photo == null || string.IsNullOrEmpty(photo.ImageData))
                return NotFound();

            // Check parent access
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole == "Parent")
            {
                var parentIdClaim = User.FindFirst("ParentId")?.Value;
                if (int.TryParse(parentIdClaim, out int parentId))
                {
                    var child = await _context.Children.FindAsync(photo.ChildId);
                    if (child == null || child.ParentId != parentId)
                    {
                        return Forbid();
                    }
                }
            }

            // Return as data URL or decode base64
            return Ok(new { imageData = photo.ImageData });
        }

        // GET: api/Photos/by-child/5
        [HttpGet("by-child/{childId}")]
        public async Task<ActionResult<object>> GetPhotosByChild(
            int childId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? category = null)
        {
            var child = await _context.Children.FindAsync(childId);
            if (child == null)
                return NotFound(new { message = "Child not found" });

            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole == "Parent")
            {
                var parentIdClaim = User.FindFirst("ParentId")?.Value;
                if (int.TryParse(parentIdClaim, out int parentId) && child.ParentId != parentId)
                {
                    return Forbid();
                }
            }

            var query = _context.Photos
                .Include(p => p.UploadedBy)
                .Where(p => p.ChildId == childId);

            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(p => p.Category == category);
            }

            var totalCount = await query.CountAsync();
            var photos = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    p.Id,
                    p.FileName,
                    p.FileType,
                    p.FileSize,
                    p.Title,
                    p.Description,
                    p.Category,
                    p.ChildId,
                    p.UploadedById,
                    UploadedByName = p.UploadedBy != null ? $"{p.UploadedBy.FirstName} {p.UploadedBy.LastName}" : null,
                    p.ThumbnailData,
                    p.CreatedAt,
                    p.UpdatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                data = photos,
                childName = $"{child.FirstName} {child.LastName}",
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }

        // POST: api/Photos/upload - Stores image in database
        [HttpPost("upload")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<ActionResult<object>> UploadPhoto(
            IFormFile file,
            [FromForm] int childId,
            [FromForm] string? title = null,
            [FromForm] string? description = null,
            [FromForm] string category = "Memory",
            [FromForm] string? relatedEntityType = null,
            [FromForm] int? relatedEntityId = null)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded" });

            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
            if (!allowedTypes.Contains(file.ContentType.ToLower()))
                return BadRequest(new { message = "Only image files (JPEG, PNG, GIF, WebP) are allowed" });

            if (file.Length > 10 * 1024 * 1024)
                return BadRequest(new { message = "File size must be less than 10MB" });

            var child = await _context.Children.FindAsync(childId);
            if (child == null)
                return BadRequest(new { message = "Child not found" });

            // Get current user ID
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId) && userId.Contains("@"))
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userId);
                userId = user?.Id;
            }

            // Process image - compress and create thumbnail
            string imageData;
            string thumbnailData;
            long compressedSize;

            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                var imageBytes = memoryStream.ToArray();

                (imageData, thumbnailData, compressedSize) = ProcessImage(imageBytes);
            }

            var photo = new Photo
            {
                FileName = file.FileName,
                FileType = "image/jpeg", // Always JPEG after compression
                FileSize = compressedSize,
                ImageData = imageData,
                ThumbnailData = thumbnailData,
                Title = title,
                Description = description,
                Category = category,
                RelatedEntityType = relatedEntityType,
                RelatedEntityId = relatedEntityId,
                ChildId = childId,
                UploadedById = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Photos.Add(photo);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPhoto), new { id = photo.Id }, new
            {
                photo.Id,
                photo.FileName,
                photo.FileType,
                photo.FileSize,
                photo.Title,
                photo.Description,
                photo.Category,
                photo.ChildId,
                ChildName = $"{child.FirstName} {child.LastName}",
                photo.ThumbnailData,
                photo.CreatedAt
            });
        }

        // POST: api/Photos/upload-multiple
        [HttpPost("upload-multiple")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<ActionResult<object>> UploadMultiplePhotos(
            [FromForm] List<IFormFile> files,
            [FromForm] int childId,
            [FromForm] string category = "Memory",
            [FromForm] string? description = null)
        {
            if (files == null || files.Count == 0)
                return BadRequest(new { message = "No files uploaded" });

            if (files.Count > 20)
                return BadRequest(new { message = "Maximum 20 files can be uploaded at once" });

            var child = await _context.Children.FindAsync(childId);
            if (child == null)
                return BadRequest(new { message = "Child not found" });

            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId) && userId.Contains("@"))
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userId);
                userId = user?.Id;
            }

            var uploadedPhotos = new List<object>();
            var errors = new List<string>();

            foreach (var file in files)
            {
                try
                {
                    if (!allowedTypes.Contains(file.ContentType.ToLower()))
                    {
                        errors.Add($"{file.FileName}: Invalid file type");
                        continue;
                    }

                    if (file.Length > 10 * 1024 * 1024)
                    {
                        errors.Add($"{file.FileName}: File too large");
                        continue;
                    }

                    string imageData;
                    string thumbnailData;
                    long compressedSize;

                    using (var memoryStream = new MemoryStream())
                    {
                        await file.CopyToAsync(memoryStream);
                        var imageBytes = memoryStream.ToArray();
                        (imageData, thumbnailData, compressedSize) = ProcessImage(imageBytes);
                    }

                    var photo = new Photo
                    {
                        FileName = file.FileName,
                        FileType = "image/jpeg",
                        FileSize = compressedSize,
                        ImageData = imageData,
                        ThumbnailData = thumbnailData,
                        Description = description,
                        Category = category,
                        ChildId = childId,
                        UploadedById = userId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.Photos.Add(photo);
                    await _context.SaveChangesAsync();

                    uploadedPhotos.Add(new
                    {
                        photo.Id,
                        photo.FileName,
                        photo.Category,
                        photo.ThumbnailData,
                        photo.CreatedAt
                    });
                }
                catch (Exception ex)
                {
                    errors.Add($"{file.FileName}: {ex.Message}");
                }
            }

            return Ok(new
            {
                message = $"Successfully uploaded {uploadedPhotos.Count} photo(s)",
                uploaded = uploadedPhotos,
                errors = errors.Count > 0 ? errors : null
            });
        }

        // PUT: api/Photos/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> UpdatePhoto(int id, [FromBody] UpdatePhotoDto dto)
        {
            var photo = await _context.Photos.FindAsync(id);
            if (photo == null)
                return NotFound();

            if (!string.IsNullOrEmpty(dto.Title))
                photo.Title = dto.Title;
            if (!string.IsNullOrEmpty(dto.Description))
                photo.Description = dto.Description;
            if (!string.IsNullOrEmpty(dto.Category))
                photo.Category = dto.Category;

            photo.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Photo updated successfully" });
        }

        // DELETE: api/Photos/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<IActionResult> DeletePhoto(int id)
        {
            var photo = await _context.Photos.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id);
            if (photo == null)
                return NotFound();

            // Soft delete
            photo.IsDeleted = true;
            photo.DeletedAt = DateTime.UtcNow;
            photo.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Photo deleted successfully" });
        }

        // DELETE: api/Photos/5/permanent
        [HttpDelete("{id}/permanent")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PermanentlyDeletePhoto(int id)
        {
            var photo = await _context.Photos.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id);
            if (photo == null)
                return NotFound();

            _context.Photos.Remove(photo);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Photo permanently deleted" });
        }

        // POST: api/Photos/5/restore
        [HttpPost("{id}/restore")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RestorePhoto(int id)
        {
            var photo = await _context.Photos.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id);
            if (photo == null)
                return NotFound();

            photo.IsDeleted = false;
            photo.DeletedAt = null;
            photo.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Photo restored successfully" });
        }

        // GET: api/Photos/categories
        [HttpGet("categories")]
        public ActionResult<IEnumerable<string>> GetCategories()
        {
            return Ok(new[] { "Memory", "Activity", "Event", "General" });
        }

        // GET: api/Photos/by-activity/5
        [HttpGet("by-activity/{activityId}")]
        public async Task<ActionResult<object>> GetPhotosByActivity(int activityId)
        {
            var activity = await _context.DailyActivities
                .Include(a => a.Child)
                .FirstOrDefaultAsync(a => a.Id == activityId);

            if (activity == null)
                return NotFound(new { message = "Activity not found" });

            // Check parent access
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole == "Parent")
            {
                var parentIdClaim = User.FindFirst("ParentId")?.Value;
                if (!int.TryParse(parentIdClaim, out int parentId) ||
                    activity.Child == null ||
                    activity.Child.ParentId != parentId)
                {
                    return Forbid();
                }
            }

            var photos = await _context.Photos
                .Where(p => p.ActivityId == activityId)
                .Include(p => p.UploadedBy)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new
                {
                    p.Id,
                    p.FileName,
                    p.FileType,
                    p.FileSize,
                    p.Title,
                    p.Description,
                    p.Category,
                    p.ChildId,
                    p.ActivityId,
                    p.UploadedById,
                    UploadedByName = p.UploadedBy != null ? $"{p.UploadedBy.FirstName} {p.UploadedBy.LastName}" : null,
                    p.ThumbnailData,
                    p.CreatedAt,
                    p.UpdatedAt
                })
                .ToListAsync();

            return Ok(new { data = photos, totalCount = photos.Count });
        }

        // POST: api/Photos/upload-activity - Upload photo for an activity
        [HttpPost("upload-activity")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<ActionResult<object>> UploadActivityPhoto(
            IFormFile file,
            [FromForm] int activityId,
            [FromForm] string? title = null,
            [FromForm] string? description = null)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded" });

            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
            if (!allowedTypes.Contains(file.ContentType.ToLower()))
                return BadRequest(new { message = "Only image files (JPEG, PNG, GIF, WebP) are allowed" });

            if (file.Length > 10 * 1024 * 1024)
                return BadRequest(new { message = "File size must be less than 10MB" });

            var activity = await _context.DailyActivities
                .Include(a => a.Child)
                .FirstOrDefaultAsync(a => a.Id == activityId);

            if (activity == null)
                return BadRequest(new { message = "Activity not found" });

            // Get current user ID
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId) && userId.Contains("@"))
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userId);
                userId = user?.Id;
            }

            // Process image - compress and create thumbnail
            string imageData;
            string thumbnailData;
            long compressedSize;

            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                var imageBytes = memoryStream.ToArray();

                (imageData, thumbnailData, compressedSize) = ProcessImage(imageBytes);
            }

            var photo = new Photo
            {
                FileName = file.FileName,
                FileType = "image/jpeg",
                FileSize = compressedSize,
                ImageData = imageData,
                ThumbnailData = thumbnailData,
                Title = title,
                Description = description,
                Category = "Activity",
                ActivityId = activityId,
                ChildId = activity.ChildId,
                UploadedById = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Photos.Add(photo);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPhoto), new { id = photo.Id }, new
            {
                photo.Id,
                photo.FileName,
                photo.FileType,
                photo.FileSize,
                photo.Title,
                photo.Description,
                photo.Category,
                photo.ActivityId,
                photo.ChildId,
                ChildName = activity.Child != null ? $"{activity.Child.FirstName} {activity.Child.LastName}" : null,
                photo.ThumbnailData,
                photo.CreatedAt
            });
        }

        // POST: api/Photos/upload-activity-multiple - Upload multiple photos for an activity
        [HttpPost("upload-activity-multiple")]
        [Authorize(Roles = "Admin,Teacher")]
        public async Task<ActionResult<object>> UploadMultipleActivityPhotos(
            [FromForm] List<IFormFile> files,
            [FromForm] int activityId,
            [FromForm] string? description = null)
        {
            if (files == null || files.Count == 0)
                return BadRequest(new { message = "No files uploaded" });

            if (files.Count > 20)
                return BadRequest(new { message = "Maximum 20 files can be uploaded at once" });

            var activity = await _context.DailyActivities
                .Include(a => a.Child)
                .FirstOrDefaultAsync(a => a.Id == activityId);

            if (activity == null)
                return BadRequest(new { message = "Activity not found" });

            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId) && userId.Contains("@"))
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == userId);
                userId = user?.Id;
            }

            var uploadedPhotos = new List<object>();
            var errors = new List<string>();

            foreach (var file in files)
            {
                try
                {
                    if (!allowedTypes.Contains(file.ContentType.ToLower()))
                    {
                        errors.Add($"{file.FileName}: Invalid file type");
                        continue;
                    }

                    if (file.Length > 10 * 1024 * 1024)
                    {
                        errors.Add($"{file.FileName}: File too large");
                        continue;
                    }

                    string imageData;
                    string thumbnailData;
                    long compressedSize;

                    using (var memoryStream = new MemoryStream())
                    {
                        await file.CopyToAsync(memoryStream);
                        var imageBytes = memoryStream.ToArray();
                        (imageData, thumbnailData, compressedSize) = ProcessImage(imageBytes);
                    }

                    var photo = new Photo
                    {
                        FileName = file.FileName,
                        FileType = "image/jpeg",
                        FileSize = compressedSize,
                        ImageData = imageData,
                        ThumbnailData = thumbnailData,
                        Description = description,
                        Category = "Activity",
                        ActivityId = activityId,
                        ChildId = activity.ChildId,
                        UploadedById = userId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.Photos.Add(photo);
                    await _context.SaveChangesAsync();

                    uploadedPhotos.Add(new
                    {
                        photo.Id,
                        photo.FileName,
                        photo.Category,
                        photo.ThumbnailData,
                        photo.CreatedAt
                    });
                }
                catch (Exception ex)
                {
                    errors.Add($"{file.FileName}: {ex.Message}");
                }
            }

            return Ok(new
            {
                message = $"Successfully uploaded {uploadedPhotos.Count} photo(s)",
                uploaded = uploadedPhotos,
                errors = errors.Count > 0 ? errors : null
            });
        }

        /// <summary>
        /// Process image: compress full image and generate thumbnail
        /// Returns (imageDataBase64, thumbnailDataBase64, compressedSize)
        /// </summary>
        private (string imageData, string thumbnailData, long compressedSize) ProcessImage(byte[] imageBytes)
        {
            using var inputStream = new MemoryStream(imageBytes);
            using var originalBitmap = SKBitmap.Decode(inputStream);

            if (originalBitmap == null)
                throw new Exception("Could not decode image");

            // Calculate dimensions for full image (max 1920x1080)
            var (fullWidth, fullHeight) = CalculateDimensions(
                originalBitmap.Width,
                originalBitmap.Height,
                MaxImageWidth,
                MaxImageHeight);

            // Calculate dimensions for thumbnail (max 300x300)
            var (thumbWidth, thumbHeight) = CalculateDimensions(
                originalBitmap.Width,
                originalBitmap.Height,
                ThumbnailWidth,
                ThumbnailHeight);

            // Create resized full image
            using var fullBitmap = originalBitmap.Resize(new SKImageInfo(fullWidth, fullHeight), SKFilterQuality.High);
            using var fullImage = SKImage.FromBitmap(fullBitmap);
            using var fullData = fullImage.Encode(SKEncodedImageFormat.Jpeg, ImageQuality);
            var fullBytes = fullData.ToArray();

            // Create thumbnail
            using var thumbBitmap = originalBitmap.Resize(new SKImageInfo(thumbWidth, thumbHeight), SKFilterQuality.Medium);
            using var thumbImage = SKImage.FromBitmap(thumbBitmap);
            using var thumbData = thumbImage.Encode(SKEncodedImageFormat.Jpeg, 70); // Lower quality for thumbnails
            var thumbBytes = thumbData.ToArray();

            // Convert to base64 data URLs
            var imageDataBase64 = $"data:image/jpeg;base64,{Convert.ToBase64String(fullBytes)}";
            var thumbnailDataBase64 = $"data:image/jpeg;base64,{Convert.ToBase64String(thumbBytes)}";

            return (imageDataBase64, thumbnailDataBase64, fullBytes.Length);
        }

        /// <summary>
        /// Calculate new dimensions maintaining aspect ratio
        /// </summary>
        private (int width, int height) CalculateDimensions(int origWidth, int origHeight, int maxWidth, int maxHeight)
        {
            if (origWidth <= maxWidth && origHeight <= maxHeight)
                return (origWidth, origHeight);

            var ratioX = (double)maxWidth / origWidth;
            var ratioY = (double)maxHeight / origHeight;
            var ratio = Math.Min(ratioX, ratioY);

            return ((int)(origWidth * ratio), (int)(origHeight * ratio));
        }
    }

    public class UpdatePhotoDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
    }
}
