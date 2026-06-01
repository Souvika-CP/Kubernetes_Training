using TaskFlow.Api.Domain;

namespace TaskFlow.UnitTests.Domain;

public class UserTests
{
    [Fact]
    public void New_user_has_member_role_by_default()
    {
        var user = new User();
        user.Role.Should().Be(UserRole.Member);
    }

    [Fact]
    public void New_user_id_is_valid_objectid()
    {
        var user = new User();
        user.Id.Should().NotBeNullOrEmpty();
        user.Id.Should().HaveLength(24);
        user.Id.Should().MatchRegex("^[a-f0-9]{24}$");
    }

    [Fact]
    public void Two_users_have_different_ids()
    {
        var u1 = new User();
        var u2 = new User();
        u1.Id.Should().NotBe(u2.Id);
    }

    [Fact]
    public void PasswordHash_field_exists_and_can_be_set()
    {
        var user = new User { PasswordHash = "some-hash" };
        user.PasswordHash.Should().Be("some-hash");
    }
}
