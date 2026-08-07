using System.Text;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using TimeOffApi.Data;
using TimeOffApi.Domain;
using TimeOffApi.Infrastructure;
using TimeOffApi.Services;

var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsDevelopment()
    && string.IsNullOrWhiteSpace(builder.Configuration["OPENAI_API_KEY"]))
{
    var localApiKey = ReadLocalOpenAiApiKey(
        Path.Combine(builder.Environment.ContentRootPath, ".env"));
    if (!string.IsNullOrWhiteSpace(localApiKey))
        builder.Configuration["OPENAI_API_KEY"] = localApiKey;
}

builder.Services.AddControllers(options =>
    {
        options.ModelBindingMessageProvider.SetValueMustNotBeNullAccessor(
            _ => "A required value was not provided.");
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var message = string.Join(" ", context.ModelState.Values
                .SelectMany(x => x.Errors)
                .Select(x => string.IsNullOrWhiteSpace(x.ErrorMessage)
                    ? "The request is invalid."
                    : x.ErrorMessage)
                .Distinct());
            return new BadRequestObjectResult(new
            {
                statusCode = StatusCodes.Status400BadRequest,
                code = "VALIDATION_ERROR",
                message,
                traceId = context.HttpContext.TraceIdentifier
            });
        };
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter the JWT returned by POST /api/auth/login."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document, null)] = []
    });
});
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IUserLockService, UserLockService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IManagerScopeResolver, ManagerScopeResolver>();
builder.Services.AddScoped<IManagerAssistantCapabilitiesService, ManagerAssistantCapabilitiesService>();
builder.Services.AddScoped<IDirectReportResolver, DirectReportResolver>();
builder.Services.AddScoped<IManagerAssistantTeamToolService, ManagerAssistantTeamToolService>();
builder.Services.AddScoped<IManagerAssistantOrchestrator, ManagerAssistantOrchestrator>();
builder.Services.AddSingleton<IManagerAssistantRateLimiter, ManagerAssistantRateLimiter>();
builder.Services.AddSingleton<UnconfiguredAssistantModelClient>();
builder.Services.AddSingleton<OpenAiAssistantModelClient>();
builder.Services.AddSingleton<IAssistantModelClient>(ResolveAssistantModelClient);
builder.Services.AddSingleton<IAssistantModelAvailability>(services =>
    (IAssistantModelAvailability)services.GetRequiredService<IAssistantModelClient>());
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITimeClockService, TimeClockService>();
builder.Services.AddScoped<ITimeLogService, TimeLogService>();
builder.Services.AddScoped<ITeamTimeReportingService, TeamTimeReportingService>();
builder.Services.AddScoped<ITimeReportingService, TimeReportingService>();
builder.Services.AddScoped<ITimeLogExportService, TimeLogExportService>();
builder.Services.AddSingleton<ITimeLogWorkbookWriter, TimeLogWorkbookWriter>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<ITimeOffRequestRepository, TimeOffRequestRepository>();
builder.Services.AddScoped<ITimeOffRequestService, TimeOffRequestService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

var databaseProvider = builder.Configuration["DatabaseProvider"] ?? "Sqlite";
var connectionString = builder.Configuration.GetConnectionString(databaseProvider)
    ?? throw new InvalidOperationException($"Connection string '{databaseProvider}' is not configured.");
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        options.UseSqlServer(connectionString);
    else if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        options.UseSqlite(connectionString);
    else
        throw new InvalidOperationException("DatabaseProvider must be Sqlite or SqlServer.");
});

builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddOptions<ManagerAssistantOptions>()
    .BindConfiguration(ManagerAssistantOptions.SectionName)
    .Validate(
        ManagerAssistantOptions.HasValidLimits,
        "ManagerAssistant limits are outside the supported range.")
    .Validate(
        ManagerAssistantOptions.HasRequiredProviderSettings,
        "ManagerAssistant Provider and Model are required when the feature is enabled.")
    .ValidateOnStart();
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is missing.");
if (Encoding.UTF8.GetByteCount(jwt.Key) < 32)
    throw new InvalidOperationException("Jwt:Key must contain at least 32 bytes.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                await ApiErrorWriter.WriteAsync(
                    context.Response,
                    StatusCodes.Status401Unauthorized,
                    "UNAUTHORIZED",
                    "A valid access token is required.",
                    context.HttpContext.TraceIdentifier);
            },
            OnForbidden = context => ApiErrorWriter.WriteAsync(
                context.Response,
                StatusCodes.Status403Forbidden,
                "FORBIDDEN",
                "You do not have permission to access this resource.",
                context.HttpContext.TraceIdentifier)
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseStaticFiles();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await DatabaseSchemaInitializer.EnsureManagerSchemaAsync(db);
    await BootstrapAdmin.SeedAsync(scope.ServiceProvider, app.Configuration);
    await DevelopmentDataSeeder.SeedAsync(
        scope.ServiceProvider, app.Configuration, app.Environment);
}

app.Run();

static string? ReadLocalOpenAiApiKey(string path)
{
    if (!File.Exists(path))
        return null;

    foreach (var rawLine in File.ReadLines(path))
    {
        var line = rawLine.Trim();
        if (line.Length == 0 || line.StartsWith('#'))
            continue;
        if (line.StartsWith("export ", StringComparison.Ordinal))
            line = line[7..].TrimStart();

        var separatorIndex = line.IndexOf('=');
        if (separatorIndex <= 0
            || !line[..separatorIndex].Trim().Equals(
                "OPENAI_API_KEY",
                StringComparison.Ordinal))
        {
            continue;
        }

        var value = line[(separatorIndex + 1)..].Trim();
        if (value.Length >= 2
            && ((value[0] == '"' && value[^1] == '"')
                || (value[0] == '\'' && value[^1] == '\'')))
        {
            value = value[1..^1];
        }

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    return null;
}

static IAssistantModelClient ResolveAssistantModelClient(IServiceProvider services)
{
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ManagerAssistantOptions>>();
    return OpenAiAssistantModelClient.IsConfiguredProvider(options.Value)
        ? services.GetRequiredService<OpenAiAssistantModelClient>()
        : services.GetRequiredService<UnconfiguredAssistantModelClient>();
}

public partial class Program;
