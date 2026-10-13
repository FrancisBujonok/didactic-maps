using MapaMundos;
using MapaMundos.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Dónde se "monta" la aplicación dentro de wwwroot/index.html
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Un único ProjectService para toda la sesión: guarda el proyecto abierto y lo persiste.
builder.Services.AddSingleton<ProjectService>();

await builder.Build().RunAsync();
