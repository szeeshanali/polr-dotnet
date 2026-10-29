using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using PolrNet.Data;

namespace PolrNet.Services;

public sealed class Mailer(PolrNetOptions options, IHostEnvironment environment, ILogger<Mailer> logger)
{
    public Task SendActivationAsync(User user) => SendAsync(user, $"{options.AppName} account activation",
        $"""
        Hi {user.Username},

        Thanks for signing up for {options.AppName}. Activate your account here:
        {options.BaseUrl}/activate/{Uri.EscapeDataString(user.Username)}/{user.RecoveryKey}

        If you did not sign up, you can ignore this email.
        """);

    public Task SendPasswordResetAsync(User user, string recoveryKey) => SendAsync(user, $"{options.AppName} password reset",
        $"""
        Hi {user.Username},

        Someone asked to reset the password for your {options.AppName} account. Choose a new password here:
        {options.BaseUrl}/reset_password/{Uri.EscapeDataString(user.Username)}/{recoveryKey}

        If this was not you, you can ignore this email.
        """);

    async Task SendAsync(User to, string subject, string body)
    {
        if (!options.Smtp.Enabled)
        {
            // Without SMTP, show the email in development so sign-up and resets can still be tried locally.
            if (environment.IsDevelopment())
                logger.LogWarning("SMTP is not configured. Email to {Email}:\n{Body}", to.Email, body);
            else
                logger.LogWarning("SMTP is not configured, so the email to {Email} was not sent", to.Email);
            return;
        }

        var message = new MimeMessage { Subject = subject, Body = new TextPart("plain") { Text = body } };
        message.From.Add(new MailboxAddress(options.Smtp.FromName ?? options.AppName, options.Smtp.FromAddress));
        message.To.Add(new MailboxAddress(to.Username, to.Email));

        using var client = new SmtpClient();
        await client.ConnectAsync(options.Smtp.Host!, options.Smtp.Port, SecureSocketOptions.Auto);
        if (!string.IsNullOrEmpty(options.Smtp.Username))
            await client.AuthenticateAsync(options.Smtp.Username, options.Smtp.Password ?? "");
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
