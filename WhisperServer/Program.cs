// File: WhisperServer/Program.cs
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WhisperServer.Data;
using WhisperServer.Hubs;
using WhisperServer.Models;
using WhisperServer.Services;

var builder = WebApplication.CreateBuilder(args);

// avoid default claim mapping so "sub" stays as-is
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

/* ──────────────── SERVICES ───────────────────────────────────────────── */
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Sqlite")));

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<TokenService>();

// 1) CORS policy to let your SPA send Authorization header
builder.Services.AddCors(o =>
    o.AddPolicy("Dev", p => p
        .SetIsOriginAllowed(_ => true)
        .AllowAnyHeader()    // ← must allow Authorization
        .AllowAnyMethod()
        .AllowCredentials()));

// 2) Authentication + JWT‐Bearer
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()!;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwt.Issuer,
            ValidAudience            = jwt.Audience,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew                = TimeSpan.Zero
        };

        o.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                Console.WriteLine($"[JWT] Authentication failed: {ctx.Exception.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = ctx =>
            {
                var sub = ctx.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                Console.WriteLine($"[JWT] Token validated for user {sub}");
                return Task.CompletedTask;
            },
            OnMessageReceived = ctx =>
            {
                // allow SignalR to pick up token from ?access_token=…
                var accessToken = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    ctx.HttpContext.Request.Path.StartsWithSegments("/hub"))
                {
                    ctx.Token = accessToken;
                }
                return Task.CompletedTask;
            },
            OnChallenge = ctx =>
            {
                // fires whenever a 401 is about to be returned (missing or invalid token)
                Console.WriteLine("[JWT] Challenge triggered – no token or invalid");
                return Task.CompletedTask;
            }
        };
    });

// 3) Authorization
builder.Services.AddAuthorization();

// 4) SignalR, Swagger, etc.
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


/* ──────────────── PIPELINE ───────────────────────────────────────────── */
var app = builder.Build();

// convert any thrown UnauthorizedAccessException into a 401
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (UnauthorizedAccessException)
    {
        ctx.Response.StatusCode = 401;
    }
});

// swagger UI
app.UseSwagger();
app.UseSwaggerUI();

// **order matters**:
app.UseCors("Dev");           // must come before Auth so browser can send header
app.UseAuthentication();      // validate incoming bearer tokens
app.UseAuthorization();       // enforce [Authorize] / RequireAuthorization()

app.Use(async (ctx, next) =>
{
    var authHeader = ctx.Request.Headers["Authorization"].FirstOrDefault() ?? "<missing>";
    Console.WriteLine($"[DBG] {ctx.Request.Method} {ctx.Request.Path}  Authorization: {authHeader}");
    await next();
});


/* safe helper ------------------------------------------------------------- */
Guid UserId(HttpContext ctx)
{
    var id = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
          ?? ctx.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

    if (id == null)
        throw new Exception("No user id claim");

    return Guid.Parse(id);
}

/* ─────────────────────  AUTH  ─────────────────────────────────────────── */
app.MapPost("/auth/register", async (AppDbContext db, TokenService tokens, RegisterDto dto) =>
{
    if (await db.Users.AnyAsync(u => u.Username == dto.Username))
        return Results.BadRequest("username taken");

    if (dto.LoginPin == dto.DeletePin)                       /* guard */
        return Results.BadRequest("Pins must differ");

    var u = new User
    {
        Username      = dto.Username,
        DisplayName   = dto.Username,
        PasswordHash  = BCrypt.Net.BCrypt.HashPassword(dto.Password),
        LoginPinHash  = BCrypt.Net.BCrypt.HashPassword(dto.LoginPin),
        DeletePinHash = BCrypt.Net.BCrypt.HashPassword(dto.DeletePin)
    };
    db.Users.Add(u);
    await db.SaveChangesAsync();

    return Results.Ok(new { token = tokens.GenerateAccessToken(u), id = u.Id, username = u.Username });
});


app.MapPost("/auth/login", async (AppDbContext db, TokenService tokens, LoginDto dto) =>
{
    Console.WriteLine($"[LOGIN] Attempt for Username={dto.Username}, LoginPin={dto.LoginPin}");

    var u = await db.Users.SingleOrDefaultAsync(x => x.Username == dto.Username);
    if (u is null)
    {
        Console.WriteLine("[LOGIN] User not found");
        return Results.Unauthorized();
    }

    var pwOk  = BCrypt.Net.BCrypt.Verify(dto.Password,  u.PasswordHash);
    var pinOk = BCrypt.Net.BCrypt.Verify(dto.LoginPin, u.LoginPinHash);

    if (!(pwOk && pinOk))
    {
        Console.WriteLine($"[LOGIN] Invalid credentials (pwOk={pwOk}, pinOk={pinOk})");
        return Results.Unauthorized();
    }


    var jwt = tokens.GenerateAccessToken(u);
    Console.WriteLine($"[LOGIN] Success for {u.Username}; token starts with \"{jwt.Substring(0, 10)}...\"");

    return Results.Ok(new
    {
        token    = jwt,
        id       = u.Id,
        username = u.Username
    });
});


