
// Program.cs (最終兼容版本)

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// 憑證資訊
var endpoint = "https://aoai-skmh-uswest-dev-01.openai.azure.com/";
var apiKey = "15922fa38b6b40b38267dc66d638809c";
var model = "gpt-4o-mini-FJU"; // deploymentName

// 註冊 Kernel 服務
builder.Services.AddSingleton<Kernel>(serviceProvider =>
{
    var kernelBuilder = Kernel.CreateBuilder();

    // ⭐ 核心變更：直接使用 endpoint, deploymentName, 和 apiKey 字串 
    // 這樣可以避免 'System.ClientModel.ApiKeyCredential' 的版本衝突
    kernelBuilder.AddAzureOpenAIChatCompletion(
        deploymentName: model,
        endpoint: endpoint,
        apiKey: apiKey
    );

    return kernelBuilder.Build();
});


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

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();