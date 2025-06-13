using Microsoft.EntityFrameworkCore;
using NhaNghiYenNhi.Models;
using NhaNghiYenNhi.Services;
using NhaNghiYenNhi.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add HttpClient for Groq API
builder.Services.AddHttpClient();

// Register ChatbotService
builder.Services.AddScoped<IChatbotService, ChatbotService>();

// Register ChatbotAgent services
builder.Services.AddScoped<IChatbotAgentService, ChatbotAgentService>();
builder.Services.AddScoped<IActionService, ActionService>();

// Add SignalR
builder.Services.AddSignalR();

// Configure CORS for SignalR
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.WithOrigins("http://localhost:5157", "https://localhost:5157")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials() // Required for SignalR
                  .SetIsOriginAllowed(origin => true); // Cho phép mọi domain khi deploy
        });
});

// Configure DbContext
builder.Services.AddDbContext<MyDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Enable CORS
app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Map SignalR Hub
app.MapHub<ChatHub>("/chathub");
app.MapHub<OrderHub>("/orderHub");

app.Run();