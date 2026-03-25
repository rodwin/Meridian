using SharedKernel;

namespace Domain.Todos;

public sealed class TodoItem : Entity, IAuditableEntity
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTimeOffset? DueDate { get; private set; }
    public List<string> Labels { get; private set; } = [];
    public bool IsCompleted { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public Priority Priority { get; private set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public static TodoItem Create(
        Guid userId,
        string description,
        Priority priority,
        DateTimeOffset? dueDate,
        List<string> labels)
    {
        var todoItem = new TodoItem
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Description = description,
            Priority = priority,
            DueDate = dueDate,
            Labels = labels,
            IsCompleted = false
        };

        todoItem.Raise(new TodoItemCreatedDomainEvent(todoItem.Id));

        return todoItem;
    }

    public Result Complete(TimeProvider timeProvider)
    {
        if (IsCompleted)
        {
            return Result.Failure(TodoItemErrors.AlreadyCompleted(Id));
        }

        IsCompleted = true;
        CompletedAt = timeProvider.GetUtcNow();

        Raise(new TodoItemCompletedDomainEvent(Id));

        return Result.Success();
    }

    public void UpdateDescription(string description)
    {
        Description = description;
    }
}
