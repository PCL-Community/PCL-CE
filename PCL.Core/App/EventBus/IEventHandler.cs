using System;
using System.Threading.Tasks;

namespace PCL.Core.App.EventBus;

public interface IEventHandler<in TEventData> : IDisposable
    where TEventData : EventDataBase
{
    /// <summary>
    /// Handle a event with the data, and the event is published by a publisher.
    /// </summary>
    /// <param name="eventData">The data that published by a publisher.</param>
    Task HandleEventAsync(TEventData eventData);
}