using AutoMapSharp.Sample.Web.Infrastructure;
using AutoMapSharp.Sample.Web.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Add MVC Controllers and Views
builder.Services.AddControllersWithViews();

// 2. Register AutoMapSharp and the services its resolvers and mapping actions use.
builder.Services.AddSampleMappingServices();

// Register Phase 6 SQLite Database Context for EF Core Projections
builder.Services.AddDbContext<SampleDbContext>(options =>
    options.UseSqlite("Data Source=sample_automapsharp.db"));

var app = builder.Build();

// Ensure SQLite database is created and seeded
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
    db.Database.EnsureCreated();
    db.SeedInitialData();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Partial class for testing
public partial class Program { }
