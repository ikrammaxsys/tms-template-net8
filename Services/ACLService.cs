using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using AuthACL.CentralAuth.Models;
using Microsoft.AspNetCore.Http;

namespace tms_template_net8.Services;

public sealed class ACLService : IACLService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ACLService> _logger;

    public ACLService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ACLService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }
}
