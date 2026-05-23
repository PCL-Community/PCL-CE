namespace PCL.Core.App.Tasks;

public delegate void TaskGroupEvent(ITask task);

public interface ITaskGroup : ITask
{
    public event TaskGroupEvent AddTask;
    public event TaskGroupEvent RemoveTask;
}
