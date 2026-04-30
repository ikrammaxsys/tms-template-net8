using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;

namespace tms_template_net8.Jwt;

public static class RsaKeyLoader
{
    /// <summary>
    /// Load RSA key from a PEM file (Jwt:RsaKeyPath).
    /// </summary>
    public static RSA LoadRsaKey(IConfiguration config, IWebHostEnvironment env)
    {
        var path = config["Jwt:RsaKeyPath"];
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException(
                "JWT RS256 requires Jwt:RsaKeyPath (path to PEM file).");

        var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(env.ContentRootPath ?? ".", path);
        var rsaKeyPem = File.ReadAllText(fullPath);
        var rsaKey = RSA.Create();
        rsaKey.ImportFromPem(rsaKeyPem);
        return rsaKey;
    }
}
