using TaskFlow.Api.Domain;

namespace TaskFlow.UnitTests.Domain;

public class TaskItemTests
{
    [Fact]
    public void New_task_has_todo_status_by_default()
    {
        var task = new TaskItem();
        task.Status.Should().Be(TaskItemStatus.Todo);
    }

    [Fact]
    public void New_task_has_medium_priority_by_default()
    {
        var task = new TaskItem();
        task.Priority.Should().Be(TaskPriority.Medium);
    }

    [Fact]
    public void New_task_tags_is_empty_list_not_null()
    {
        var task = new TaskItem();
        task.Tags.Should().NotBeNull();
        task.Tags.Should().BeEmpty();
    }

    [Fact]
    public void New_task_id_is_valid_objectid_string()
    {
        var task = new TaskItem();
        task.Id.Should().NotBeNullOrEmpty();
        task.Id.Should().HaveLength(24);
        task.Id.Should().MatchRegex("^[a-f0-9]{24}$");
    }

    [Fact]
    public void New_task_createdAt_is_approximately_utcnow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var task = new TaskItem();
        var after = DateTime.UtcNow.AddSeconds(1);

        task.CreatedAt.Should().BeAfter(before).And.BeBefore(after);
    }

    [Fact]
    public void Two_tasks_have_different_ids()
    {
        var task1 = new TaskItem();
        var task2 = new TaskItem();
        task1.Id.Should().NotBe(task2.Id);
    }

    [Fact]
    public void AssigneeId_is_null_by_default()
    {
        var task = new TaskItem();
        task.AssigneeId.Should().BeNull();
    }

    [Fact]
    public void DueDate_is_null_by_default()
    {
        var task = new TaskItem();
        task.DueDate.Should().BeNull();
    }

    [Theory]
    [InlineData(TaskItemStatus.Todo)]
    [InlineData(TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.InReview)]
    [InlineData(TaskItemStatus.Done)]
    public void Status_can_be_set_to_any_valid_value(TaskItemStatus status)
    {
        var task = new TaskItem { Status = status };
        task.Status.Should().Be(status);
    }

    [Theory]
    [InlineData(TaskPriority.Low)]
    [InlineData(TaskPriority.Medium)]
    [InlineData(TaskPriority.High)]
    [InlineData(TaskPriority.Critical)]
    public void Priority_can_be_set_to_any_valid_value(TaskPriority priority)
    {
        var task = new TaskItem { Priority = priority };
        task.Priority.Should().Be(priority);
    }

    [Fact]
    public void Tags_can_be_set_and_retrieved()
    {
        var task = new TaskItem { Tags = ["backend", "urgent", "v2"] };
        task.Tags.Should().BeEquivalentTo(["backend", "urgent", "v2"]);
    }

    [Fact]
    public void UpdatedAt_can_be_changed()
    {
        var task = new TaskItem();
        var newTime = DateTime.UtcNow.AddMinutes(5);
        task.UpdatedAt = newTime;
        task.UpdatedAt.Should().Be(newTime);
    }
}
