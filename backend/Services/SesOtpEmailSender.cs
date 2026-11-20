using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;

namespace CarLease.Api.Services;

public class SesOtpEmailSender(IAmazonSimpleEmailServiceV2 ses, IConfiguration configuration) : IOtpEmailSender
{
    public async Task SendAsync(string recipient, string otp, CancellationToken cancellationToken)
    {
        var fromAddress = configuration["Auth:EmailFrom"];
        if (string.IsNullOrWhiteSpace(fromAddress))
            throw new InvalidOperationException("Configure Auth:EmailFrom with a verified Amazon SES sender address.");

        await ses.SendEmailAsync(new SendEmailRequest
        {
            FromEmailAddress = fromAddress,
            Destination = new Destination { ToAddresses = [recipient] },
            Content = new EmailContent
            {
                Simple = new Message
                {
                    Subject = new Content { Data = "Your Royal Limo Service sign-in code" },
                    Body = new Body
                    {
                        Text = new Content { Data = $"Your verification code is {otp}. It expires in five minutes. If you did not request this code, you can ignore this email." }
                    }
                }
            }
        }, cancellationToken);
    }
}
