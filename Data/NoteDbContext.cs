// Data/NoteDbContext.cs
using Azure;
using Microsoft.EntityFrameworkCore;
using note_app_be.Models;

namespace note_app_be.Data;

public class NoteDbContext : DbContext
{
    public NoteDbContext(DbContextOptions<NoteDbContext> options) : base(options) { }

    public DbSet<Note> Notes { get; set; }
    public DbSet<Tag> Tags { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Note>(entity =>
        {
            entity.HasKey(n => n.Id);
            entity.Property(n => n.Title).HasMaxLength(200).IsRequired();
            entity.Property(n => n.Content).HasMaxLength(5000);
            entity.Property(n => n.UserId).HasMaxLength(100).IsRequired();
            entity.Property(n => n.Color).HasMaxLength(7).HasDefaultValue("#FFFFFF");
            entity.Property(n => n.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            // Index untuk query cepat by user
            entity.HasIndex(n => n.UserId);
            entity.HasIndex(n => new { n.UserId, n.IsArchived });
            entity.Property(d => d.IsDeleted).HasDefaultValue(false);
            entity.HasQueryFilter(e => e.IsDeleted == false);
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Name).HasMaxLength(50).IsRequired();

            entity.HasOne(t => t.Note)
                  .WithMany(n => n.Tags)
                  .HasForeignKey(t => t.NoteId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}