app.MapPost("/auth/delete", async (AppDbContext db, DeleteDto dto) =>
{
    var u = await db.Users
                    .Include(x => x.Contacts)
                    .Include(x => x.ConversationUsers).ThenInclude(cu => cu.Conversation)
                    .SingleOrDefaultAsync(x => x.Username == dto.Username);
    if (u is null) return Results.Unauthorized();

    var pwOk  = BCrypt.Net.BCrypt.Verify(dto.Password,  u.PasswordHash);
    var pinOk = BCrypt.Net.BCrypt.Verify(dto.DeletePin, u.DeletePinHash);

    if (!pwOk || !pinOk) return Results.Unauthorized();

    db.Remove(u);
    await db.SaveChangesAsync();
    return Results.Ok();
});

/* ───────────────────  CONTACTS  ───────────────────────────────────────── */
app.MapGet("/contacts", async (AppDbContext db, HttpContext ctx) =>
{
    var meId = UserId(ctx);
    var list = await db.Contacts
        .Where(c => c.OwnerId == meId && !c.IsBlocked)
        .Include(c => c.Target)
        .Select(c => new { id = c.Target.Id, username = c.Target.Username, avatar = c.Target.AvatarUrl })
        .ToListAsync();
    return Results.Ok(list);
}).RequireAuthorization();

