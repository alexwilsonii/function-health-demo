namespace TaskManager.Api.Domain;

// Named TaskState (not TaskStatus) to avoid colliding with System.Threading.Tasks.TaskStatus,
// which is in scope via implicit usings. Serialized to JSON as strings ("Todo", "InProgress", "Done");
// stored in the database as ints so that ordering by Priority is semantic (Low < Medium < High).
public enum TaskState
{
    Todo,
    InProgress,
    Done
}

public enum TaskPriority
{
    Low,
    Medium,
    High
}
