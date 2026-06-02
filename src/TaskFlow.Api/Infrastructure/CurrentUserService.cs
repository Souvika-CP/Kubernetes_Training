using System.IdentityModel.Tokens.Jwt;
using HotChocolate;

namespace TaskFlow.Api.Infrastructure;

public interface ICurrentUserService
{
    string UserId { get; }
    bool IsAuthenticated { get; }
}

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public string UserId =>
        httpContextAccessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? throw new GraphQLException(
            ErrorBuilder.New()
                .SetMessage("Authentication required.")
                .SetCode("UNAUTHENTICATED")
                .Build());

    public bool IsAuthenticated =>
        httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;
}
