using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace MonitorScraper
{
    static class Mailer
    {
        public static void Notify()
        {
            Tuple<string, string, decimal> monitor = DBInstance.GetLowestPrice();
            if(monitor.Item3 != -1)
                SendMail($"<div><h5>{monitor.Item1}</h5><a href=\"{monitor.Item2}\">{monitor.Item2}</a>: {monitor.Item3}</div>",
                         ConfigurationManager.AppSettings["EmailSubjectSuccess"].ToString());
        }

        public static void SendError(string msg)
        {
            SendMail($"<div>{msg}</div>",
                     ConfigurationManager.AppSettings["EmailSubjectError"].ToString());
        }

        public static void SendMail(string msg, string subject)
        {
            try
            {
                // Build message
                MailMessage message = new MailMessage
                {
                    From = new MailAddress(ConfigurationManager.AppSettings["Email"].ToString(), ConfigurationManager.AppSettings["FromDisplay"].ToString()),
                    Subject = subject,
                    IsBodyHtml = true,
                    Body = msg
                };
                message.To.Add(new MailAddress(ConfigurationManager.AppSettings["Email"]));

                // Build client
                SmtpClient client = new SmtpClient(ConfigurationManager.AppSettings["Host"], Convert.ToInt32(ConfigurationManager.AppSettings["Port"]))
                {
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(ConfigurationManager.AppSettings["Email"], ConfigurationManager.AppSettings["Password"]),
                    EnableSsl = Convert.ToBoolean(ConfigurationManager.AppSettings["EnableSsl"])
                };

                // Send message
                client.Send(message);
            }
            catch (Exception e)
            {
                throw e;
            }
        }
    }
}
