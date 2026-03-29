// Models/Dtos.cs
namespace note_app_be.Models.Dtos;

// Request DTOs
public class CreateNoteRequest
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Color { get; set; }
    public List<string>? Tags { get; set; }
    public bool IsPinned { get; set; }
}

public class UpdateNoteRequest
{
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? Color { get; set; }
    public List<string>? Tags { get; set; }
    public bool? IsPinned { get; set; }
    public bool? IsArchived { get; set; }
}

// Response DTOs
public class NoteResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public bool IsArchived { get; set; }
    public List<string> Tags { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
}