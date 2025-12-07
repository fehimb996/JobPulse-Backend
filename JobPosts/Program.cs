using FluentValidation;
using FluentValidation.AspNetCore;
using Hangfire;
using JobPosts.Configurations;
using JobPosts.Data;
using JobPosts.Handlers;
using JobPosts.Hangfire;
using JobPosts.Interfaces;
using JobPosts.Models;
using JobPosts.Repositories;
using JobPosts.Services;
using JobPosts.Validators;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAutoMapper(cfg => {
    cfg.AddMaps(Assembly.GetExecutingAssembly());
});
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<JobPostsDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddSwaggerConfiguration();
builder.Services.AddControllers();

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssembly(typeof(RegisterCommandValidator).Assembly);

builder.Services.AddDbContext<JobPostsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<AdzunaRepository>();

builder.Services.AddHangfireServer();

builder.Services.Configure<JobPosts.Options.AdzunaOptions>(
    builder.Configuration.GetSection("Adzuna"));

builder.Services.AddSingleton<JobPosts.Providers.IAdzunaCredentialProvider, JobPosts.Providers.AdzunaCredentialProvider>();

builder.Services.AddHttpClient();
builder.Services.AddHttpClient<JobPosts.Services.AdzunaService>();;
builder.Services.AddScoped<JobPostCleanupRunner>();

builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGeneratorAndRefresh>();

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblies(
        typeof(RegisterCommandHandler).Assembly,
        Assembly.GetExecutingAssembly()
    );
});

builder.Services.AddOptions<EmailSettings>()
    .Bind(builder.Configuration.GetSection("EmailSettings"))
    .ValidateOnStart();

builder.Services.AddHangfire(config =>
    config.UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddTransient<AdzunaJobRunner>();
builder.Services.AddTransient<CareerjetJobRunner>();
builder.Services.AddTransient<CombinedJobRunner>();
builder.Services.AddTransient<IEmailService, EmailService>();

builder.Services.AddMemoryCache();
builder.Services.AddCacheManagement();

builder.Services.AddHttpContextAccessor();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
            "http://localhost:5173",
            "https://peria-pulse-fe-dev-e8dhcvh6g3d4hhh8.westeurope-01.azurewebsites.net")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Peria Pulse API V1");
    c.RoutePrefix = "swagger";
    c.EnableTryItOutByDefault();
});

app.UseHangfireDashboard();
JobScheduler.RegisterRecurringJobs();

app.UseCors("AllowFrontend");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
