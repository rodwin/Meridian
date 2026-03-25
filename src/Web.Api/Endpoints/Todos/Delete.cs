using Application.Abstractions.Messaging;
using Application.Todos.Delete;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Todos;

internal sealed class Delete : IEndpoint
{
    public sealed class DeleteTodoRequest
    {
        public byte[] RowVersion { get; set; } = [];
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("todos/{id:guid}", async (
            Guid id,
            [FromBody] DeleteTodoRequest request,
            ICommandHandler<DeleteTodoCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new DeleteTodoCommand(id, request.RowVersion);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Todos)
        .RequireAuthorization();
    }
}