app.MapPost("/contacts/{targetUsername}", async (AppDbContext db, HttpContext ctx, string targetUsername) =>
{
    var meId = UserId(ctx);
    var other = await db.Users.SingleOrDefaultAsync(u => u.Username == targetUsername);
    if (other is null || other.Id == meId) return Results.BadRequest();

    if (!await db.Contacts.AnyAsync(c => c.OwnerId == meId && c.TargetId == other.Id))
        db.Contacts.Add(new Contact { OwnerId = meId, TargetId = other.Id });

    await db.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization();

app.MapDelete("/contacts/{targetUsername}", async (AppDbContext db, HttpContext ctx, string targetUsername) =>
{
    var meId = UserId(ctx);
    var del = await db.Contacts.SingleOrDefaultAsync(c => c.OwnerId == meId && c.Target.Username == targetUsername);
    if (del is null) return Results.NotFound();
    db.Remove(del);
    await db.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization();

/* ─────────────────  USER SEARCH  ───────────────────────────────────────── */
app.MapGet("/users/search", async (AppDbContext db, string q) =>
{
    if (string.IsNullOrWhiteSpace(q) || q.Length < 2) return Results.BadRequest();
    var res = await db.Users
        .Where(u => EF.Functions.Like(u.Username, $"%{q}%"))
        .OrderBy(u => u.Username)
        .Take(10)
        .Select(u => new { u.Id, u.Username, u.AvatarUrl })
        .ToListAsync();
    return Results.Ok(res);
}).RequireAuthorization();


/* ────────────────  CONVERSATIONS  ───────────────────────────────────────── */
app.MapGet("/conversations", async (AppDbContext db, HttpContext ctx) =>
{
    try
    {
        var meId = UserId(ctx);
        Console.WriteLine($"[CONV] Incoming request for userId: {meId}");

        var userExists = await db.Users.AnyAsync(u => u.Id == meId);
        Console.WriteLine($"[CONV] User exists in DB: {userExists}");

        if (!userExists)
        {
            Console.WriteLine($"[CONV] No matching user in DB for ID {meId}, returning 401");
            return Results.Unauthorized();
        }

        var raw = await db.ConversationUsers
            .Where(cu => cu.UserId == meId)
            .Include(cu => cu.Conversation)
                .ThenInclude(c => c.Participants).ThenInclude(cu => cu.User)
            .Include(cu => cu.Conversation.Messages.OrderByDescending(m => m.SentAt).Take(1))
            .Select(cu => new
            {
                cu.ConversationId,
                Title = cu.Conversation.IsGroup
                    ? cu.Conversation.Title
                    : cu.Conversation.Participants
                          .Where(p => p.UserId != meId)
                          .Select(p => p.User.Username)
                          .FirstOrDefault(),
                LastBody = cu.Conversation.Messages
                    .OrderByDescending(m => m.SentAt)
                    .Select(m => m.Body)
                    .FirstOrDefault(),
                LastSent = cu.Conversation.Messages
                    .OrderByDescending(m => m.SentAt)
                    .Select(m => (DateTimeOffset?)m.SentAt)
                    .FirstOrDefault(),
                cu.UnreadCount
            })
            .ToListAsync();

        Console.WriteLine($"[CONV] Retrieved {raw.Count} conversations");

        var convs = raw
            .OrderByDescending(r => r.LastSent ?? DateTimeOffset.MinValue)
            .Select(r => new
            {
                conversationId = r.ConversationId,
                title = r.Title,
                last = r.LastBody,
                lastSent = r.LastSent,
                unread = r.UnreadCount
            });

        return Results.Ok(convs);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[CONV ERROR] {ex.GetType().Name}: {ex.Message}");
        return Results.Unauthorized();
    }
}).RequireAuthorization();


app.MapPost("/conversations", async (AppDbContext db, HttpContext ctx, CreateConversationDto dto) =>
{
    try
    {
        if (dto == null)
        {
            Console.WriteLine("[CREATE] ❌ No DTO received in request body.");
            return Results.BadRequest("Request body is missing or invalid.");
        }

        Console.WriteLine($"[CREATE] 🔔 Received request: targetUsername = '{dto.TargetUsername}'");

        if (string.IsNullOrWhiteSpace(dto.TargetUsername))
        {
            Console.WriteLine("[CREATE] ❌ targetUsername is missing or empty.");
            return Results.BadRequest("Target username is required.");
        }

        var meId = UserId(ctx);
        Console.WriteLine($"[CREATE] 👤 Current user ID: {meId}");

        var other = await db.Users.SingleOrDefaultAsync(u => u.Username == dto.TargetUsername);
        if (other is null)
        {
            Console.WriteLine($"[CREATE] ❌ Target user '{dto.TargetUsername}' not found in DB.");
            return Results.BadRequest("Target user not found.");
        }

        if (other.Id == meId)
        {
            Console.WriteLine($"[CREATE] ❌ User tried to create a conversation with themselves: {meId}");
            return Results.BadRequest("Cannot create conversation with yourself.");
        }

        Console.WriteLine($"[CREATE] ✅ Target user found: {other.Id} ({other.Username})");

        var existing = await db.ConversationUsers
            .Where(cu => cu.UserId == meId).Select(cu => cu.ConversationId)
            .Intersect(db.ConversationUsers.Where(cu => cu.UserId == other.Id).Select(cu => cu.ConversationId))
            .FirstOrDefaultAsync();

        if (existing != Guid.Empty)
        {
            Console.WriteLine($"[CREATE] 🔄 Conversation already exists between {meId} and {other.Id}: {existing}");
            return Results.Ok(new { conversationId = existing });
        }

        var conv = new Conversation { IsGroup = false };
        conv.Participants.Add(new ConversationUser { UserId = meId });
        conv.Participants.Add(new ConversationUser { UserId = other.Id });

        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        Console.WriteLine($"[CREATE] 🎉 New conversation created with ID: {conv.Id}");

        return Results.Ok(new { conversationId = conv.Id });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[CREATE] 💥 Exception: {ex.GetType().Name} - {ex.Message}");
        return Results.StatusCode(500);
    }
}).RequireAuthorization();


/* ───────────────────  MESSAGES  ───────────────────────────────────────── */
app.MapGet("/messages/{conversationId:guid}", async (AppDbContext db, HttpContext ctx, Guid conversationId) =>
{
    var meId = UserId(ctx);
    if (!await db.ConversationUsers.AnyAsync(cu => cu.ConversationId == conversationId && cu.UserId == meId))
        return Results.Forbid();

    var msgs = await db.Messages
        .Where(m => m.ConversationId == conversationId)
        .Include(m => m.Sender)
        .OrderBy(m => m.SentAt)
        .Select(m => new
        {
            m.Id,
            m.ConversationId,
            m.SenderId,
            sender = m.Sender.Username,
            m.Body,
            m.SentAt,
            m.IsDeleted
        })
        .ToListAsync();

    return Results.Ok(msgs);
}).RequireAuthorization();

/* ────────────────  PUSH TOKENS  ───────────────────────────────────────── */
app.MapPost("/push/register", async (AppDbContext db, HttpContext ctx, PushRegisterDto dto) =>
{
    var uid = UserId(ctx);
    if (!await db.PushTokens.AnyAsync(p => p.Token == dto.Token))
        db.PushTokens.Add(new PushToken { UserId = uid, Token = dto.Token, Platform = dto.Platform });
    await db.SaveChangesAsync();
    return Results.Ok();
}).RequireAuthorization();

/* hub */
app.MapHub<ChatHub>("/hub/chat").RequireAuthorization();

/* db migrate */
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();

/* ────────────────  DTOs  ─────────────────────────────────────────────── */
public record RegisterDto(string Username, string Password, string LoginPin, string DeletePin);
public record LoginDto   (string Username, string Password, string LoginPin);
public record DeleteDto  (string Username, string Password, string DeletePin);
public record CreateConversationDto
{
    public string? TargetUsername { get; init; }
}

public record PushRegisterDto     (string Token, string Platform);
