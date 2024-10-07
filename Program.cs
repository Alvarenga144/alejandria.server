using Alejandria.Server.Models;
using Alejandria.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// This section its for MongoDB connection
builder.Services.Configure<MongoDBSettings>(builder.Configuration.GetSection("MongoDB"));
builder.Services.AddSingleton<UserService>();
builder.Services.AddSingleton<PostService>();
builder.Services.AddSingleton<ProductService>();
builder.Services.AddSingleton<NotificationService>();

// This section its for Azure OpenAI and Azure Search Document as a service in a RAG Arch
builder.Services.AddSingleton<EmbedService>();
builder.Services.AddSingleton<ChatService>();
builder.Services.AddSingleton<VoiceChatService>();
builder.Services.AddSingleton<DocumentService>();
// builder.Services.AddSingleton<GenerateResponseRAG>();

// Other services (custom)
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<JwtAuthService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();

// This section its for Azure Blob Storage
builder.Services.AddTransient<IAvatarsBlobConfiguration, AvatarsBlobConfiguration>();
builder.Services.AddTransient<IPostContentBlobConfiguration, PostContentBlobConfiguration>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("/index.html");

app.Run();
