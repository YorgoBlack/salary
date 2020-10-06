using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MailKit.Net.Smtp;
using MimeKit;
using SalaryApp.Models;

namespace SalaryApp
{

    public class MailSendWorker
    {
        public void SendPaidOut(CancellationToken cancellationToken = default(CancellationToken))
        {
            AppDbContext db = new AppDbContext();
            SmtpClient SmtpClient = new SmtpClient();

            while (true)
            {
                try
                {
                    var emails = (from x in db.JobForMailings select x.UserPayment.User.Email).Distinct().ToList();

                    if ( !emails.Any() )
                    {
                        return;
                    }

                    foreach (var email in emails)
                    {
                        Thread.Sleep(60 * 1000);

                        var jobs = (from x in db.JobForMailings select x).Where(x => x.UserPayment.User.Email == email).ToList();
                        string lines = "";
                        foreach (var job in jobs)
                        {
                            lines += "проект " + job.UserPayment.Project.Name + " сумма " + job.UserPayment.Sum + " грн" + Environment.NewLine;
                        }

                        var message = new MimeMessage();
                        message.From.Add(new MailboxAddress(WebConfig.SmtpUserName));
                        message.To.Add(new MailboxAddress(email));
                        message.Subject = "Начисление премии";
                        message.Body = new TextPart("plain")
                        {
                            Text = "Вам начислено" + Environment.NewLine + lines + 
                                Environment.NewLine + "Для детального просмотра перейдите на портал премий"
                        };

                        if (!SmtpClient.IsConnected)
                        {
                            SmtpClient.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => { return true; };
                            SmtpClient.Connect(WebConfig.SmtpHost, WebConfig.SmtpPort);
                            SmtpClient.AuthenticationMechanisms.Remove("XOAUTH2");
                            SmtpClient.Authenticate(WebConfig.SmtpUserName, WebConfig.SmtpUserPass);
                        }

                        if (SmtpClient.IsConnected)
                        {
                            SmtpClient.Send(message);
                            foreach (var job in jobs)
                            {
                                db.JobForMailings.Remove(job);
                            }

                            db.SaveChanges();
                        }
                    }

                    Thread.Sleep(5 * 60 * 1000);
                }
                catch (Exception ex)
                {
                    ProcessCancellation();
                }
            }
        }
        private void ProcessCancellation()
        {
            Thread.Sleep(10000);
        }
    }
}