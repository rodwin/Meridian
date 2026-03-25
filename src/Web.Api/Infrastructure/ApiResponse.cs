namespace Web.Api.Infrastructure;

public sealed record ApiResponse<T>(T Data);
