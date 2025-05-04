// This file is part of WhisperServer
// a server for the Whisper protocol.
//Services/JwtOptions.cs
namespace WhisperServer.Services;

/// <summary>Bound to the "Jwt" section of appsettings.</summary>
public class JwtOptions
{
    public string Key           { get; set; } = string.Empty;
    public string Issuer        { get; set; } = string.Empty;
    public string Audience      { get; set; } = string.Empty;
    public int    ExpireMinutes { get; set; } = 60;
}
