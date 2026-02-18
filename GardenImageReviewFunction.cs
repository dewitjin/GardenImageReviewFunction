
using Azure.Messaging.ServiceBus;
using GardenImageReviewFunction;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Text.Json;


namespace GardenImage.Function;

public class GardenImageReviewFunction
{
    private readonly ILogger<GardenImageReviewFunction> _logger;

    public GardenImageReviewFunction(ILogger<GardenImageReviewFunction> logger)
    {
        _logger = logger;
    }

    [Function(nameof(GardenImageReviewFunction))]
    public async Task Run(
        [ServiceBusTrigger("image-review-queue", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {

        ImageReviewMessage? data;
        try
        {
            data = JsonSerializer.Deserialize<ImageReviewMessage>(message.Body);
            if (data == null || data.PlantId <= 0 || string.IsNullOrWhiteSpace(data.ReviewImageSasUrl))
            {
                _logger.LogWarning("Invalid message format");
                // await messageActions.AbandonMessageAsync(message); // ← only if autoComplete=false
                return;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize message");
            // await messageActions.DeadLetterMessageAsync(message, "InvalidJson", ex.Message); // ← only if autoComplete=false
            return;
        }

        var apiKey = Environment.GetEnvironmentVariable("SendGridApiKey");
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogCritical("SendGridApiKey is missing");
            throw new InvalidOperationException("SendGrid API key not configured");
        }

        try
        {
            var client = new SendGridClient(apiKey);

            var email = new SendGridMessage();
            email.SetFrom("gardenapi@proton.me", "Garden App");
            email.AddTo("gardenapi@proton.me");
            email.SetSubject($"Review Plant Image #{data.PlantId}");

            // TODO: Image is in blob so we need to get it from blob storage and embed as base64. 
            // Testing with SAS URL for now, but this will expire after a while and cause broken images in the email.
            var htmlContent = $@"
                <h2>New plant image needs review</h2>
                <img src='{data.ReviewImageSasUrl}' style='max-width:600px;' />
                <br><br>
                <a href='https://gardenapi-h6b6bebhf9gndqhb.canadacentral-01.azurewebsites.net/review/approve/{data.PlantId}' 
                   style='background:green;color:white;padding:10px 20px;text-decoration:none;'>✅ Approve</a>
                &nbsp;&nbsp;
                <a href='https://gardenapi-h6b6bebhf9gndqhb.canadacentral-01.azurewebsites.net/review/delete/{data.PlantId}' 
                   style='background:red;color:white;padding:10px 20px;text-decoration:none;'>🗑️ Delete</a>";

            email.AddContent("text/html", htmlContent);

            var response = await client.SendEmailAsync(email);
            _logger.LogInformation($"Email sent: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process image review for plant {PlantId} - image {ImageUrl}", data.PlantId, data.ReviewImageSasUrl);

            // Option A: let runtime abandon → retry → eventual DLQ (most common)
            throw;

            // Option B: dead-letter immediately with reason (requires autoComplete = false)
            // await messageActions.DeadLetterMessageAsync(message, "ProcessingFailed", ex.Message);
        }
    }
}