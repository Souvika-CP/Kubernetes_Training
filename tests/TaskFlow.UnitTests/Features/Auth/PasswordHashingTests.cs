namespace TaskFlow.UnitTests.Features.Auth;

public class PasswordHashingTests
{
    [Fact]
    public void HashPassword_returns_non_empty_hash()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("MySecurePassword1!");
        hash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void HashPassword_does_not_return_plain_text()
    {
        const string password = "MySecurePassword1!";
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        hash.Should().NotBe(password);
    }

    [Fact]
    public void Verify_returns_true_for_correct_password()
    {
        const string password = "CorrectHorseBatteryStaple";
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        BCrypt.Net.BCrypt.Verify(password, hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_returns_false_for_wrong_password()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("RightPassword");
        BCrypt.Net.BCrypt.Verify("WrongPassword", hash).Should().BeFalse();
    }

    [Fact]
    public void Same_password_produces_different_hashes_each_time()
    {
        const string password = "SamePasswordEveryTime";
        var hash1 = BCrypt.Net.BCrypt.HashPassword(password);
        var hash2 = BCrypt.Net.BCrypt.HashPassword(password);

        // BCrypt embeds a random salt — hashes must differ
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Both_hashes_of_same_password_verify_correctly()
    {
        const string password = "SamePasswordEveryTime";
        var hash1 = BCrypt.Net.BCrypt.HashPassword(password);
        var hash2 = BCrypt.Net.BCrypt.HashPassword(password);

        BCrypt.Net.BCrypt.Verify(password, hash1).Should().BeTrue();
        BCrypt.Net.BCrypt.Verify(password, hash2).Should().BeTrue();
    }

    [Fact]
    public void Hash_starts_with_bcrypt_identifier()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("AnyPassword");
        // BCrypt hashes always start with $2a$ or $2b$
        hash.Should().StartWith("$2");
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("short")]
    [InlineData("a very long password that is more than 32 characters long")]
    public void Verify_works_for_various_password_lengths(string password)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        BCrypt.Net.BCrypt.Verify(password, hash).Should().BeTrue();
    }
}
