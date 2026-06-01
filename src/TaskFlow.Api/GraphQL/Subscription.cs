using HotChocolate;
using HotChocolate.Subscriptions;
using TaskFlow.Api.Domain;

namespace TaskFlow.Api.GraphQL;

public class Subscription
{
    [Subscribe]
    [Topic("taskUpdated_{projectId}")]
    public TaskItem OnTaskUpdated(
        string projectId,
        [EventMessage] TaskItem task) => task;

    [Subscribe]
    [Topic("commentAdded_{taskId}")]
    public Comment OnCommentAdded(
        string taskId,
        [EventMessage] Comment comment) => comment;
}
