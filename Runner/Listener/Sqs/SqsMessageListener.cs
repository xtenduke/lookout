using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;

namespace Lookout.Runner.Listener.Sqs;

public record SqsProviderData(string ReceiptHandle, string MessageId);

public record LookoutSqsMessageBody(string ImageName, string ImageTag, string? DeployTimeSeconds);


public class SqsMessageListener(IAmazonSQS sqsClient, ILogger<SqsMessageListener> logger) : IQueueListener<SqsProviderData>
{
    public Task StartListening(string queue, IQueueListenerDelegate<SqsProviderData> listener)
    {
        while (true)
        {
            var message = GetMessage(sqsClient, queue).Result;
            if (message.Messages.Count > 0)
            {
                foreach (var msg in message.Messages)
                {
                    var parsedMessage = ParseMessage(msg);
                    if (parsedMessage == null)
                    {
                        logger.LogError($"Failed to deserialize message body");
                        logger.LogDebug($"body: {msg.Body}");
                    }

                    listener.OnReceived(parsedMessage);
                }
            }
            else
            {
                // No messages available, wait for a while before checking again
                Thread.Sleep(1000);
            }
        }
    }

    // Get a message off the queue
    private static async Task<ReceiveMessageResponse> GetMessage(IAmazonSQS sqsClient, string queue)
    {
        return await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest {
            QueueUrl=queue,
            MaxNumberOfMessages=1,
            WaitTimeSeconds=1
        });
    }

    // Delete the message from the queue
    public async Task ConfirmReceipt(QueueMessage<SqsProviderData> message)
    {
        await sqsClient.DeleteMessageAsync(new DeleteMessageRequest
        {
            QueueUrl = message.ImageDescription.Name,
            ReceiptHandle = message.ImageDescription.Tag
        });
    }

    private static QueueMessage<SqsProviderData>? ParseMessage(Message? message)
    {
        if (message == null)
        {
            return null;
        }

        var body = JsonSerializer.Deserialize<LookoutSqsMessageBody>(message.Body);
        if (body == null)
        {
            return null;
        }

        var imageDescription = new ImageDescription(body.ImageName, body.ImageTag);
        var providerData = new SqsProviderData(message.ReceiptHandle, message.MessageId);

        TimeSpan? deployTime = null;
        if (body.DeployTimeSeconds != null && int.TryParse(body.DeployTimeSeconds, out var deployTimeSeconds))
        {
            deployTime = TimeSpan.FromSeconds(deployTimeSeconds);
        }

        return new QueueMessage<SqsProviderData>(
            imageDescription,
            providerData,
            deployTime);
    }
}