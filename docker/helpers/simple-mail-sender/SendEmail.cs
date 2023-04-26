using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net;
using Microsoft.Extensions.Configuration;
using System.Net.Mail;
using System.Reflection;

namespace simple_mail_sender
{
    public static class SendEmail
    {
        [FunctionName("SendEmail")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log, ExecutionContext context)
        {
            try
            {
                log.LogInformation("C# HTTP trigger function processed a request.");


                var config = new ConfigurationBuilder()
                   .SetBasePath(context.FunctionAppDirectory)
                   .AddJsonFile("host.json", optional: true, reloadOnChange: true)
                   .AddUserSecrets(Assembly.GetExecutingAssembly())
                   .AddEnvironmentVariables()
                   .Build();

                log.LogInformation("C# HTTP trigger function processed a request.");

                string secretsSource = config["SECRETSOURCE"];
                string senderEmail = config["EMAILSENDER"];
                string emailPassword = config["EMAILPASSWORD"];
                string smptServer = config["SMTPSERVER"];
                int smptPort = int.Parse(config["SMPTPORT"]);
                bool enableSSL = bool.Parse(config["ENABLESSL"]);
                bool useDefaultCredentials = bool.Parse(config["USEDEFAULTCREDENTIALS"]);

                string apiKey = config["API_KEY"];
                if (apiKey == null || !req.Headers.TryGetValue("x-api-key", out var providedApiKey) || providedApiKey != apiKey)
                {
                    return new UnauthorizedResult();
                }

                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);
                string emailRecepient = data?.recepient;
                string emailSubject = data?.subject;
                string emailBody = data?.body;


                log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

                // SMTP server settings
                SmtpClient smtpClient = new SmtpClient(smptServer, smptPort);
                smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
                smtpClient.EnableSsl = enableSSL;
                smtpClient.UseDefaultCredentials = useDefaultCredentials;
                smtpClient.Credentials = new NetworkCredential(senderEmail, emailPassword);

                // Email message
                MailMessage mailMessage = new MailMessage();
                mailMessage.From = new MailAddress(senderEmail);
                mailMessage.To.Add(emailRecepient);
                mailMessage.Subject = emailSubject;
                mailMessage.Body = emailBody;

                log.LogInformation($"Sending email from '{senderEmail}' to benonesimulescu2017@gmail.com");

                // Send the email
                smtpClient.Send(mailMessage);
                log.LogInformation("Email sent successfully.");

                return new OkObjectResult($"Mail sent from email '{senderEmail}' to '{emailRecepient}' with the subhect : {emailSubject}");
            }
            catch (Exception ex)
            {
                log.LogError(ex.Message);

                return new ObjectResult("Error sending email : " + ex.Message)
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError
                };
            }

        }
    }
}
