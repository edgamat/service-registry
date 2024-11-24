using System.Net.NetworkInformation;
using System.Text.RegularExpressions;

namespace ServiceRegistry.Abstractions;

public static partial class AddressResolver
{
    private static readonly Regex HostNameExpression = MyRegex();

    public static string Resolve(string configurationServiceAddress)
    {
        if (HostNameExpression.IsMatch(configurationServiceAddress))
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();
            
            return HostNameExpression.Replace(configurationServiceAddress, properties.HostName);
        }

        return configurationServiceAddress;
    }

    [GeneratedRegex("{HOSTNAME}", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex MyRegex();
}