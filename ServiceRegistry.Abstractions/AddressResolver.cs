using System.Net.NetworkInformation;
using System.Text.RegularExpressions;

namespace ServiceRegistry.Abstractions;

public static class AddressResolver
{
    private static readonly Regex HostNameExpression = new("{HOSTNAME}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string Resolve(string configurationServiceAddress)
    {
        if (HostNameExpression.IsMatch(configurationServiceAddress))
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();
            
            return HostNameExpression.Replace(configurationServiceAddress, properties.HostName);
        }

        return configurationServiceAddress;
    }
}