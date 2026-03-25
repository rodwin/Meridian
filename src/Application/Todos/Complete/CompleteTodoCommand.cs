using Application.Abstractions.Messaging;

namespace Application.Todos.Complete;

public sealed record CompleteTodoCommand(Guid TodoItemId, byte[] RowVersion) : ICommand;
