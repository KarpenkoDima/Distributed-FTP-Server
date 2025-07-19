using FtpServer.Auth.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Our service UserService
builder.Services.AddScoped<IUserService, UserService>();

builder.Services.AddEndpointsApiExplorer(); // Essential for Swagger to discover endpoints

// Configure Swagger
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Version = "V1",
        Title = "FtpServer Auth API",
        Description = "Auth service for Ftp Server",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Dima Karpenko, dimak",
            Email = "dimka2010@gmail.com",
            Url = new Uri("https://github.com/karpenkodima")
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Auth API V1");

        /*
         *  http://localhost:5000/ to serve Swagger UI at the application's root 
         */
        options.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
