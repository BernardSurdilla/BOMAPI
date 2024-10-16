using MailKit.Net.Smtp;
using MimeKit;

using System.ComponentModel.DataAnnotations;

namespace BOM_API_v2.Services
{
    public class IEmailServiceOptions
    {

    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        public EmailService(IConfiguration config) { _configuration = config; }

        public async Task<int> SendEmailConfirmationEmail(string recepientName, [EmailAddress] string recepientEmail, [Url] string confirmEmailLink)
        {
            try
            {
                string? senderName = _configuration.GetValue<string>("Email:SenderName");
                string? senderEmail = _configuration.GetValue<string>("Email:SenderEmail");
                string? smtpHost = _configuration.GetValue<string>("Email:SMTPServer:Host");
                int? port = _configuration.GetValue<int>("Email:SMTPServer:Port");
                string? userName = _configuration.GetValue<string>("Email:SMTPServer:UserName");
                string? password = _configuration.GetValue<string>("Email:SMTPServer:Password");


                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(senderName, senderEmail));
                message.To.Add(new MailboxAddress(recepientName, recepientEmail));
                message.Subject = "Confirm your Email";

                message.Body = new TextPart("html")
                {
                    Text = ConfirmationEmailFormat(confirmEmailLink)
                };

                using (var client = new SmtpClient())
                {
                    await client.ConnectAsync(smtpHost, port.Value, false);

                    // Note: only needed if the SMTP server requires authentication
                    await client.AuthenticateAsync(userName, password);

                    await client.SendAsync(message);
                    client.Disconnect(true);
                }
                return 0;
            }
            catch
            {
                return 1;
            }

        }
        public async Task<int> SendForgotPasswordEmail(string recepientName, [EmailAddress] string recepientEmail, [Url] string confirmEmailLink)
        {
            try
            {
                string? senderName = _configuration.GetValue<string>("Email:SenderName");
                string? senderEmail = _configuration.GetValue<string>("Email:SenderEmail");
                string? smtpHost = _configuration.GetValue<string>("Email:SMTPServer:Host");
                int? port = _configuration.GetValue<int>("Email:SMTPServer:Port");
                string? userName = _configuration.GetValue<string>("Email:SMTPServer:UserName");
                string? password = _configuration.GetValue<string>("Email:SMTPServer:Password");


                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(senderName, senderEmail));
                message.To.Add(new MailboxAddress(recepientName, recepientEmail));
                message.Subject = "Password Reset";

                message.Body = new TextPart("html")
                {
                    Text = ForgotPasswordEmailFormat(confirmEmailLink)
                };

                using (var client = new SmtpClient())
                {
                    await client.ConnectAsync(smtpHost, port.Value, false);

                    // Note: only needed if the SMTP server requires authentication
                    await client.AuthenticateAsync(userName, password);

                    await client.SendAsync(message);
                    client.Disconnect(true);
                }
                return 0;
            }
            catch
            {
                return 1;
            }

        }
        private string ConfirmationEmailFormat([Url] string confirmEmailLink)
        {
            string response = "<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Strict//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd\">\r\n<html xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:v=\"urn:schemas-microsoft-com:vml\" xmlns:o=\"urn:schemas-microsoft-com:office:office\"><head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\"><meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\"><meta name=\"format-detection\" content=\"telephone=no\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\"><title>Confirm your Email</title><style type=\"text/css\" emogrify=\"no\">#outlook a { padding:0; } .ExternalClass { width:100%; } .ExternalClass, .ExternalClass p, .ExternalClass span, .ExternalClass font, .ExternalClass td, .ExternalClass div { line-height: 100%; } table td { border-collapse: collapse; mso-line-height-rule: exactly; } .editable.image { font-size: 0 !important; line-height: 0 !important; } .nl2go_preheader { display: none !important; mso-hide:all !important; mso-line-height-rule: exactly; visibility: hidden !important; line-height: 0px !important; font-size: 0px !important; } body { width:100% !important; -webkit-text-size-adjust:100%; -ms-text-size-adjust:100%; margin:0; padding:0; } img { outline:none; text-decoration:none; -ms-interpolation-mode: bicubic; } a img { border:none; } table { border-collapse:collapse; mso-table-lspace:0pt; mso-table-rspace:0pt; } th { font-weight: normal; text-align: left; } *[class=\"gmail-fix\"] { display: none !important; } </style><style type=\"text/css\" emogrify=\"no\"> @media (max-width: 600px) { .gmx-killpill { content: ' \\03D1';} } </style><style type=\"text/css\" emogrify=\"no\">@media (max-width: 600px) { .gmx-killpill { content: ' \\03D1';} .r0-o { border-style: solid !important; margin: 0 auto 0 auto !important; width: 320px !important } .r1-i { background-color: #ffffff !important } .r2-o { border-style: solid !important; margin: 0 auto 0 auto !important; width: 100% !important } .r3-c { box-sizing: border-box !important; display: block !important; valign: top !important; width: 100% !important } .r4-o { border-style: solid !important; width: 100% !important } .r5-i { padding-left: 0px !important; padding-right: 0px !important } .r6-c { box-sizing: border-box !important; padding-bottom: 15px !important; padding-top: 15px !important; text-align: left !important; valign: top !important; width: 100% !important } .r7-c { box-sizing: border-box !important; padding: 0 !important; text-align: center !important; valign: top !important; width: 100% !important } .r8-o { border-style: solid !important; margin: 0 auto 0 auto !important; margin-bottom: 15px !important; margin-top: 15px !important; width: 100% !important } .r9-i { padding: 0 !important; text-align: center !important } .r10-r { background-color: #0092FF !important; border-radius: 4px !important; border-width: 0px !important; box-sizing: border-box; height: initial !important; padding: 0 !important; padding-bottom: 12px !important; padding-top: 12px !important; text-align: center !important; width: 100% !important } body { -webkit-text-size-adjust: none } .nl2go-responsive-hide { display: none } .nl2go-body-table { min-width: unset !important } .mobshow { height: auto !important; overflow: visible !important; max-height: unset !important; visibility: visible !important; border: none !important } .resp-table { display: inline-table !important } .magic-resp { display: table-cell !important } } </style><style type=\"text/css\">p, h1, h2, h3, h4, ol, ul, li { margin: 0; } a, a:link { color: #0092ff; text-decoration: underline } .nl2go-default-textstyle { color: #3b3f44; font-family: arial,helvetica,sans-serif; font-size: 16px; line-height: 1.5; word-break: break-word } .default-button { color: #ffffff; font-family: arial,helvetica,sans-serif; font-size: 16px; font-style: normal; font-weight: normal; line-height: 1.15; text-decoration: none; word-break: break-word } .default-heading1 { color: #1F2D3D; font-family: arial,helvetica,sans-serif; font-size: 36px; word-break: break-word } .default-heading2 { color: #1F2D3D; font-family: arial,helvetica,sans-serif; font-size: 32px; word-break: break-word } .default-heading3 { color: #1F2D3D; font-family: arial,helvetica,sans-serif; font-size: 24px; word-break: break-word } .default-heading4 { color: #1F2D3D; font-family: arial,helvetica,sans-serif; font-size: 18px; word-break: break-word } a[x-apple-data-detectors] { color: inherit !important; text-decoration: inherit !important; font-size: inherit !important; font-family: inherit !important; font-weight: inherit !important; line-height: inherit !important; } .no-show-for-you { border: none; display: none; float: none; font-size: 0; height: 0; line-height: 0; max-height: 0; mso-hide: all; overflow: hidden; table-layout: fixed; visibility: hidden; width: 0; } </style><!--[if mso]><xml> <o:OfficeDocumentSettings> <o:AllowPNG/> <o:PixelsPerInch>96</o:PixelsPerInch> </o:OfficeDocumentSettings> </xml><![endif]--><style type=\"text/css\">a:link{color: #0092ff; text-decoration: underline;}</style></head><body bgcolor=\"#ffffff\" text=\"#3b3f44\" link=\"#0092ff\" yahoo=\"fix\" style=\"background-color: #ffffff;\"> <table cellspacing=\"0\" cellpadding=\"0\" border=\"0\" role=\"presentation\" class=\"nl2go-body-table\" width=\"100%\" style=\"background-color: #ffffff; width: 100%;\"><tr><td> <table cellspacing=\"0\" cellpadding=\"0\" border=\"0\" role=\"presentation\" width=\"600\" align=\"center\" class=\"r0-o\" style=\"table-layout: fixed; width: 600px;\"><tr><td valign=\"top\" class=\"r1-i\" style=\"background-color: #ffffff;\"> <table cellspacing=\"0\" cellpadding=\"0\" border=\"0\" role=\"presentation\" width=\"100%\" align=\"center\" class=\"r2-o\" style=\"table-layout: fixed; width: 100%;\"><tr><th width=\"100%\" valign=\"top\" class=\"r3-c\" style=\"font-weight: normal;\"> <table cellspacing=\"0\" cellpadding=\"0\" border=\"0\" role=\"presentation\" width=\"100%\" class=\"r4-o\" style=\"table-layout: fixed; width: 100%;\"><tr><td valign=\"top\" class=\"r5-i\"> <table width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" role=\"presentation\"><tr><td class=\"r6-c nl2go-default-textstyle\" align=\"left\" style=\"color: #3b3f44; font-family: arial,helvetica,sans-serif; font-size: 16px; line-height: 1.5; word-break: break-word; padding-bottom: 15px; padding-top: 15px; text-align: left; valign: top;\"> <div><p style=\"margin: 0; text-align: center;\">Thank you for signing up to PinkButter. Confirm your email by clicking the button below.</p></div> </td> </tr><tr><td class=\"r7-c\" align=\"center\" style=\"align: center; padding-bottom: 15px; padding-top: 15px; valign: top;\"> <table cellspacing=\"0\" cellpadding=\"0\" border=\"0\" role=\"presentation\" width=\"300\" class=\"r8-o\" style=\"background-color: #0092FF; border-collapse: separate; border-color: #0092FF; border-radius: 4px; border-style: solid; border-width: 0px; table-layout: fixed; width: 300px;\"><tr><td height=\"18\" align=\"center\" valign=\"top\" class=\"r9-i nl2go-default-textstyle\" style=\"word-break: break-word; background-color: #0092FF; border-radius: 4px; color: #ffffff; font-family: arial,helvetica,sans-serif; font-size: 16px; font-style: normal; line-height: 1.15; padding-bottom: 12px; padding-top: 12px; text-align: center;\"> <a href=\"" + confirmEmailLink + "\" class=\"r10-r default-button\" target=\"_blank\" data-btn=\"1\" style=\"font-style: normal; font-weight: normal; line-height: 1.15; text-decoration: none; word-break: break-word; word-wrap: break-word; display: block; -webkit-text-size-adjust: none; color: #ffffff; font-family: arial,helvetica,sans-serif; font-size: 16px;\"> <span>Confirm your email</span></a> </td> </tr></table></td> </tr></table></td> </tr></table></th> </tr></table></td> </tr></table></td> </tr></table></body></html>";

            return response;
        }

        private string ForgotPasswordEmailFormat([Url] string forgotPasswordLink)
        {
            string response = "<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Strict//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd\">\r\n<html xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:v=\"urn:schemas-microsoft-com:vml\" xmlns:o=\"urn:schemas-microsoft-com:office:office\"><head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\"><meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\"><meta name=\"format-detection\" content=\"telephone=no\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\"><title>Forgot Password</title><style type=\"text/css\" emogrify=\"no\">#outlook a { padding:0; } .ExternalClass { width:100%; } .ExternalClass, .ExternalClass p, .ExternalClass span, .ExternalClass font, .ExternalClass td, .ExternalClass div { line-height: 100%; } table td { border-collapse: collapse; mso-line-height-rule: exactly; } .editable.image { font-size: 0 !important; line-height: 0 !important; } .nl2go_preheader { display: none !important; mso-hide:all !important; mso-line-height-rule: exactly; visibility: hidden !important; line-height: 0px !important; font-size: 0px !important; } body { width:100% !important; -webkit-text-size-adjust:100%; -ms-text-size-adjust:100%; margin:0; padding:0; } img { outline:none; text-decoration:none; -ms-interpolation-mode: bicubic; } a img { border:none; } table { border-collapse:collapse; mso-table-lspace:0pt; mso-table-rspace:0pt; } th { font-weight: normal; text-align: left; } *[class=\"gmail-fix\"] { display: none !important; } </style><style type=\"text/css\" emogrify=\"no\"> @media (max-width: 600px) { .gmx-killpill { content: ' \\03D1';} } </style><style type=\"text/css\" emogrify=\"no\">@media (max-width: 600px) { .gmx-killpill { content: ' \\03D1';} .r0-o { border-style: solid !important; margin: 0 auto 0 auto !important; width: 320px !important } .r1-i { background-color: #ffffff !important } .r2-o { border-style: solid !important; margin: 0 auto 0 auto !important; width: 100% !important } .r3-c { box-sizing: border-box !important; display: block !important; valign: top !important; width: 100% !important } .r4-o { border-style: solid !important; width: 100% !important } .r5-i { padding-left: 0px !important; padding-right: 0px !important } .r6-c { box-sizing: border-box !important; padding-bottom: 15px !important; padding-top: 15px !important; text-align: left !important; valign: top !important; width: 100% !important } .r7-c { box-sizing: border-box !important; padding: 0 !important; text-align: center !important; valign: top !important; width: 100% !important } .r8-o { border-style: solid !important; margin: 0 auto 0 auto !important; margin-bottom: 15px !important; margin-top: 15px !important; width: 100% !important } .r9-i { padding: 0 !important; text-align: center !important } .r10-r { background-color: #0092FF !important; border-radius: 4px !important; border-width: 0px !important; box-sizing: border-box; height: initial !important; padding: 0 !important; padding-bottom: 12px !important; padding-top: 12px !important; text-align: center !important; width: 100% !important } body { -webkit-text-size-adjust: none } .nl2go-responsive-hide { display: none } .nl2go-body-table { min-width: unset !important } .mobshow { height: auto !important; overflow: visible !important; max-height: unset !important; visibility: visible !important; border: none !important } .resp-table { display: inline-table !important } .magic-resp { display: table-cell !important } } </style><style type=\"text/css\">p, h1, h2, h3, h4, ol, ul, li { margin: 0; } a, a:link { color: #0092ff; text-decoration: underline } .nl2go-default-textstyle { color: #3b3f44; font-family: arial,helvetica,sans-serif; font-size: 16px; line-height: 1.5; word-break: break-word } .default-button { color: #ffffff; font-family: arial,helvetica,sans-serif; font-size: 16px; font-style: normal; font-weight: normal; line-height: 1.15; text-decoration: none; word-break: break-word } .default-heading1 { color: #1F2D3D; font-family: arial,helvetica,sans-serif; font-size: 36px; word-break: break-word } .default-heading2 { color: #1F2D3D; font-family: arial,helvetica,sans-serif; font-size: 32px; word-break: break-word } .default-heading3 { color: #1F2D3D; font-family: arial,helvetica,sans-serif; font-size: 24px; word-break: break-word } .default-heading4 { color: #1F2D3D; font-family: arial,helvetica,sans-serif; font-size: 18px; word-break: break-word } a[x-apple-data-detectors] { color: inherit !important; text-decoration: inherit !important; font-size: inherit !important; font-family: inherit !important; font-weight: inherit !important; line-height: inherit !important; } .no-show-for-you { border: none; display: none; float: none; font-size: 0; height: 0; line-height: 0; max-height: 0; mso-hide: all; overflow: hidden; table-layout: fixed; visibility: hidden; width: 0; } </style><!--[if mso]><xml> <o:OfficeDocumentSettings> <o:AllowPNG/> <o:PixelsPerInch>96</o:PixelsPerInch> </o:OfficeDocumentSettings> </xml><![endif]--><style type=\"text/css\">a:link{color: #0092ff; text-decoration: underline;}</style></head><body bgcolor=\"#ffffff\" text=\"#3b3f44\" link=\"#0092ff\" yahoo=\"fix\" style=\"background-color: #ffffff;\"> <table cellspacing=\"0\" cellpadding=\"0\" border=\"0\" role=\"presentation\" class=\"nl2go-body-table\" width=\"100%\" style=\"background-color: #ffffff; width: 100%;\"><tr><td> <table cellspacing=\"0\" cellpadding=\"0\" border=\"0\" role=\"presentation\" width=\"600\" align=\"center\" class=\"r0-o\" style=\"table-layout: fixed; width: 600px;\"><tr><td valign=\"top\" class=\"r1-i\" style=\"background-color: #ffffff;\"> <table cellspacing=\"0\" cellpadding=\"0\" border=\"0\" role=\"presentation\" width=\"100%\" align=\"center\" class=\"r2-o\" style=\"table-layout: fixed; width: 100%;\"><tr><th width=\"100%\" valign=\"top\" class=\"r3-c\" style=\"font-weight: normal;\"> <table cellspacing=\"0\" cellpadding=\"0\" border=\"0\" role=\"presentation\" width=\"100%\" class=\"r4-o\" style=\"table-layout: fixed; width: 100%;\"><tr><td valign=\"top\" class=\"r5-i\"> <table width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" role=\"presentation\"><tr><td class=\"r6-c nl2go-default-textstyle\" align=\"left\" style=\"color: #3b3f44; font-family: arial,helvetica,sans-serif; font-size: 16px; line-height: 1.5; word-break: break-word; padding-bottom: 15px; padding-top: 15px; text-align: left; valign: top;\"> <div><p style=\"margin: 0; text-align: center;\">Reset your PinkButter account password by clicking the button below.</p></div> </td> </tr><tr><td class=\"r7-c\" align=\"center\" style=\"align: center; padding-bottom: 15px; padding-top: 15px; valign: top;\"> <table cellspacing=\"0\" cellpadding=\"0\" border=\"0\" role=\"presentation\" width=\"300\" class=\"r8-o\" style=\"background-color: #0092FF; border-collapse: separate; border-color: #0092FF; border-radius: 4px; border-style: solid; border-width: 0px; table-layout: fixed; width: 300px;\"><tr><td height=\"18\" align=\"center\" valign=\"top\" class=\"r9-i nl2go-default-textstyle\" style=\"word-break: break-word; background-color: #0092FF; border-radius: 4px; color: #ffffff; font-family: arial,helvetica,sans-serif; font-size: 16px; font-style: normal; line-height: 1.15; padding-bottom: 12px; padding-top: 12px; text-align: center;\"> <a href=\"" + forgotPasswordLink + "\" class=\"r10-r default-button\" target=\"_blank\" data-btn=\"1\" style=\"font-style: normal; font-weight: normal; line-height: 1.15; text-decoration: none; word-break: break-word; word-wrap: break-word; display: block; -webkit-text-size-adjust: none; color: #ffffff; font-family: arial,helvetica,sans-serif; font-size: 16px;\"> <span>Reset your password</span></a> </td> </tr></table></td> </tr></table></td> </tr></table></th> </tr></table></td> </tr></table></td> </tr></table></body></html>";

            return response;
        }

        public async Task<int> SendPaymentNoticeToEmail(string recipientName, [EmailAddress] string recipientEmail, string checkoutUrl)
        {
            try
            {
                string? senderName = _configuration.GetValue<string>("Email:SenderName");
                string? senderEmail = _configuration.GetValue<string>("Email:SenderEmail");
                string? smtpHost = _configuration.GetValue<string>("Email:SMTPServer:Host");
                int? port = _configuration.GetValue<int>("Email:SMTPServer:Port");
                string? userName = _configuration.GetValue<string>("Email:SMTPServer:UserName");
                string? password = _configuration.GetValue<string>("Email:SMTPServer:Password");

                // Create the email message
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(senderName, senderEmail));
                message.To.Add(new MailboxAddress(recipientName, recipientEmail));
                message.Subject = "Payment Reminder: Action Required";

                // Create the email body content with the checkout URL
                message.Body = new TextPart("html")
                {
                    Text = PaymentNoticeEmailFormat(checkoutUrl)
                };

                using (var client = new SmtpClient())
                {
                    await client.ConnectAsync(smtpHost, port.Value, false);
                    await client.AuthenticateAsync(userName, password);
                    await client.SendAsync(message);
                    client.Disconnect(true);
                }
                return 0;
            }
            catch
            {
                return 1;
            }
        }

        // Update the PaymentNoticeEmailFormat to accept the checkout URL
        private string PaymentNoticeEmailFormat(string checkoutUrl)
        {
            string response = $@"<!DOCTYPE html>
<html xmlns=""http://www.w3.org/1999/xhtml"">
<head>
    <meta http-equiv=""Content-Type"" content=""text/html; charset=utf-8"">
    <title>Payment Reminder</title>
    <style>
        body {{ font-family: Arial, sans-serif; }}
        .content {{ max-width: 600px; margin: auto; padding: 20px; }}
        .button {{ background-color: #0092FF; color: white; padding: 10px 15px; text-decoration: none; border-radius: 5px; }}
    </style>
</head>
<body>
    <div class=""content"">
        <h1>Payment Reminder</h1>
        <p>Dear customer,</p>
        <p>This is a reminder to complete the payment for your order. Please make the payment at your earliest convenience to ensure your order remains active.</p>
        <p>If the payment is not completed, the order will be considered cancelled, and a refund will not be possible due to ingredients already used in the preparation process.</p>
        <p>Thank you for your understanding!</p>
        <p><a href=""{checkoutUrl}"" class=""button"">Pay Now</a></p>
    </div>
</body>
</html>";

            return response;
        }

    }
}
