using Lookout.Runner.Listener;
using Microsoft.Extensions.Logging;

public class FakeQueueListener(ILogger<FakeQueueListener> logger) : IQueueListener<TestProviderData>
{
    private readonly Dictionary<string, List<IQueueListenerDelegate<TestProviderData>>> _queueListeners = new ();
    private int count = 0;

    public async Task StartListening(string queue, IQueueListenerDelegate<TestProviderData> listener) {
        if (!_queueListeners.TryGetValue(queue, out List<IQueueListenerDelegate<TestProviderData>>? listeners))
        {
            listeners = [];
        }

        listeners.Add(listener);
        _queueListeners[queue] = listeners;

        // Test code
        await EmitFakeMessages();
    }

    public Task ConfirmReceipt(QueueMessage<TestProviderData> message)
    {
        // No-op
        return Task.CompletedTask;
    }


    private async Task EmitFakeMessages()
    {
        foreach (var queueListener in _queueListeners)
        {
            foreach (var listener in queueListener.Value)
            {
                logger.LogInformation("emitting fake message");
                listener.OnReceived(new QueueMessage<TestProviderData>(new ImageDescription("redis","7"), new TestProviderData(count), null, null));
            }
        }

        count += 1;
        await Task.Delay(TimeSpan.FromSeconds(30));
        await EmitFakeMessages();
    }
}