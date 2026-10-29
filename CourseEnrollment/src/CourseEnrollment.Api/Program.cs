using CourseEnrollment.Core.Services;
using CourseEnrollment.Core.Storage;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// In-memory store must be Singleton to preserve state across requests
builder.Services.AddSingleton<ICourseOfferingRepository, CourseOfferingRepository>();
builder.Services.AddScoped<ICourseEnrollmentService, CourseEnrollmentService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();