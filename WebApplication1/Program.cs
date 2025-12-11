using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// 讀取 appsettings.json 中的 OpenAI:ApiKey (用於 Azure OpenAI 的 API Key)
var openApiKey = builder.Configuration.GetSection("OpenAI:ApiKey").Value;
// 讀取 Azure OpenAI 的配置
var azureOpenAIEndpoint = builder.Configuration.GetSection("AzureOpenAI:Endpoint").Value;
var azureOpenAIDeploymentName = builder.Configuration.GetSection("AzureOpenAI:DeploymentName").Value;

// 註冊 IKernel 服務，並配置 OpenAI 連接器
builder.Services.AddSingleton<Kernel>(serviceProvider =>
{
    var kernelBuilder = Kernel.CreateBuilder();

    if (!string.IsNullOrEmpty(openApiKey) &&
        !string.IsNullOrEmpty(azureOpenAIEndpoint) &&
        !string.IsNullOrEmpty(azureOpenAIDeploymentName))
    {
        // 使用 AddAzureOpenAIChatCompletion 註冊 Azure OpenAI 模型
        kernelBuilder.AddAzureOpenAIChatCompletion(
            deploymentName: azureOpenAIDeploymentName,
            endpoint: azureOpenAIEndpoint,
            apiKey: openApiKey
        );
    }
    else
    {
        System.Diagnostics.Debug.WriteLine("⚠️ Warning: Missing Azure OpenAI configuration (API Key, Endpoint, or Deployment Name). Semantic Kernel AI features will fail.");
    }

    return kernelBuilder.Build();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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
