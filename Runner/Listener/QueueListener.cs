namespace Lookout.Runner.Listener;

public interface IQueueListenerDelegate
{
    public void OnReceived(QueueMessage message);
}

public interface IQueueListener
{
    public Task StartListening(string queue, IQueueListenerDelegate listener);
}

public class QueueListener : IQueueListener
{
    private readonly Dictionary<string, List<IQueueListenerDelegate>> _queueListeners = new ();

    public async Task StartListening(string queue, IQueueListenerDelegate listener) {
        if (!_queueListeners.TryGetValue(queue, out List<IQueueListenerDelegate>? listeners))
        {
            listeners = new List<IQueueListenerDelegate>();
        }
        listeners.Add(listener);
        _queueListeners[queue] = listeners;

        // Test code
        await EmitFakeMessages();
    }


    private async Task EmitFakeMessages()
    {
        foreach (var queueListener in _queueListeners)
        {
            foreach (var listener in queueListener.Value)
            {
                listener.OnReceived(new QueueMessage(new ImageDescription("redis","7")));
            }
        }
        await Task.Delay(TimeSpan.FromSeconds(30));
        await EmitFakeMessages();
    }
}