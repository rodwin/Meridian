using Application.Abstractions.Messaging;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Abstractions.Behaviors;

internal static partial class LoggingDecorator
{
    internal sealed partial class CommandHandler<TCommand, TResponse>(
        ICommandHandler<TCommand, TResponse> innerHandler,
        ILogger<CommandHandler<TCommand, TResponse>> logger)
        : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
        {
            string commandName = typeof(TCommand).Name;

            LogProcessing(logger, commandName);

            Result<TResponse> result = await innerHandler.Handle(command, cancellationToken);

            if (result.IsSuccess)
            {
                LogCompleted(logger, commandName);
            }
            else
            {
                var data = new Dictionary<string, object>
                {
                    ["Error"] = result.Error
                };
                using (logger.BeginScope(data))
                {
                    LogCompletedWithError(logger, commandName);
                }
            }

            return result;
        }

        [LoggerMessage(Level = LogLevel.Information, Message = "Processing command {Command}")]
        private static partial void LogProcessing(ILogger logger, string command);

        [LoggerMessage(Level = LogLevel.Information, Message = "Completed command {Command}")]
        private static partial void LogCompleted(ILogger logger, string command);

        [LoggerMessage(Level = LogLevel.Error, Message = "Completed command {Command} with error")]
        private static partial void LogCompletedWithError(ILogger logger, string command);
    }

    internal sealed partial class CommandBaseHandler<TCommand>(
        ICommandHandler<TCommand> innerHandler,
        ILogger<CommandBaseHandler<TCommand>> logger)
        : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public async Task<Result> Handle(TCommand command, CancellationToken cancellationToken)
        {
            string commandName = typeof(TCommand).Name;

            LogProcessing(logger, commandName);

            Result result = await innerHandler.Handle(command, cancellationToken);

            if (result.IsSuccess)
            {
                LogCompleted(logger, commandName);
            }
            else
            {
                var data = new Dictionary<string, object>
                {
                    ["Error"] = result.Error
                };
                using (logger.BeginScope(data))
                {
                    LogCompletedWithError(logger, commandName);
                }
            }

            return result;
        }

        [LoggerMessage(Level = LogLevel.Information, Message = "Processing command {Command}")]
        private static partial void LogProcessing(ILogger logger, string command);

        [LoggerMessage(Level = LogLevel.Information, Message = "Completed command {Command}")]
        private static partial void LogCompleted(ILogger logger, string command);

        [LoggerMessage(Level = LogLevel.Error, Message = "Completed command {Command} with error")]
        private static partial void LogCompletedWithError(ILogger logger, string command);
    }

    internal sealed partial class QueryHandler<TQuery, TResponse>(
        IQueryHandler<TQuery, TResponse> innerHandler,
        ILogger<QueryHandler<TQuery, TResponse>> logger)
        : IQueryHandler<TQuery, TResponse>
        where TQuery : IQuery<TResponse>
    {
        public async Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken)
        {
            string queryName = typeof(TQuery).Name;

            LogProcessing(logger, queryName);

            Result<TResponse> result = await innerHandler.Handle(query, cancellationToken);

            if (result.IsSuccess)
            {
                LogCompleted(logger, queryName);
            }
            else
            {
                var data = new Dictionary<string, object>
                {
                    ["Error"] = result.Error
                };
                using (logger.BeginScope(data))
                {
                    LogCompletedWithError(logger, queryName);
                }
            }

            return result;
        }

        [LoggerMessage(Level = LogLevel.Information, Message = "Processing query {Query}")]
        private static partial void LogProcessing(ILogger logger, string query);

        [LoggerMessage(Level = LogLevel.Information, Message = "Completed query {Query}")]
        private static partial void LogCompleted(ILogger logger, string query);

        [LoggerMessage(Level = LogLevel.Error, Message = "Completed query {Query} with error")]
        private static partial void LogCompletedWithError(ILogger logger, string query);
    }
}
