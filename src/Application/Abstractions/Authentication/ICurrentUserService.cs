namespace Application.Abstractions.Authentication;

/// <summary>
/// Null-safe accessor for the current user's identity.
/// Returns null for system-initiated operations (e.g. Worker background services).
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
}
