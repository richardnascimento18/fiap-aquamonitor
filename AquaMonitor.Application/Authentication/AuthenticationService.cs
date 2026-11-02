using AquaMonitor.Application.Abstractions;
using AquaMonitor.Application.Contracts;

namespace AquaMonitor.Application.Authentication;

public sealed class AuthenticationService(IWorkSessionFactory sessions, IPasswordHasher passwords, ITokenService tokens, TimeProvider time)
{
    public AuthResponseVM Register(RegisterVM request)
    {
        RequestValidation.Validate(request);
        var email = request.Email.ToLowerInvariant();
        var hash = passwords.Hash(request.Password);
        using var session = sessions.Open();
        var id = session.InTransaction(() =>
        {
            if (session.Users.FindByEmail(email) is not null) throw new ApplicationFailure(409, "E-mail ja cadastrado.");
            return session.Users.Insert(request.Name, email, hash, time.GetUtcNow().UtcDateTime);
        });
        return new AuthResponseVM { Token = tokens.GenerateToken(id, request.Email, request.Name, "User"), Name = request.Name, Email = email, Role = "User", ExpiresAt = tokens.GetExpiration() };
    }
    public AuthResponseVM Login(LoginVM request)
    {
        RequestValidation.Validate(request);
        using var session = sessions.Open();
        var user = session.Users.FindByEmail(request.Email.ToLowerInvariant());
        if (user is null || !passwords.Verify(request.Password, user.PasswordHash))
            throw new ApplicationFailure(401, "Credenciais invalidas.");
        return new AuthResponseVM { Token = tokens.GenerateToken(user.Id, user.Email, user.Name, user.Role), Name = user.Name, Email = user.Email, Role = user.Role, ExpiresAt = tokens.GetExpiration() };
    }
}
