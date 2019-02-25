# Windows 10 IoT Core App Updates Service
We needed a way to remotely update .NET Core 2.2 apps (not UWP apps) installed on IoT devices running Windows 10 IoT Core.  We created a standard Windows service that periodically reads an app update manifest from a remote host and updates locally installed apps on the device as needed.  Please note that this service was designed for and has only been tested with .NET Core 2.2 binaries.

### Current Features
1. The app update manifest and associated app update packages can be hosted on any website, Azure storage, Dropbox, One Drive, Google Drive, etc.
2. Configurable daily check time (defaults to 3am). 
3. The service can update multiple apps and has "rollback" or "downgrade" capabilities.
4. Package integrity is validated using a MD5 hash.
5. The update manager will automatically "kill" processes being updated and restart them after they've been updated.  Please see the section below regarding security.
6. The server side administrative website functionality can be integrated into any existing ASP.NET Core 2.2 project by simply adding the Hyprsoft.IoT.AppUpdates.Web NuGet package and making the code and configuration changes noted below.
7. If the Hyprsoft.IoT.AppUpdates.Web NuGet package is utilized, update package endpoints are automatically protected using built-in bearer token authentication.  This prevents unauthorized downloads of your app packages.
8. Credentials for accessing the administrative website can be supplied using Azure App Service settings or Azure Key Vault.

### Process to Update an App
#### Automatically
1. Integrate the administrative website NuGet package into your existing ASP.NET Core 2.2 website and manage updates and packages via your browser.  See the sample configuration, code, and administrative website screenshots below.

#### Manually
1. Add a new package definition to the app update manifest with an incremented file version, new release date, new source URI, and new checksum.
2. Upload the app update package (i.e. zip file) containing the updated application files (EXEs, DLLs, etc.) to your desired "host" (i.e  website or file sharing service).

### Sample Service Configuration
The service configuration Json file resides on each device.  <b>Note: If the administrative website NuGet package is used then ClientId, ClientSecret, and Scope are required.</b>.
```json
{
  "ClientCredentials": {
    "ClientId": "<optional clientid>",
    "ClientSecret": "<optional client secret>",
    "Scope": "appupdates"
  },
  "CheckTime": "03:00:00",
  "NextCheckDate": "2018-10-17T03:00:00",
  "ManifestUri": "http://www.yourdomain.com/app-updates-manifest.json",
  "InstalledApps": [
    {
      "ApplicationId": "04fc007e-db18-430f-b4fa-f5b54de1e142",
      "InstallUri": "c:\\data\\testapp"
    }
  ]
}
```
### Sample App Update Manifest
The app update manifest Json file and app packages can reside on a website or any file sharing service.
```json
[
  {
    "Id": "04fc007e-db18-430f-b4fa-f5b54de1e142",
    "Name": "My Awesome App",
    "Description": "My Awesome App Description",
    "ExeFilename": "hyprsoft.my.awesome.app.exe",
    "VersionFilename": "hyprsoft.my.awesome.app.dll",
    "CommandLine": "param1",
    "Packages": [
      {
        "Id": "02902554-4d3d-4159-b106-41e0ac158733",
        "IsAvailable": true,
        "Version": "1.0.0.0",
        "ReleaseDateUtc": "2018-09-01T00:00:00",
        "SourceUri": "http://www.yourdomain.com/appupdates/apps/04fc007e-db18-430f-b4fa-f5b54de1e142/packages/download/hyprsoft.my.awesome.app-1000.zip",
        "Checksum": "bddf0cd85b9b4985fb10a1555e10ab6d"
      },
      {
        "Id": "02902554-4d3d-4159-b106-41e0ac158733",
        "IsAvailable": true,
        "Version": "1.0.1.0",
        "ReleaseDateUtc": "2018-09-16T00:00:00",
        "SourceUri": "http://www.yourdomain.com/appupdates/apps/04fc007e-db18-430f-b4fa-f5b54de1e142/packages/download/hyprsoft.my.awesome.app-1010.zip",
        "Checksum": "e6276631b2c67664810132f1ee565d59"
      }
    ]
  }
]
```
### Sample Program.cs
If you would like to incorporate the app updates administrative website functionality into your own website, you need to install the Hyprsoft.Iot.AppUpdates.Web NuGet package and configure your program.cs like below.  Once integrated and deployed, the administrative website can be accessed using http[s]://[www.your-domain.com]/appupdates.
```csharp
public class Program
{
    public static void Main(string[] args)
    {
        CreateWebHostBuilder(args).Build().Run();
    }

    public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
        WebHost.CreateDefaultBuilder(args)
            .UseStartup<Startup>()
            .UseAppUpdates();
}
```
### Sample Startup.cs
If you have incorporated the app updates administrative website functionality into your own website, you need to make the following code changes.
```csharp
public class Startup
{
    public Startup(IConfiguration configuration, IHostingEnvironment env)
    {
        Configuration = configuration;
        HostingEnvironment = env;
    }

    public IConfiguration Configuration { get; }

    public IHostingEnvironment HostingEnvironment { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddAppUpdates(options =>
        {
            options.ManifestUri = new Uri(Path.Combine(HostingEnvironment.WebRootPath, UpdateManager.DefaultManifestFilename));
            options.ClientCredentials.ClientId = Configuration["AppUpdates:ClientId"];
            options.ClientCredentials.ClientSecret = Configuration["AppUpdates:ClientSecret"];
            options.ClientCredentials.Scope = options.TokenOptions.Audience;
            options.TokenOptions.SecurityKey = Configuration["AppUpdates:SecurityKey"];
        });
        services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
    }

    public void Configure(IApplicationBuilder app)
    {
        if (HostingEnvironment.IsDevelopment())
            app.UseDeveloperExceptionPage();
        else
            app.UseExceptionHandler("/Home/Error");

        app.UseAppUpdates();
        app.UseMvc(routes => routes.MapRoute(name: "default", template: "{controller=Home}/{action=Index}/{id?}"));
    }
}
```
### Sample Web.config for Hosting in IIS/Azure App Service
If you have incorporated the app updates administrative website functionality into your own website, you need to make the following web.config configuration changes.
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.web>
    <!-- in kilobytes-->
    <httpRuntime maxRequestLength="51200" />
  </system.web>
  <system.webServer>
    <security>
      <requestFiltering>
        <!--in bytes-->
        <requestLimits maxAllowedContentLength="52428800" />
      </requestFiltering>
    </security>
  </system.webServer>
