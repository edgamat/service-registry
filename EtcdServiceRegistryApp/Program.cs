// See https://aka.ms/new-console-template for more information

using EtcdServiceRegistry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder();

builder.Services.AddOptions<ServiceRegistryConfiguration>().Bind(builder.Configuration.GetSection("ServiceRegistry"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<ServiceRegistryConfiguration>>().Value);

builder.Services.AddSingleton<IEtcdServiceRegistry, EtcdServiceRegistryImpl>();

var app = builder.Build();

var registry = app.Services.GetRequiredService<IEtcdServiceRegistry>();

await registry.RegisterServiceAsync(CancellationToken.None);

var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

lifetime.ApplicationStopped.Register(() =>
{
    registry.UnregisterServiceAsync(CancellationToken.None).Wait();
});

app.Run();