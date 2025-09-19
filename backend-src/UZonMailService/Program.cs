using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Models;
using Quartz;
using System.Reflection;
using System.Security.Claims;
using Uamazing.Utils.Plugin;
using UZonMail.DB.MySql;
using UZonMail.DB.PostgreSql;
using UZonMail.DB.SQL;
using UZonMail.DB.SqLite;
using UZonMail.Utils.Database.Redis;
using UZonMail.Utils.Log;
using UZonMail.Utils.Web;
using UZonMail.Utils.Web.Filters;
using UZonMail.Utils.Web.Token;
using UZonMailService.Middlewares;

// �޸ĵ�ǰĿ¼
Directory.SetCurrentDirectory(AppContext.BaseDirectory);

// ����Ĭ�ϵ������ļ�
var productConfig = "appsettings.Production.json";
if (!File.Exists(productConfig))
{
    File.WriteAllText(productConfig, "{\n}");
}

// ���� quartz ���ݿ�
var quartzDb = "data/db/quartz-sqlite.sqlite3";
if (!File.Exists(quartzDb))
{
    Directory.CreateDirectory(Path.GetDirectoryName(quartzDb));
    File.Copy("Quartz/quartz-sqlite.sqlite3", quartzDb);
}

var appOptions = new WebApplicationOptions
{
    ApplicationName = typeof(Program).Assembly.FullName,
    ContentRootPath = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory),
    WebRootPath = "wwwroot",
    Args = args
};
var builder = WebApplication.CreateBuilder(appOptions);
var services = builder.Services;


// �鿴��ǰ����
Console.WriteLine($"Current Environment��{builder.Environment.EnvironmentName}");


// ��ֻ֤��һ��ʵ��
// services.UseSingleApp();

// ��־
services.AddLogging(loggingBuilder =>
{
    loggingBuilder.ClearProviders();
    loggingBuilder.AddLog4Net();
});
// ��� http ��־
services.AddHttpLogging(logging =>
{
    logging.LoggingFields = HttpLoggingFields.RequestProperties
        | HttpLoggingFields.RequestHeaders
        | HttpLoggingFields.ResponseHeaders;
    logging.RequestBodyLogLimit = 4096;
    logging.ResponseBodyLogLimit = 4096;
});
//// log4net: https://github.com/huorswords/Microsoft.Extensions.Logging.Log4Net.AspNetCore/blob/develop/samples/Net8.0/WebApi/log4net.config
//// �ο���https://github.com/huorswords/Microsoft.Extensions.Logging.Log4Net.AspNetCore/blob/develop/samples/Net8.0/WebApi/Program.cs
//builder.Logging.ClearProviders();
//builder.Logging.AddLog4Net();
// �� logging ����־����ӳ�䵽 log4net
builder.AttachLevelToLog4Net();


// ��� httpClient
services.AddHttpClient();

// Add services to the container.
var mvcBuilder = services.AddControllers(option =>
{
    // ���ȫ���쳣����
    option.Filters.Add(new KnownExceptionFilter());
    option.Filters.Add(new TokenExpiredFilter());
})
.AddNewtonsoftJson(x =>
{
    x.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
});

// ���ز��
var pluginLoader = new PluginLoader("Plugins");
pluginLoader.AddApplicationPart(mvcBuilder);

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
services.AddEndpointsApiExplorer();

// ���� swagger
services.AddSwaggerGen(new OpenApiInfo()
{
    Title = "UZonMail API",
    Contact = new OpenApiContact()
    {
        Name = "galens",
        Url = new Uri("https://galens.uamazing.cn"),
        Email = "260827400@qq.com"
    },    
});



// ��֤�� jwt ��ʵ��
// ��� signalR������Ҫ�� app ��ʹ�� MapHub
// �ο�: https://learn.microsoft.com/en-us/aspnet/core/tutorials/signalr?view=aspnetcore-8.0&tabs=visual-studio
services.AddSignalR();
// Change to use Name as the user identifier for SignalR
// WARNING: This requires that the source of your JWT token 
// ensures that the Name claim is unique!
// If the Name claim isn't unique, users could receive messages 
// intended for a different user!
// ��ʹ���Զ���� IUserIdProvider��ʹ��Ĭ�ϵģ���֤ token �� claim �а��� ClaimTypes.Name,
//builder.Services.AddSingleton<IUserIdProvider, NameUserIdProvider>();

