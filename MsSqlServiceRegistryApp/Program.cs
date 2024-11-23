using MsSqlServiceRegistry;
using MsSqlServiceRegistryApp;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddUserSecrets<Program>();

builder.Services.AddHostedService<Worker>();

builder.Services.AddServiceRegistry(builder.Configuration);

var host = builder.Build();

host.Run();