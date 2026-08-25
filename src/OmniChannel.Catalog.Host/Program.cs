using OmniChannel.Catalog.Data;
using OmniChannel.Catalog.Host.Api;
using OmniChannel.Catalog.Host.HostedServices;
using OmniChannel.Catalog.Host.Infrastructure;
using OmniChannel.Catalog.Host.Realtime;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDb"));
builder.Services.Configure<ChannelSimulatorSettings>(builder.Configuration.GetSection("ChannelSimulator"));

builder.Services.AddCatalogData();

builder.Services.AddSingleton(sp => new SimulatorSwitch(sp.GetRequiredService<IOptions<ChannelSimulatorSettings>>().Value.Enabled));

builder.Services.AddSignalR().AddJsonProtocol(options =>
    options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);
builder.Services.Configure<HostOptions>(options => options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

builder.Services.AddHostedService<MongoIndexInitializer>();
builder.Services.AddHostedService<ProjectProductsService>();
builder.Services.AddHostedService<ProjectVariantsService>();
builder.Services.AddHostedService<ProjectListingsProprietaryService>();
builder.Services.AddHostedService<ProjectListingsObservedService>();
builder.Services.AddHostedService<BroadcastProductsService>();
builder.Services.AddHostedService<BroadcastVariantsService>();
builder.Services.AddHostedService<BroadcastListingsService>();
builder.Services.AddHostedService<ChannelSimulatorService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapEditorEndpoints();
app.MapQueryEndpoints();
app.MapAdminEndpoints();
app.MapHub<CatalogHub>("/hub/catalog");

app.Run();