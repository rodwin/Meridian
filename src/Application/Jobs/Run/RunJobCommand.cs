using Application.Abstractions.Messaging;

namespace Application.Jobs.Run;

public sealed record RunJobCommand(Guid JobId, Guid ScheduleId) : ICommand;
