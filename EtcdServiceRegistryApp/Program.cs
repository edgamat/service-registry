using EtcdServiceRegistry;
using EtcdServiceRegistryApp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder();

builder.Services.AddServiceRegistry(builder.Configuration);

builder.Services.AddSingleton<IEtcdServiceRegistry, EtcdServiceRegistryImpl>();

builder.Services.AddHostedService<Worker>();

var app = builder.Build();

//app.UseServiceRegistry();

app.Run();