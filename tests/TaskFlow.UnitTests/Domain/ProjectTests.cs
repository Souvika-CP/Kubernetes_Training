using TaskFlow.Api.Domain;

namespace TaskFlow.UnitTests.Domain;

public class ProjectTests
{
    [Fact]
    public void New_project_has_active_status_by_default()
    {
        var project = new Project();
        project.Status.Should().Be(ProjectStatus.Active);
    }

    [Fact]
    public void New_project_id_is_valid_objectid_string()
    {
        var project = new Project();
        project.Id.Should().NotBeNullOrEmpty();
        project.Id.Should().HaveLength(24);
        project.Id.Should().MatchRegex("^[a-f0-9]{24}$");
    }

    [Fact]
    public void Two_projects_have_different_ids()
    {
        var p1 = new Project();
        var p2 = new Project();
        p1.Id.Should().NotBe(p2.Id);
    }

    [Fact]
    public void New_project_createdAt_is_approximately_utcnow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var project = new Project();
        var after = DateTime.UtcNow.AddSeconds(1);

        project.CreatedAt.Should().BeAfter(before).And.BeBefore(after);
    }

    [Theory]
    [InlineData(ProjectStatus.Active)]
    [InlineData(ProjectStatus.Completed)]
    [InlineData(ProjectStatus.Archived)]
    public void Status_can_be_set_to_any_valid_value(ProjectStatus status)
    {
        var project = new Project { Status = status };
        project.Status.Should().Be(status);
    }

    [Fact]
    public void Name_and_description_can_be_set()
    {
        var project = new Project { Name = "My Project", Description = "A test project" };
        project.Name.Should().Be("My Project");
        project.Description.Should().Be("A test project");
    }
}