</configuration>
```
### General Usage Notes
1. When dealing with versions of EXE and DLL files it is important to note that the "File Version" is used as opposed to the "Product Version". 

![File Properties](https://github.com/hyprsoftcorp/IoTCoreAppUpdates/blob/master/Media/file-properties.jpg)

2. When the update manager searches for the latest package to install, the package's ReleasedDateUtc is used instead of the assembly file version.  So the package with the most recent ReleasedDateUtc will be chosen regardless of the assembly file version.
3. File checksums can be manually calculated using the built in Windows 10 certutil.exe utility.  Ex: certutil.exe -hashfile hyprsoft.my.awesome.app-1010.zip MD5.

### Security Concerns
By default the app update service runs on the IoT device under the 'NT AUTHORITY\SYSTEM' user context and has full rights/access to the operating and file systems.  This means that the processes the service invokes after an update also run under the same unrestricted user context. **This can be a security risk!  Use at your own risk!**

### Administrative Website Credentials
The administrative website credentials should be provided via standard Azure App Service settings.
#### App Service Settings
Setting Name | Example Value | Description
--- | --- | ---
AppUpdates:Username | myusername | Cookie Authentication Username
AppUpdates:Password | myp@ssw0rd | Cookie Authentication Password
AppUpdates:ClientId | myclientid | Bearer Token Authentication Client Id
AppUpdates:ClientSecret | mys3cr3t | Bearer Token Authentication Client Secret
AppUpdates:SecurityKey | C2225C6A-3B98-4002-B1F1-DBD1B92B33D1 | Bearer Token Authentication Symmetric Signing Key

### Administrative Website Screenshots
#### Login
![Login](https://github.com/hyprsoftcorp/IoTCoreAppUpdates/blob/master/Media/login-thumb.png)

#### List of defined Apps
![List of defined Apps](https://github.com/hyprsoftcorp/IoTCoreAppUpdates/blob/master/Media/apps-list-thumb.png)

#### Add a new App
![Add a new App](https://github.com/hyprsoftcorp/IoTCoreAppUpdates/blob/master/Media/apps-add-thumb.png)

#### Edit an existing App
![Edit an existing App](https://github.com/hyprsoftcorp/IoTCoreAppUpdates/blob/master/Media/apps-edit-thumb.png)

#### Add an app update package
![Add an app update package](https://github.com/hyprsoftcorp/IoTCoreAppUpdates/blob/master/Media/packages-add-thumb.png)

#### Edit an app update package
![Edit an app update package](https://github.com/hyprsoftcorp/IoTCoreAppUpdates/blob/master/Media/packages-edit-thumb.png)
