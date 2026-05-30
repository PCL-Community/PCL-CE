using System.Threading.Tasks;

namespace PCL.Core.App.EventBus;

// I think this is too hard to implement. So this is Obsolete.
public interface IResponsibleEventHandler<TResponse>
{
    /// <summary>
    /// Handle a event with the data, and the event is published by a publisher, and return a response to the publisher.
    /// </summary>
    /// <param name="eventData">The event data that published by a publisher</param>
    Task<TResponse> HandleEventAsync(EventDataBase eventData);
}