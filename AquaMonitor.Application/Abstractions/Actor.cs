namespace AquaMonitor.Application.Abstractions;

/// <summary>Trusted identity supplied by the host after authentication.</summary>
public sealed record Actor
{
    public int UserId { get; }
    public bool IsAdmin { get; }
    public Actor(int userId, bool isAdmin)
    {
        if (userId <= 0) throw new ApplicationFailure(401, "Credenciais invalidas.");
        UserId = userId;
        IsAdmin = isAdmin;
    }
    public void RequireAdministrator()
    {
        if (!IsAdmin) throw new ApplicationFailure(403);
    }
    public void RequireOwnership(int ownerId)
    {
        if (!IsAdmin && ownerId != UserId) throw new ApplicationFailure(403);
    }
}
