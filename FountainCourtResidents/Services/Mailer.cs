using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Configuration;
using System.Net.Mail;
using System.Web;

namespace FountainCourtResidents.Services
{
    public static class Mailer
    {
        public static void SendHtml(string to, string subject, string htmlBody)
        {
            var smtpSection = (SmtpSection)ConfigurationManager.GetSection("system.net/mailSettings/smtp");
            var from = string.IsNullOrWhiteSpace(smtpSection.From) ? "no-reply@localhost" : smtpSection.From;

            using (var msg = new MailMessage(from, to))
            {
                msg.Subject = subject;
                msg.Body = htmlBody;
                msg.IsBodyHtml = true;
                using (var client = new SmtpClient()) // reads host/port/ssl/credentials from web.config
                {
                    client.Send(msg);
                }
            }
        }
    }
}