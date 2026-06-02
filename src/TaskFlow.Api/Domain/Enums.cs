namespace TaskFlow.Api.Domain;

public enum ProjectStatus { Active, Completed, Archived }

public enum TaskItemStatus { Todo, InProgress, InReview, Done }

public enum TaskPriority { Low, Medium, High, Critical }

public enum UserRole { Admin, Member, Viewer }

public enum WorkspaceMemberRole { Owner, Editor, Viewer }
