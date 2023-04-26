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
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log, ExecutionContext context)
        {
            try
            {
                log.LogInformation("C# HTTP trigger function processed a request.");

                string name = req.Query["name"];

                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);
                name = name ?? data?.name;


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
                mailMessage.To.Add("benonesimulescu2017@gmail.com");
                mailMessage.Subject = "Azure Function Email Test";
                mailMessage.Body = "This is a test email sent from an Azure Function.";

                // Send the email
                smtpClient.Send(mailMessage);
                log.LogInformation("Email sent successfully.");

                string responseMessage = string.IsNullOrEmpty(name)
                    ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                    : $"Hello, {name}. This HTTP triggered function executed successfully.";

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseMessage)
                };
            }
            catch (Exception ex)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("Error sending email : " + ex.Message)
                };
            }

        }
    }
}
