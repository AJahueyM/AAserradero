using AntiguoAserradero.Application.Users;

namespace AntiguoAserradero.Api.Endpoints.Users;

public sealed class UsersEndpointModule : IEndpointModule
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // User administration requires both Catalog.Manage and Reservations.Manage; the
        // Application service enforces the combined capability check after authentication.
        var group = endpoints.MapGroup("/api/users")
            .RequireAuthorization()
            .WithTags("Users");

        group.MapGet("/", ListAsync)
            .WithName("ListStaffUsers");

        group.MapPost("/", CreateAsync)
            .WithName("CreateStaffUser");

        group.MapPut("/{id:int}", UpdateAsync)
            .WithName("UpdateStaffUser");

        group.MapPost("/{id:int}/capabilities", AssignCapabilityAsync)
            .WithName("AssignStaffUserCapability");

        group.MapDelete("/{id:int}/capabilities/{capability}", RemoveCapabilityAsync)
            .WithName("RemoveStaffUserCapability");

        group.MapPost("/{id:int}/password", ResetPasswordAsync)
            .WithName("ResetStaffUserPassword");

        group.MapDelete("/{id:int}", DisableAsync)
            .WithName("DisableStaffUser");
    }

    private static async Task<IResult> ListAsync(IStaffUserAdministrationService service, CancellationToken cancellationToken)
    {
        var response = await service.ListAsync(cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> CreateAsync(IStaffUserAdministrationService service, CreateStaffUserRequest request, CancellationToken cancellationToken)
    {
        var response = await service.CreateAsync(request, cancellationToken);
        return Results.Created($"/api/users/{response.Id}", response);
    }

    private static async Task<IResult> UpdateAsync(IStaffUserAdministrationService service, int id, UpdateStaffUserRequest request, CancellationToken cancellationToken)
    {
        var response = await service.UpdateAsync(id, request, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> AssignCapabilityAsync(IStaffUserAdministrationService service, int id, StaffUserCapabilityRequest request, CancellationToken cancellationToken)
    {
        var response = await service.AssignCapabilityAsync(id, request, cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> RemoveCapabilityAsync(IStaffUserAdministrationService service, int id, string capability, CancellationToken cancellationToken)
    {
        var response = await service.RemoveCapabilityAsync(id, new StaffUserCapabilityRequest(capability), cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> ResetPasswordAsync(IStaffUserAdministrationService service, int id, ResetStaffUserPasswordRequest request, CancellationToken cancellationToken)
    {
        await service.ResetPasswordAsync(id, request, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> DisableAsync(IStaffUserAdministrationService service, int id, CancellationToken cancellationToken)
    {
        await service.DisableAsync(id, cancellationToken);
        return Results.NoContent();
    }
}
