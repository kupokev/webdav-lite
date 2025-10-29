using System.Text.Json;

namespace WebDavLite.Services
{
    internal class ConfigService
    {
        public bool RequireAuthentication { get; set; } = true;
        public string ListenPrefix { get; set; } = "http://localhost:8080/";
        public string StoragePath { get; set; } = "./webdav-storage";
        public string UsersFile { get; set; } = "users.json";

        public static ConfigService Load(string configPath)
        {
            if (!File.Exists(configPath))
            {
                Console.WriteLine($"Configuration file not found: {configPath}");
                Console.WriteLine("Creating default configuration...");

                var defaultConfig = new ConfigService();
                defaultConfig.Save(configPath);

                return defaultConfig;
            }

            try
            {
                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<ConfigService>(json);

                if (config == null)
                {
                    Console.WriteLine("Failed to parse configuration. Using defaults.");
                    return new ConfigService();
                }

                Console.WriteLine($"Configuration loaded from {configPath}");
                Console.WriteLine($"  - RequireAuthentication: {config.RequireAuthentication}");
                Console.WriteLine($"  - ListenPrefix: {config.ListenPrefix}");
                Console.WriteLine($"  - StoragePath: {config.StoragePath}");
                Console.WriteLine($"  - UsersFile: {config.UsersFile}");

                return config;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading configuration: {ex.Message}");
                Console.WriteLine("Using default configuration.");
                return new ConfigService();
            }
        }

        public void Save(string configPath)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                var json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(configPath, json);
                Console.WriteLine($"Configuration saved to {configPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving configuration: {ex.Message}");
            }
        }
    }
}
