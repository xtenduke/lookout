using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;

namespace Lookout.Runner.Listener.Sqs;

public record SqsProviderData(string ReceiptHandle, string MessageId, string QueueUrl);

public class SqsMessageListener(IAmazonSQS sqsClient, ILogger<SqsMessageListener> logger) : IQueueListener<SqsProviderData>
{
    private JsonSerializerOptions _jsonSerializationOptions = new JsonSerializerOptions {
        PropertyNameCaseInsensitive = true
    };

    public Task StartListening(string queue, IQueueListenerDelegate<SqsProviderData> listener)
    {
        while (true)
        {
            var message = GetMessage(sqsClient, queue).Result;
            if (message.Messages.Count > 0)
            {
                foreach (var msg in message.Messages)
                {
                    var parsedMessage = ParseMessage(msg, queue);
                    if (parsedMessage == null)
                    {
                        logger.LogError($"Failed to deserialize message body");
                        logger.LogDebug("body: {Body}", msg.Body);
                        continue;
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
        return await sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = queue,
            MaxNumberOfMessages = 1,
            WaitTimeSeconds = 1
        });
    }

    // Delete the message from the queue
    public async Task ConfirmReceipt(QueueMessage<SqsProviderData> message)
    {
        logger.LogDebug("Confirming receipt of message: {MessageId}", message.ProviderData.MessageId);
        var res = await sqsClient.DeleteMessageAsync(new DeleteMessageRequest
        {
            QueueUrl = message.ProviderData.QueueUrl,
            ReceiptHandle = message.ProviderData.ReceiptHandle 
        });

        if (res.HttpStatusCode == System.Net.HttpStatusCode.OK)
        {
            logger.LogDebug("Confirmed receipt of message: {MessageId}", message.ProviderData.MessageId);
        } else
        {
            logger.LogError("Failed to confirm receipt of message: {MessageId}, with code {Code}", message.ProviderData.MessageId, res.HttpStatusCode);
        }
    }

    private QueueMessage<SqsProviderData>? ParseMessage(Message? message, string queueUrl)
    {
        if (message == null)
        {
            return null;
        }

        try
        {
            var body = JsonSerializer.Deserialize<LookoutSqsMessageBody>(message.Body, _jsonSerializationOptions);
            if (body == null || body.Validate() == false)
            {
                return null;
            }

            var imageDescription = new ImageDescription(body.ImageName, body.ImageTag);
            var providerData = new SqsProviderData(message.ReceiptHandle, message.MessageId, queueUrl);

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
        catch (Exception ex)
        {
            logger.LogDebug("Failed to parse message body: {Body}", message.Body);
            logger.LogError(ex, "Failed to parse message");
            return null;
        }
    }
}
