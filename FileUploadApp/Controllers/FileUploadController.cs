using Microsoft.AspNetCore.Mvc;

namespace FileUploadApp.Controllers;

public class FileUploadController : Controller
{
    private readonly IWebHostEnvironment _env;
    private static readonly string[] AllowedExtensions =
        [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
         ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
         ".txt", ".csv", ".zip", ".mp4", ".mp3"];
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB

    public FileUploadController(IWebHostEnvironment env)
    {
        _env = env;
    }

    private string UploadsFolder =>
        Path.Combine(_env.WebRootPath, "uploads");

    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(new { error = "File exceeds the 50 MB limit." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(new { error = $"File type '{ext}' is not allowed." });

        var uploadsDir = UploadsFolder;
        if (!Directory.Exists(uploadsDir))
            Directory.CreateDirectory(uploadsDir);

        // Use a unique file name to avoid collisions while preserving the original name for display.
        // Strip any directory components from the original file name before embedding it.
        var uniqueId = Guid.NewGuid().ToString("N");
        var safeOriginal = Path.GetFileNameWithoutExtension(Path.GetFileName(file.FileName));
        var storedName = $"{uniqueId}_{safeOriginal}{ext}";
        var filePath = Path.Combine(uploadsDir, storedName);

        // Canonical path check: ensure the resolved path is inside the uploads directory.
        var canonicalUploads = Path.GetFullPath(uploadsDir);
        var canonicalFile   = Path.GetFullPath(filePath);
        if (!canonicalFile.StartsWith(canonicalUploads + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Invalid file path." });

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        return Ok(new
        {
            storedName,
            originalName = file.FileName,
            viewUrl = Url.Action("ViewFile", "FileUpload", new { fileName = storedName }),
            deleteUrl = Url.Action("Delete", "FileUpload", new { fileName = storedName })
        });
    }

    [HttpPost]
    public IActionResult Delete(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return BadRequest(new { error = "File name is required." });

        // Prevent path traversal – only allow simple file names.
        if (fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(".."))
            return BadRequest(new { error = "Invalid file name." });

        var filePath = Path.Combine(UploadsFolder, fileName);

        // Canonical path check: ensure the resolved path is inside the uploads directory.
        var canonicalUploads = Path.GetFullPath(UploadsFolder);
        var canonicalFile   = Path.GetFullPath(filePath);
        if (!canonicalFile.StartsWith(canonicalUploads + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Invalid file name." });

        if (!System.IO.File.Exists(filePath))
            return NotFound(new { error = "File not found." });

        System.IO.File.Delete(filePath);
        return Ok(new { message = "File deleted successfully." });
    }

    [HttpGet]
    public IActionResult ViewFile(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return BadRequest();

        if (fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(".."))
            return BadRequest();

        var filePath = Path.Combine(UploadsFolder, fileName);

        // Canonical path check: ensure the resolved path is inside the uploads directory.
        var canonicalUploads = Path.GetFullPath(UploadsFolder);
        var canonicalFile   = Path.GetFullPath(filePath);
        if (!canonicalFile.StartsWith(canonicalUploads + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return BadRequest();

        if (!System.IO.File.Exists(filePath))
            return NotFound();

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var contentType = GetContentType(ext);
        return PhysicalFile(filePath, contentType, enableRangeProcessing: true);
    }

    private static string GetContentType(string ext) => ext switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        ".webp" => "image/webp",
        ".pdf" => "application/pdf",
        ".txt" => "text/plain",
        ".csv" => "text/csv",
        ".mp4" => "video/mp4",
        ".mp3" => "audio/mpeg",
        _ => "application/octet-stream"
    };
}
