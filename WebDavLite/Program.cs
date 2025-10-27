using WebDavLite.Services;

namespace WebDavLite
{
    internal class Program
    {
        //static void Main(string[] args)
        static async Task Main(string[] args)
        {
            const string CONFIG_FILE = "config.json";

            // DEBUG: Print received arguments
            Console.WriteLine($"[DEBUG] Received {args.Length} arguments:");
            for (int i = 0; i < args.Length; i++)
            {
                Console.WriteLine($"[DEBUG]   args[{i}] = '{args[i]}'");
            }
            Console.WriteLine();

            // Load configuration
            var config = ConfigService.Load(CONFIG_FILE);

            // Initialize authentication manager
            var authManager = new AuthenticationService(config.UsersFile);

            // Check for commands (must be first argument)
            if (args.Length > 0)
            {
                switch (args[0].ToLower())
                {
                    case "adduser":
                        if (args.Length < 3)
                        {
                            Console.WriteLine("Usage: dotnet run -- adduser <username> <password>");
                            return;
                        }
                        authManager.AddUser(args[1], args[2]);
                        Console.WriteLine($"User '{args[1]}' added successfully.");
                        return;

                    case "removeuser":
                        if (args.Length < 2)
                        {
                            Console.WriteLine("Usage: dotnet run -- removeuser <username>");
                            return;
                        }
                        if (authManager.RemoveUser(args[1]))
                        {
                            Console.WriteLine($"User '{args[1]}' removed successfully.");
                        }
                        else
                        {
                            Console.WriteLine($"User '{args[1]}' not found.");
                        }
                        return;

                    case "listusers":
                        Console.WriteLine("Registered users:");
                        foreach (var username in authManager.GetUsernames())
                        {
                            Console.WriteLine($"  - {username}");
                        }
                        return;

                    case "config":
                        if (args.Length == 1)
                        {
                            Console.WriteLine("\nCurrent Configuration:");
                            Console.WriteLine($"  RequireAuthentication: {config.RequireAuthentication}");
                            Console.WriteLine($"  ListenPrefix: {config.ListenPrefix}");
                            Console.WriteLine($"  StoragePath: {config.StoragePath}");
                            Console.WriteLine($"  UsersFile: {config.UsersFile}");
                            Console.WriteLine();
                            Console.WriteLine("Usage: dotnet run -- config <setting> <value>");
                            Console.WriteLine("Settings:");
                            Console.WriteLine("  auth <true|false>    - Enable/disable authentication");
                            Console.WriteLine("  prefix <url>         - Set listen prefix (e.g., http://localhost:8080/)");
                            Console.WriteLine("  storage <path>       - Set storage directory path");
                            Console.WriteLine("  usersfile <path>     - Set users file path");
                            return;
                        }

                        if (args.Length < 3)
                        {
                            Console.WriteLine("Usage: dotnet run -- config <setting> <value>");
                            return;
                        }

                        switch (args[1].ToLower())
                        {
                            case "auth":
                                if (bool.TryParse(args[2], out bool authValue))
                                {
                                    config.RequireAuthentication = authValue;
                                    config.Save(CONFIG_FILE);
                                    Console.WriteLine($"Authentication requirement set to: {authValue}");
                                    Console.WriteLine("Restart the server for changes to take effect.");
                                }
                                else
                                {
                                    Console.WriteLine("Invalid value. Use 'true' or 'false'");
                                }
                                break;
                            case "prefix":
                                config.ListenPrefix = args[2];
                                config.Save(CONFIG_FILE);
                                Console.WriteLine($"Listen prefix set to: {args[2]}");
                                Console.WriteLine("Restart the server for changes to take effect.");
                                break;
                            case "storage":
                                config.StoragePath = args[2];
                                config.Save(CONFIG_FILE);
                                Console.WriteLine($"Storage path set to: {args[2]}");
                                Console.WriteLine("Restart the server for changes to take effect.");
                                break;
                            case "usersfile":
                                config.UsersFile = args[2];
                                config.Save(CONFIG_FILE);
                                Console.WriteLine($"Users file set to: {args[2]}");
                                Console.WriteLine("Restart the server for changes to take effect.");
                                break;
                            default:
                                Console.WriteLine($"Unknown setting: {args[1]}");
                                Console.WriteLine("Valid settings: auth, prefix, storage, usersfile");
                                break;
                        }
                        return;

                    default:
                        Console.WriteLine($"Unknown command: {args[0]}");
                        Console.WriteLine();
                        Console.WriteLine("Available commands:");
                        Console.WriteLine("  dotnet run -- adduser <username> <password>");
                        Console.WriteLine("  dotnet run -- removeuser <username>");
                        Console.WriteLine("  dotnet run -- listusers");
                        Console.WriteLine("  dotnet run -- config [setting] [value]");
                        Console.WriteLine();
                        Console.WriteLine("To start the server, just run: dotnet run");
                        return;
                }
            }

            // No command specified - start the server
            // Create and start server
            var server = new WebDavLite.Services.WebDaveServerService(
                config.ListenPrefix,
                config.StoragePath,
                config.RequireAuthentication,
                authManager
            );

            Console.WriteLine();
            Console.WriteLine("=== WebDAV Server ===");
            Console.WriteLine($"Authentication: {(config.RequireAuthentication ? "ENABLED" : "DISABLED")}");
            if (config.RequireAuthentication)
            {
                Console.WriteLine($"Users file: {config.UsersFile}");
            }
            else
            {
                Console.WriteLine("⚠️  WARNING: Authentication is DISABLED! Server is accessible to anyone!");
            }
            Console.WriteLine($"Storage path: {config.StoragePath}");
            Console.WriteLine($"Configuration file: {CONFIG_FILE}");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  dotnet run -- adduser <username> <password>");
            Console.WriteLine("  dotnet run -- removeuser <username>");
            Console.WriteLine("  dotnet run -- listusers");
            Console.WriteLine("  dotnet run -- config [setting] [value]");
            Console.WriteLine();

            // Handle Ctrl+C gracefully
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("\nShutting down...");
                server.Stop();
            };

            await server.StartAsync();

        }
    }
}
