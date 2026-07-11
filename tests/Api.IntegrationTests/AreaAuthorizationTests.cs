using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using AntiguoAserradero.Api;
using AntiguoAserradero.Application.Areas;
using AntiguoAserradero.Application.Reference;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AntiguoAserradero.Api.IntegrationTests;

// Regression tests for the app-role authorization pipeline. Azure AD app roles arrive in the
// "roles" claim; the API policies use RequireClaim("roles", ...). These guard against inbound
// claim mapping (MapInboundClaims) silently renaming "roles" to ClaimTypes.Role, which would make
// every role check fail with 403 even when the token clearly carries the role.
public sealed class AreaAuthorizationTests : IClassFixture<AreaAuthorizationTests.TestAuthWebApplicationFactory>
{
    private readonly TestAuthWebApplicationFactory _factory;

    public AreaAuthorizationTests(TestAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateAreaWithCatalogManageRolePassesAuthorization()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = BuildCreateAreaRequest(TestAuthWebApplicationFactory.CreateToken("Catalog.Manage"));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateAreaWithoutCatalogManageRoleIsForbidden()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = BuildCreateAreaRequest(TestAuthWebApplicationFactory.CreateToken("Reservations.Manage"));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateAreaWithoutTokenIsUnauthorized()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = BuildCreateAreaRequest(token: null);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static HttpRequestMessage BuildCreateAreaRequest(string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/areas/")
        {
            Content = JsonContent.Create(new UpsertAreaRequest(
                "Bosque",
                new TimeOnly(15, 0),
                new TimeOnly(12, 0),
                new TimeOnly(8, 0),
                new TimeOnly(20, 0))),
        };

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    public sealed class TestAuthWebApplicationFactory : WebApplicationFactory<Program>
    {
        // 512-bit symmetric key used to sign and validate test tokens locally (no Azure AD calls).
        private static readonly SymmetricSecurityKey SigningKey =
            new(Encoding.UTF8.GetBytes("antiguo-aserradero-integration-tests-signing-key-please-change-0001"));

        public static string CreateToken(params string[] roles)
        {
            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = "https://test.antiguoaserradero.local/",
                Audience = "api://antiguo-aserradero-tests",
                SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256),
                Claims = new Dictionary<string, object>
                {
                    ["scp"] = "access_as_user",
                    ["roles"] = roles,
                    ["oid"] = Guid.NewGuid().ToString(),
                    ["name"] = "Integration Test",
                },
            };

            return new JsonWebTokenHandler().CreateToken(descriptor);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                // Validate locally-signed test tokens instead of contacting Azure AD, while keeping the
                // real JwtBearer handler and the app's MapInboundClaims=false so authorization is exercised
                // exactly as in production. Runs last, so it overrides Microsoft.Identity.Web's setup.
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    // Null ConfigurationManager stops the handler from fetching Azure AD metadata; it then
                    // validates purely against the local signing key below.
                    options.ConfigurationManager = null!;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ValidateLifetime = false,
                        ValidateIssuerSigningKey = false,
                        IssuerSigningKey = SigningKey,
                    };
                });

                // Stub the catalog service so the authorized path returns cleanly without a database.
                services.RemoveAll<IAreaService>();
                services.AddScoped<IAreaService, StubAreaService>();
            });
        }
    }

    private sealed class StubAreaService : IAreaService
    {
        public Task<AreaDto> CreateAsync(UpsertAreaRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new AreaDto(
                1,
                request.Name,
                request.CheckInTime,
                request.CheckOutTime,
                request.ReceptionOpenTime,
                request.ReceptionCloseTime,
                true));

        public Task<PagedResult<AreaDto>> ListAsync(CatalogListQuery query, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<AreaDto> GetAsync(int id, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<AreaDto> UpdateAsync(int id, UpsertAreaRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<AreaMutationResponse> DeactivateAsync(int id, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
