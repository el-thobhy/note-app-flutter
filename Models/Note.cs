// Models/Note.cs
namespace note_app_be.Models;

public class Note
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;  // From JWT 'sub' claim
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Color { get; set; } = "#FFFFFF";
    public bool IsPinned { get; set; }
    public bool IsArchived { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }

    public List<Tag> Tags { get; set; } = new();
}

public class Tag
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid NoteId { get; set; }
    public Note Note { get; set; } = null!;
}