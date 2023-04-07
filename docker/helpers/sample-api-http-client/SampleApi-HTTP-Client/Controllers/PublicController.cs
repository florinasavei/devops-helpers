using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager.AppService;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Rest;
using System.Net;
using System.Net.Http;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Management.ResourceManager.Models;
using Microsoft.Rest.Azure.OData;

namespace PublicPrivateApisWithDns.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PublicController : ControllerBase
    {

        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly IHttpContextAccessor _httpContextAccessor;


        private readonly ILogger<PublicController> _logger;

        public PublicController(ILogger<PublicController> logger, IHttpClientFactory httpClientFactory, IConfiguration config, IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
            _config = config;
            _httpContextAccessor = httpContextAccessor;
        }

        [HttpGet("/")]
        public ActionResult<string> ItWorks()
        {
            var apiName = _config.GetValue<string>("ApiName");
            return $"Public api '{apiName}' works";
        }

        [HttpGet("/test")]
        public ActionResult<string> Test()
        {
            var apiName = _config.GetValue<string>("ApiName");
            return $"Public api '{apiName}' test endpoint works";
        }

        [HttpGet("/ip")]
        public IActionResult PrintLocalIp()
        {
            var apiName = _config.GetValue<string>("ApiName");

            try
            {
                var localipAddressFromContextAccesor = _httpContextAccessor?.HttpContext?.Connection.LocalIpAddress;
                var remoreIpAddressFromContextAccesor = _httpContextAccessor?.HttpContext?.Connection.RemoteIpAddress;

                return Ok(new
                {
                    localIp = localipAddressFromContextAccesor?.ToString(),
                    removeIp = remoreIpAddressFromContextAccesor?.ToString(),
                    apiName = apiName,
                });
            }
            catch (Exception ex)
            {
                return StatusCode(501, $"Error for '{apiName}': {ex.Message}");
            }
        }

        [HttpGet("/ip-hostname")]
        public IActionResult GetHostnameIp([FromQuery] string hostname = "google.com")
        {
            var apiName = _config.GetValue<string>("ApiName");

            try
            {
                string privateAzureIpEnv = Environment.GetEnvironmentVariable("WEBSITE_PRIVATE_IP");

                IPAddress[] ipAddressesFomEnv = hostname != null ? Dns.GetHostAddresses(hostname) : new IPAddress[0];
                return Ok(new { host = hostname, privateAzureIpEnv = privateAzureIpEnv, hostNameIps = ipAddressesFomEnv.Select(t => t.ToString()).ToList(), apiName = apiName, });
            }
            catch (Exception ex)
            {
                return StatusCode(501, $"Error for '{apiName}': {ex.Message}");
            }
        }

        [HttpGet("/azure/ip")]
        public async Task<IActionResult> AppServiceIps()
        {
            var apiName = _config.GetValue<string>("ApiName");

            try
            {
                string hostName = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");
                string privateIpEnv = Environment.GetEnvironmentVariable("WEBSITE_PRIVATE_IP");

                IPAddress[] ipAddressesFomEnv = hostName != null ? Dns.GetHostAddresses(hostName) : new IPAddress[0];
                List<IPAddress> ipAddressesFromMachine = System.Net.Dns.GetHostEntry(System.Environment.MachineName).AddressList.ToList();

                return Ok(new { host = hostName, privateIpEnv = privateIpEnv, hostNameIps = ipAddressesFomEnv.Select(t => t.ToString()).ToList(), machineIps = ipAddressesFromMachine.Select(t => t.ToString()).ToList(), apiName = apiName, });

            }
            catch (Exception ex)
            {
                return StatusCode(501, $"Error for '{apiName}': {ex.Message}");
            }
        }

        public class AppServNetworkInfo
        {
            public string outboundIps { get; set; }
            public string subnetId { get; set; }
        }

        [HttpGet("/azure/{rg}/network")]
        public async Task<IActionResult> GetResourceGroupDetails([FromRoute] string rg)
        {
            var apiName = _config.GetValue<string>("ApiName");

            try
            {
                var credential = new DefaultAzureCredential();
                var armClient = new ArmClient(credential);

                SubscriptionResource subscription = await armClient.GetDefaultSubscriptionAsync();
                ResourceGroupCollection resourceGroups = subscription.GetResourceGroups();
                ResourceGroupResource resourceGroup = await resourceGroups.GetAsync(rg);


                var allVnetsInResourceGroup = resourceGroup.GetGenericResources(filter: "resourceType eq 'Microsoft.Network/virtualNetworks'").ToList();

                Dictionary<string, AppServNetworkInfo?> allOutboundAppServiceIpsInRG = new Dictionary<string, AppServNetworkInfo?>();

                await foreach (var appServicePlan in resourceGroup.GetAppServicePlans())
                {
                    await foreach (var appService in appServicePlan.GetWebAppsAsync())
                    {
                        Console.WriteLine(appService.Name);
                        Console.WriteLine(appService.OutboundIPAddresses);
                        Console.WriteLine(appService.VirtualNetworkSubnetId);

                        if (appService.VirtualNetworkSubnetId != null)
                        {
                            var genericVnetResourceWithoutData = armClient.GetGenericResource(appService.VirtualNetworkSubnetId.Parent);
                            var vnet = allVnetsInResourceGroup.Find(t => t.Id == genericVnetResourceWithoutData.Id);
                        }

                        allOutboundAppServiceIpsInRG.Add(appService.Name, new AppServNetworkInfo { outboundIps = appService.OutboundIPAddresses, subnetId = appService?.VirtualNetworkSubnetId?.Name });
                    }
                }


                return Ok(new { allOutboundAppServiceIpsInRG = allOutboundAppServiceIpsInRG, vnets = allVnetsInResourceGroup });

            }
            catch (Exception ex)
            {
                return StatusCode(501, $"Error for '{apiName}': {ex.Message}");
            }
        }

        [HttpGet("/index.html")]
        public ActionResult<string> FakeIndex()
        {
            var apiName = _config.GetValue<string>("ApiName");

            return $"Public api '{apiName}' fake index.html works";
        }


        [HttpGet("/proxy-any-url")]
        public async Task<IActionResult> GetAnyUrl([FromQuery] string url = "https://google.com")
        {
            var apiName = _config.GetValue<string>("ApiName");

            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {

                    var rawResponseContent = await response.Content.ReadAsStringAsync();
                    return Ok(new { responseHostIp = TryGetesponseHostnameIp(response), rawResponseContent = rawResponseContent, responesHeaders = response.Headers, apiName = apiName, });
                }
                else
                {
                    return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());
                }
            }
            catch (Exception ex)
            {
                return StatusCode(501, $"Error for '{apiName}': {ex.Message}");
            }
        }


        private string TryGetesponseHostnameIp(HttpResponseMessage response)
        {
            string firstServerName = "";
            try
            {
                var headers = response.Headers.ToList();
                var serverFromHeader = headers.FirstOrDefault(t => t.Key == "Server");
                firstServerName = serverFromHeader.Value?.First() ?? "";
                IPAddress[] responseServerAddresses = Dns.GetHostAddresses(firstServerName) ?? new IPAddress[0];
                string responsesServerIpAddressString = responseServerAddresses.Any() ? responseServerAddresses[0].ToString() : "";

                return $"{firstServerName} =  {responsesServerIpAddressString}";
            }
            catch (Exception ex)
            {
                return $"ERROR finding ip for host '{firstServerName}' : {ex.Message}";
            }
        }
    }
}