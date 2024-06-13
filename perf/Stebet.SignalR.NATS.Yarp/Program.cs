WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
WebApplication app = builder.Build();
app.MapReverseProxy();
app.Run();
