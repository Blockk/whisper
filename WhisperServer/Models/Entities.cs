// File: WhisperServer/Models/Entities.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace WhisperServer.Models;

[Index(nameof(Username), IsUnique = true)]
public class User
{
    public Guid Id { get; set; }
    [Required, MaxLength(32)] public string Username { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string DisplayName { get; set; } = string.Empty;
    [MaxLength(256)] public string? AvatarUrl { get; set; }
    [Required] public string PasswordHash  { get; set; } = string.Empty;
    [Required] public string LoginPinHash  { get; set; } = string.Empty;
    [Required] public string DeletePinHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastSeenAt { get; set; }
    public ICollection<Contact>          Contacts          { get; } = new List<Contact>();
    public ICollection<ConversationUser> ConversationUsers { get; } = new List<ConversationUser>();
    public ICollection<Message>          MessagesSent      { get; } = new List<Message>();
    public ICollection<PushToken>        PushTokens        { get; } = new List<PushToken>();
    public ICollection<Reaction>         Reactions         { get; } = new List<Reaction>();
}

public class Contact
{
    public Guid Id { get; set; }
    public Guid OwnerId  { get; set; }
    public User Owner    { get; set; } = null!;
    public Guid TargetId { get; set; }
    public User Target   { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsBlocked { get; set; }
}

public class Conversation
{
    public Guid Id { get; set; }
    [MaxLength(64)] public string? Title { get; set; }
    public bool IsGroup { get; set; }
    public DateTimeOffset CreatedAt     { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastActivityAt{ get; set; } = DateTimeOffset.UtcNow;
    public ICollection<ConversationUser> Participants { get; } = new List<ConversationUser>();
    public ICollection<Message>          Messages     { get; } = new List<Message>();
}

public class ConversationUser
{
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User   { get; set; } = null!;
    public bool IsAdmin { get; set; }
    public DateTimeOffset JoinedAt   { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastReadAt{ get; set; }
    public int UnreadCount { get; set; }
}

public class Message
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public Guid SenderId { get; set; }
    public User Sender   { get; set; } = null!;
    [MaxLength(4000)] public string Body { get; set; } = string.Empty;
    public DateTimeOffset SentAt   { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EditedAt{ get; set; }
    public bool IsDeleted { get; set; }
    public ICollection<Attachment> Attachments { get; } = new List<Attachment>();
    public ICollection<Reaction>   Reactions   { get; } = new List<Reaction>();
}

public class Attachment
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public Message Message { get; set; } = null!;
    [MaxLength(256)] public string FileName  { get; set; } = string.Empty;
    [MaxLength(64)]  public string MediaType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    [MaxLength(512)] public string Url { get; set; } = string.Empty;
}

public class Reaction
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public Message Message { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User   { get; set; } = null!;
    [MaxLength(16)] public string Emote { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class PushToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User   { get; set; } = null!;
    [MaxLength(256)] public string Token { get; set; } = string.Empty;
    [MaxLength(32)]  public string Platform { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt  { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUsedAt{ get; set; }
}
