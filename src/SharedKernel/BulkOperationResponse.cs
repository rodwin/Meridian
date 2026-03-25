namespace SharedKernel;

public sealed record BulkOperationResponse<TSuccess>(
    IReadOnlyList<TSuccess> Succeeded,
    IReadOnlyList<BulkItemFailure> Failed);

public sealed record BulkItemFailure(
    int Index,
    Guid? Id,
    string? Name,
    IReadOnlyList<string> Errors);
