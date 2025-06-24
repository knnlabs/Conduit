using System;
using System.Collections.Generic;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Tests.TestHelpers.Builders
{
    /// <summary>
    /// Builder for creating AsyncTask test data
    /// </summary>
    public class AsyncTaskBuilder
    {
        private readonly AsyncTask _task;

        public AsyncTaskBuilder()
        {
            _task = new AsyncTask
            {
                Id = Guid.NewGuid().ToString(),
                State = (int)TaskState.Pending,
                Type = "test-task",
                Payload = "{}",
                VirtualKeyId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsArchived = false,
                Progress = 0,
                Metadata = "{}"
            };
        }

        public AsyncTaskBuilder WithId(string id)
        {
            _task.Id = id;
            return this;
        }

        public AsyncTaskBuilder WithState(TaskState state)
        {
            _task.State = (int)state;
            return this;
        }

        public AsyncTaskBuilder WithType(string type)
        {
            _task.Type = type;
            return this;
        }

        public AsyncTaskBuilder WithPayload(string payload)
        {
            _task.Payload = payload;
            return this;
        }

        public AsyncTaskBuilder WithPayload(object payload)
        {
            _task.Payload = System.Text.Json.JsonSerializer.Serialize(payload);
            return this;
        }

        public AsyncTaskBuilder WithProgress(int progress, string? message = null)
        {
            _task.Progress = progress;
            _task.ProgressMessage = message;
            return this;
        }

        public AsyncTaskBuilder WithResult(string result)
        {
            _task.Result = result;
            return this;
        }

        public AsyncTaskBuilder WithResult(object result)
        {
            _task.Result = System.Text.Json.JsonSerializer.Serialize(result);
            return this;
        }

        public AsyncTaskBuilder WithError(string error)
        {
            _task.Error = error;
            _task.State = (int)TaskState.Failed;
            return this;
        }

        public AsyncTaskBuilder WithVirtualKeyId(int virtualKeyId)
        {
            _task.VirtualKeyId = virtualKeyId;
            return this;
        }

        public AsyncTaskBuilder WithMetadata(string metadata)
        {
            _task.Metadata = metadata;
            return this;
        }

        public AsyncTaskBuilder WithMetadata(Dictionary<string, object> metadata)
        {
            _task.Metadata = System.Text.Json.JsonSerializer.Serialize(metadata);
            return this;
        }

        public AsyncTaskBuilder AsCompleted(string result = "{\"status\":\"success\"}")
        {
            _task.State = (int)TaskState.Completed;
            _task.Result = result;
            _task.CompletedAt = DateTime.UtcNow;
            _task.Progress = 100;
            return this;
        }

        public AsyncTaskBuilder AsProcessing(int progress = 50)
        {
            _task.State = (int)TaskState.Processing;
            _task.Progress = progress;
            return this;
        }

        public AsyncTaskBuilder AsFailed(string error = "Task failed")
        {
            _task.State = (int)TaskState.Failed;
            _task.Error = error;
            _task.CompletedAt = DateTime.UtcNow;
            return this;
        }

        public AsyncTaskBuilder AsCancelled()
        {
            _task.State = (int)TaskState.Cancelled;
            _task.CompletedAt = DateTime.UtcNow;
            return this;
        }

        public AsyncTaskBuilder AsArchived()
        {
            _task.IsArchived = true;
            _task.ArchivedAt = DateTime.UtcNow;
            return this;
        }

        public AsyncTaskBuilder WithCreatedAt(DateTime createdAt)
        {
            _task.CreatedAt = createdAt;
            return this;
        }

        public AsyncTaskBuilder WithUpdatedAt(DateTime updatedAt)
        {
            _task.UpdatedAt = updatedAt;
            return this;
        }

        public AsyncTaskBuilder WithCompletedAt(DateTime? completedAt)
        {
            _task.CompletedAt = completedAt;
            return this;
        }

        public AsyncTaskBuilder WithArchivedAt(DateTime archivedAt)
        {
            _task.ArchivedAt = archivedAt;
            _task.IsArchived = true;
            return this;
        }

        public AsyncTask Build() => _task;
    }

    /// <summary>
    /// Builder for creating AsyncTaskStatus test data
    /// </summary>
    public class AsyncTaskStatusBuilder
    {
        private readonly AsyncTaskStatus _status;

        public AsyncTaskStatusBuilder()
        {
            _status = new AsyncTaskStatus
            {
                TaskId = Guid.NewGuid().ToString(),
                State = TaskState.Pending,
                Progress = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public AsyncTaskStatusBuilder WithTaskId(string taskId)
        {
            _status.TaskId = taskId;
            return this;
        }

        public AsyncTaskStatusBuilder WithState(TaskState state)
        {
            _status.State = state;
            return this;
        }

        public AsyncTaskStatusBuilder WithProgress(int progress, string? message = null)
        {
            _status.Progress = progress;
            _status.ProgressMessage = message;
            return this;
        }

        public AsyncTaskStatusBuilder WithResult(object result)
        {
            _status.Result = result;
            _status.State = TaskState.Completed;
            return this;
        }

        public AsyncTaskStatusBuilder WithError(string error)
        {
            _status.Error = error;
            _status.State = TaskState.Failed;
            return this;
        }

        public AsyncTaskStatusBuilder AsCompleted(object result = null)
        {
            _status.State = TaskState.Completed;
            _status.Progress = 100;
            _status.Result = result ?? new { status = "success" };
            _status.CompletedAt = DateTime.UtcNow;
            return this;
        }

        public AsyncTaskStatusBuilder AsProcessing(int progress = 50)
        {
            _status.State = TaskState.Processing;
            _status.Progress = progress;
            return this;
        }

        public AsyncTaskStatusBuilder AsFailed(string error = "Task failed")
        {
            _status.State = TaskState.Failed;
            _status.Error = error;
            _status.CompletedAt = DateTime.UtcNow;
            return this;
        }

        public AsyncTaskStatusBuilder AsCancelled()
        {
            _status.State = TaskState.Cancelled;
            _status.CompletedAt = DateTime.UtcNow;
            return this;
        }

        public AsyncTaskStatus Build() => _status;
    }
}