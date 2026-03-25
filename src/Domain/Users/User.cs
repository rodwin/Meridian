using SharedKernel;

namespace Domain.Users;

public sealed class User : Entity, IAuditableEntity
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public static User Create(string email, string firstName, string lastName, string passwordHash)
    {
        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            PasswordHash = passwordHash
        };

        user.Raise(new UserRegisteredDomainEvent(user.Id));

        return user;
    }
}
