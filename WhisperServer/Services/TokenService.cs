// File: WhisperServer/Services/TokenService.cs
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WhisperServer.Models;

namespace WhisperServer.Services;

public class TokenService
{
    private readonly JwtOptions _opt;
    private readonly SymmetricSecurityKey  _signKey;
    private readonly SigningCredentials    _creds;
    private readonly JwtSecurityTokenHandler _handler = new();

    public TokenService(IOptions<JwtOptions> opt)
    {
        _opt     = opt.Value;
        _signKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key));
        _creds   = new SigningCredentials(_signKey, SecurityAlgorithms.HmacSha256);
    }

    /* ── access token ──────────────────────────────────────────────────── */
    public string GenerateAccessToken(User user)
    {
        // 1) **sub** claim is REQUIRED – server uses it to build Guid userId
        // 2) username is handy on the client but optional for auth
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
        };

        var token = new JwtSecurityToken(
            issuer:            _opt.Issuer,
            audience:          _opt.Audience,
            claims:            claims,
            expires:           DateTime.UtcNow.AddMinutes(_opt.ExpireMinutes),
            signingCredentials:_creds);

        return _handler.WriteToken(token);
    }

    /* ── refresh token (opaque) ────────────────────────────────────────── */
    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    /* ── validation helper (for refresh‑flow or SignalR tests) ─────────── */
    public ClaimsPrincipal? ValidateToken(string token, bool ignoreExpiry = false)
    {
        var parms = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = _opt.Issuer,
            ValidAudience            = _opt.Audience,
            IssuerSigningKey         = _signKey,
            ClockSkew                = TimeSpan.FromMinutes(5),
            ValidateLifetime         = !ignoreExpiry
        };

        try   { return _handler.ValidateToken(token, parms, out _); }
        catch { return null; }
    }

    /* convenience pair --------------------------------------------------- */
    public (string access, string refresh) GeneratePair(User u) =>
        (GenerateAccessToken(u), GenerateRefreshToken());
}
