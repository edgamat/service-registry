// See https://aka.ms/new-console-template for more information

using dotnet_etcd;
using dotnet_etcd.interfaces;
using EtcdServiceRegistry;
using EtcdServiceRegistryApp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder();

builder.Services.AddOptions<ServiceRegistryConfiguration>().Bind(builder.Configuration.GetSection("ServiceRegistry"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<ServiceRegistryConfiguration>>().Value);

builder.Services.AddSingleton<IEtcdServiceRegistry, EtcdServiceRegistryImpl>();

builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<HeartbeatService>();

builder.Services.AddSingleton<IEtcdClient>(sp =>
{
    var connectionString = sp.GetRequiredService<IOptions<ServiceRegistryConfiguration>>().Value.ConnectionString;
    return new EtcdClient(connectionString);
});
builder.Services.AddSingleton<ServiceRegistry>();

var app = builder.Build();

var registry = app.Services.GetRequiredService<ServiceRegistry>();

await registry.RegisterServiceAsync(CancellationToken.None);

// var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

// lifetime.ApplicationStopped.Register(() =>
// {
//     registry.UnregisterServiceAsync(CancellationToken.None).Wait();
// });

app.Run();