// Controllers/NotesController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using note_app_be.Data;
using note_app_be.Models;
using note_app_be.Models.Dtos;
using System.Security.Claims;

namespace NoteService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // SEMUA endpoint butuh JWT
public class NotesController : ControllerBase
{
    private readonly NoteDbContext _db;

    public NotesController(NoteDbContext db)
    {
        _db = db;
    }

    // Helper: Ambil UserId dari JWT
    private string GetCurrentUserId()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value; // 'sub' adalah standard JWT claim

        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedAccessException("User ID not found in token");

        return userId;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NoteResponse>>> GetMyNotes()
    {
        var userId = GetCurrentUserId();

        var notes = await _db.Notes
            .AsNoTracking()
            .Include(n => n.Tags)
            .Where(n => n.UserId == userId && !n.IsArchived)
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.ModifiedAt)
            .Select(n => new NoteResponse
            {
                Id = n.Id,
                Title = n.Title,
                Content = n.Content,
                Color = n.Color,
                IsPinned = n.IsPinned,
                IsArchived = n.IsArchived,
                Tags = n.Tags.Select(t => t.Name).ToList(),
                CreatedAt = n.CreatedAt,
                ModifiedAt = n.ModifiedAt
            })
            .ToListAsync();

        return Ok(notes);
    }

    [HttpGet("archived")]
    public async Task<ActionResult<IEnumerable<NoteResponse>>> GetArchivedNotes()
    {
        var userId = GetCurrentUserId();

        var notes = await _db.Notes
            .AsNoTracking()
            .Include(n => n.Tags)
            .Where(n => n.UserId == userId && n.IsArchived)
            .OrderByDescending(n => n.ModifiedAt)
            .Select(n => MapToResponse(n))
            .ToListAsync();

        return Ok(notes);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NoteResponse>> GetNote(Guid id)
    {
        var userId = GetCurrentUserId();

        var note = await _db.Notes
            .Include(n => n.Tags)
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

        if (note == null)
            return NotFound(new { message = "Note not found" });

        return Ok(MapToResponse(note));
    }

    [HttpPost]
    public async Task<ActionResult<NoteResponse>> CreateNote([FromBody] CreateNoteRequest request)
    {
        var userId = GetCurrentUserId();

        var note = new Note
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = request.Title,
            Content = request.Content,
            Color = request.Color ?? "#FFFFFF",
            IsPinned = request.IsPinned,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            Tags = request.Tags?.Select(t => new Tag { Id = Guid.NewGuid(), Name = t }).ToList()
                ?? new List<Tag>()
        };

        _db.Notes.Add(note);
        await _db.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetNote),
            new { id = note.Id },
            MapToResponse(note));
    }

    // Alternative Update - Attach Explicitly
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<NoteResponse>> UpdateNoteSafe(
        Guid id,
        [FromBody] UpdateNoteRequest request)
    {
        var userId = GetCurrentUserId();

        // Cek kepemilikan dulu (lightweight)
        var exists = await _db.Notes
            .AsNoTracking()
            .AnyAsync(n => n.Id == id && n.UserId == userId);

        if (!exists)
            return NotFound(new { message = "Note not found" });

        // Buat entity detached dan attach ke context
        var note = new Note
        {
            Id = id,
            UserId = userId, // WAJIB untuk security
            Title = request.Title ?? "",
            Content = request.Content ?? "",
            Color = request.Color ?? "#FFFFFF",
            IsPinned = request.IsPinned ?? false,
            IsArchived = request.IsArchived ?? false,
            ModifiedAt = DateTime.UtcNow
        };

        // Attach dan mark modified
        _db.Notes.Attach(note);
        _db.Entry(note).State = EntityState.Modified;

        // Handle tags dengan transaction
        using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            // Hapus tags lama
            var oldTags = await _db.Tags.Where(t => t.NoteId == id).ToListAsync();
            _db.Tags.RemoveRange(oldTags);

            // Simpan note dulu
            await _db.SaveChangesAsync();

            // Tambah tags baru
            if (request.Tags != null && request.Tags.Any())
            {
                var newTags = request.Tags.Select(t => new Tag
                {
                    Id = Guid.NewGuid(),
                    Name = t,
                    NoteId = id
                });
                _db.Tags.AddRange(newTags);
                await _db.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            // Reload dengan tags
            var result = await _db.Notes
                .Include(n => n.Tags)
                .FirstAsync(n => n.Id == id);

            return Ok(MapToResponse(result));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "Update failed", error = ex.Message });
        }
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteNote(Guid id)
    {
        var userId = GetCurrentUserId();

        var note = await _db.Notes
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

        if (note == null)
            return NotFound();
        note.IsDeleted = !note.IsDeleted;
        _db.Notes.Update(note);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id:guid}/pin")]
    public async Task<ActionResult<NoteResponse>> TogglePin(Guid id)
    {
        var userId = GetCurrentUserId();

        var note = await _db.Notes
            .Include(n => n.Tags)
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

        if (note == null)
            return NotFound();

        note.IsPinned = !note.IsPinned;
        note.ModifiedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(MapToResponse(note));
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<ActionResult<NoteResponse>> ToggleArchive(Guid id)
    {
        var userId = GetCurrentUserId();

        var note = await _db.Notes
            .Include(n => n.Tags)
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

        if (note == null)
            return NotFound();

        note.IsArchived = !note.IsArchived;
        note.ModifiedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(MapToResponse(note));
    }

    private static NoteResponse MapToResponse(Note note)
    {
        return new NoteResponse
        {
            Id = note.Id,
            Title = note.Title,
            Content = note.Content,
            Color = note.Color,
            IsPinned = note.IsPinned,
            IsArchived = note.IsArchived,
            Tags = note.Tags.Select(t => t.Name).ToList(),
            CreatedAt = note.CreatedAt,
            ModifiedAt = note.ModifiedAt
        };
    }
}