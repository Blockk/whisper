// File: WhisperServer/Hubs/ChatHub.cs
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WhisperServer.Data;
using WhisperServer.Models;

namespace WhisperServer.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly AppDbContext _db;
    public ChatHub(AppDbContext db) => _db = db;

    /* ── helpers ────────────────────────────────────────────────────────── */
    private Guid? TryUserId()
    {
        var idClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        return Guid.TryParse(idClaim, out var id) ? id : (Guid?)null;
    }


    Guid RequireUserId()
    {
        var id = TryUserId();
        if (id is null)
            throw new HubException("Unauthenticated connection");
        return id.Value;
    }

    /* ── connection events ─────────────────────────────────────────────── */
public override async Task OnConnectedAsync()
{
    var uid = TryUserId();
    if (uid is null)
    {
        Console.WriteLine("[HUB] ❌ No valid user ID in token; aborting connection.");
        Context.Abort();
        return;
    }

    Console.WriteLine($"[HUB] ✅ Connected user ID: {uid}");

    var groups = await _db.ConversationUsers
                          .Where(cu => cu.UserId == uid)
                          .Select(cu => cu.ConversationId.ToString())
                          .ToListAsync();

    foreach (var g in groups)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, g);
        Console.WriteLine($"[HUB] ➕ Added user {uid} to group {g}");
    }

    var user = await _db.Users.FindAsync(uid);
    if (user != null)
    {
        user.LastSeenAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        Console.WriteLine($"[HUB] 🕒 Updated LastSeenAt for user {uid}");
    }

    await base.OnConnectedAsync();
}

public override async Task OnDisconnectedAsync(Exception? ex)
{
    var uid = TryUserId();
    if (uid is not null)
    {
        var user = await _db.Users.FindAsync(uid);
        if (user != null)
        {
            user.LastSeenAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
            Console.WriteLine($"[HUB] 🕒 Updated LastSeenAt on disconnect for user {uid}");
        }
    }

    Console.WriteLine($"[HUB] ❌ Disconnected user {uid}, reason: {ex?.Message ?? "No exception"}");
    await base.OnDisconnectedAsync(ex);
}


    /* ── messaging ─────────────────────────────────────────────────────── */
    public async Task SendMessage(Guid conversationId, string body)
    {
        var uid = RequireUserId();

        var cu = await _db.ConversationUsers
                          .Include(cu => cu.Conversation)
                          .SingleOrDefaultAsync(x => x.ConversationId == conversationId && x.UserId == uid);
        if (cu is null) return;

        var msg = new Message { ConversationId = conversationId, SenderId = uid, Body = body };
        _db.Messages.Add(msg);

        var others = await _db.ConversationUsers
                              .Where(x => x.ConversationId == conversationId && x.UserId != uid)
                              .ToListAsync();
        foreach (var o in others) o.UnreadCount++;

        cu.Conversation.LastActivityAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        await Clients.Group(conversationId.ToString()).SendAsync("Receive", new
        {
            msg.Id,
            msg.ConversationId,
            msg.SenderId,
            msg.Body,
            msg.SentAt,
            msg.IsDeleted
        });
    }

    public async Task MarkRead(Guid conversationId)
    {
        var uid = RequireUserId();
        var cu = await _db.ConversationUsers.SingleOrDefaultAsync(x => x.ConversationId == conversationId && x.UserId == uid);
        if (cu is null) return;
        cu.LastReadAt = DateTimeOffset.UtcNow;
        cu.UnreadCount = 0;
        await _db.SaveChangesAsync();
    }

    public async Task EditMessage(Guid messageId, string newBody)
    {
        var uid = RequireUserId();
        var msg = await _db.Messages.FindAsync(messageId);
        if (msg is null || msg.SenderId != uid || msg.IsDeleted) return;

        msg.Body = newBody;
        msg.EditedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        await Clients.Group(msg.ConversationId.ToString()).SendAsync("Edit", new
        {
            msg.Id,
            msg.Body,
            msg.EditedAt
        });
    }

    public async Task DeleteMessage(Guid messageId)
    {
        var uid = RequireUserId();
        var msg = await _db.Messages.FindAsync(messageId);
        if (msg is null || (msg.SenderId != uid && !await IsAdmin(msg.ConversationId, uid))) return;

        msg.IsDeleted = true;
        await _db.SaveChangesAsync();
        await Clients.Group(msg.ConversationId.ToString()).SendAsync("Delete", new { msg.Id });
    }

    public async Task React(Guid messageId, string emote)
    {
        var uid = RequireUserId();
        var msg = await _db.Messages.Include(m => m.Reactions).SingleOrDefaultAsync(m => m.Id == messageId);
        if (msg is null || msg.IsDeleted) return;

        var existing = msg.Reactions.SingleOrDefault(r => r.UserId == uid && r.Emote == emote);
        if (existing is null)
            msg.Reactions.Add(new Reaction { MessageId = messageId, UserId = uid, Emote = emote });
        else
            _db.Reactions.Remove(existing);

        await _db.SaveChangesAsync();
        await Clients.Group(msg.ConversationId.ToString()).SendAsync("React", new
        {
            messageId,
            userId = uid,
            emote
        });
    }

    public async Task Typing(Guid conversationId, bool isTyping)
    {
        var uid = RequireUserId();
        if (!await _db.ConversationUsers.AnyAsync(cu => cu.ConversationId == conversationId && cu.UserId == uid))
            return;
        await Clients.Group(conversationId.ToString()).SendAsync("Typing", new { conversationId, userId = uid, isTyping });
    }

    /* ── group join/leave ──────────────────────────────────────────────── */
    public async Task JoinConversation(Guid conversationId)
    {
        var uid = RequireUserId();
        if (await _db.ConversationUsers.AnyAsync(cu => cu.ConversationId == conversationId && cu.UserId == uid))
            await Groups.AddToGroupAsync(Context.ConnectionId, conversationId.ToString());
    }

    public Task LeaveConversation(Guid conversationId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationId.ToString());

    /* ── admin helpers ─────────────────────────────────────────────────── */
    public async Task AddUser(Guid conversationId, Guid targetUserId)
    {
        var uid = RequireUserId();
        if (!await IsAdmin(conversationId, uid)) return;
        if (await _db.ConversationUsers.AnyAsync(cu => cu.ConversationId == conversationId && cu.UserId == targetUserId))
            return;

        _db.ConversationUsers.Add(new ConversationUser { ConversationId = conversationId, UserId = targetUserId });
        await _db.SaveChangesAsync();

        await Clients.Group(conversationId.ToString()).SendAsync("UserAdded", new { conversationId, targetUserId });
    }

    public async Task RemoveUser(Guid conversationId, Guid targetUserId)
    {
        var uid = RequireUserId();
        if (!await IsAdmin(conversationId, uid) || targetUserId == uid) return;

        var cu = await _db.ConversationUsers.SingleOrDefaultAsync(c => c.ConversationId == conversationId && c.UserId == targetUserId);
        if (cu is null) return;

        _db.ConversationUsers.Remove(cu);
        await _db.SaveChangesAsync();

        await Clients.Group(conversationId.ToString()).SendAsync("UserRemoved", new { conversationId, targetUserId });
    }

    async Task<bool> IsAdmin(Guid conversationId, Guid userId) =>
        await _db.ConversationUsers.AnyAsync(cu => cu.ConversationId == conversationId && cu.UserId == userId && cu.IsAdmin);
}
