namespace Lookout.Runner.Listener;

public interface IQueueListenerDelegate<T>
{
    public void OnReceived(QueueMessage<T> message);
}

public record TestProviderData(int token);

public interface IQueueListener<T>
{
    public Task StartListening(string queue, IQueueListenerDelegate<T> listener);
    public Task ConfirmReceipt(QueueMessage<T> message);
}