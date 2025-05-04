// File: WhisperServer/Data/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using WhisperServer.Models;

namespace WhisperServer.Data;

public class AppDbContext(DbContextOptions<AppDbContext> opts) : DbContext(opts)
{
    public DbSet<User>              Users              => Set<User>();
    public DbSet<Contact>           Contacts           => Set<Contact>();
    public DbSet<Conversation>      Conversations      => Set<Conversation>();
    public DbSet<ConversationUser>  ConversationUsers  => Set<ConversationUser>();
    public DbSet<Message>           Messages           => Set<Message>();
    public DbSet<Attachment>        Attachments        => Set<Attachment>();
    public DbSet<Reaction>          Reactions          => Set<Reaction>();
    public DbSet<PushToken>         PushTokens         => Set<PushToken>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ConversationUser>().HasKey(cu => new { cu.ConversationId, cu.UserId });

        b.Entity<Contact>()
            .HasOne(c => c.Owner).WithMany(u => u.Contacts)
            .HasForeignKey(c => c.OwnerId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Contact>()
            .HasOne(c => c.Target).WithMany()
            .HasForeignKey(c => c.TargetId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Message>()
            .Property(m => m.SentAt)
            .HasConversion(v => v.UtcDateTime, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        b.Entity<Message>()
            .HasOne(m => m.Conversation).WithMany(c => c.Messages)
            .HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Attachment>()
            .HasOne(a => a.Message).WithMany(m => m.Attachments)
            .HasForeignKey(a => a.MessageId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Reaction>()
            .HasOne(r => r.Message).WithMany(m => m.Reactions)
            .HasForeignKey(r => r.MessageId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Reaction>()
            .HasOne(r => r.User).WithMany(u => u.Reactions)
            .HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Reaction>()
            .HasIndex(r => new { r.MessageId, r.UserId, r.Emote }).IsUnique();

        b.Entity<PushToken>()
            .HasOne(p => p.User).WithMany(u => u.PushTokens)
            .HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<PushToken>()
            .HasIndex(p => p.Token).IsUnique();
    }
}
