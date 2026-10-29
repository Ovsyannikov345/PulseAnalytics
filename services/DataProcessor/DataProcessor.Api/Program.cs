var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/ping", () =>
{
    return "pong";
})
.WithName("Ping");

await app.RunAsync();