// ���� hyphen-case ·��
services.SetupSlugifyCaseRoute();

// ע�����ݿ�
services.AddSqlContext<SqlContext, PostgreSqlContext, MySqlContext, SqLiteContext>(builder.Configuration);

// ��� HttpContextAccessor���Թ� service ��ȡ��ǰ������û���Ϣ
services.AddHttpContextAccessor();

// ��ʱ����
services.Configure<QuartzOptions>(builder.Configuration.GetSection("Quartz"));
// if you are using persistent job store, you might want to alter some options
services.Configure<QuartzOptions>(options =>
{
    options.Scheduling.IgnoreDuplicates = true; // default: false
    options.Scheduling.OverWriteExistingData = true; // default: true
});
// ע�뵽 ioc ��
services.AddQuartz();
// ��������
services.AddQuartzHostedService(
    q => q.WaitForJobsToComplete = false);

// ���� jwt ��֤
var tokenParams = new TokenParams();
builder.Configuration.GetSection("TokenParams").Bind(tokenParams);
var redisConfig = new RedisConnectionConfig();
builder.Configuration.GetSection("Database:Redis").Bind(redisConfig);
services.AddJWTAuthentication(tokenParams.UniqueSecret, redisConfig);

// ���ýӿڼ�Ȩ����
services.AddAuthorizationBuilder()
    // ����
    .AddPolicy("RequireAdmin", policy => policy.RequireClaim(ClaimTypes.Role, "admin"));

// �رղ����Զ�����
services.Configure<ApiBehaviorOptions>(o =>
{
    o.SuppressModelStateInvalidFilter = true;
});

// ����
services.AddCors(options =>
{
    var configuration = builder.Configuration;
    // ��ȡ��������
    string[]? corsConfig = configuration.GetSection("Cors").Get<string[]>();

    options.AddDefaultPolicy(
        policy =>
        {
            policy.WithOrigins([.. corsConfig])
            .AllowAnyMethod()
            .AllowAnyHeader();
        });
});

// �޸��ļ��ϴ���С����
services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = int.MaxValue;
});

// ���� Kestrel ������
// Ĭ�ϼ�����ַͨ�� Urls ���� 
builder.WebHost.ConfigureKestrel(options =>
{
    bool listenAnyIP = builder.Configuration.GetSection("Http:ListenAnyIP").Get<bool>();
    int port = builder.Configuration.GetSection("Http:Port").Get<int>();
    if (listenAnyIP)
        options.ListenAnyIP(port);
    else
        options.ListenLocalhost(port);

    options.Limits.MaxRequestBodySize = int.MaxValue;
});

// ���ر�������
services.AddServices();

// ���ز������
pluginLoader.UseServices(builder);

var app = builder.Build();

app.UseDefaultFiles();
// ������վ�ĸ�Ŀ¼
app.UseStaticFiles(new StaticFileOptions()
{
    ServeUnknownFileTypes = true,
    OnPrepareResponse = (ctx) =>
    {
        // �ر���� .well-known �����
        if (ctx.Context.Request.Path.StartsWithSegments("/.well-known"))
        {
            ctx.Context.Response.ContentType = "text/plain";
        }
    }
});

// ���� public Ŀ¼Ϊ��̬�ļ�Ŀ¼
var publicPath = Path.Combine(builder.Environment.ContentRootPath, "data/public");
Directory.CreateDirectory(publicPath);
app.UseStaticFiles(new StaticFileOptions()
{
    FileProvider = new PhysicalFileProvider(publicPath),
    RequestPath = "/public"
});

// ����
app.UseCors();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}

app.UseAuthentication();
app.UseAuthorization();

// vue ��ҳ��Ӧ���м��
app.UseVueASP();

// http ·��
app.MapControllers();

pluginLoader.UseApp(app);

app.Run();

