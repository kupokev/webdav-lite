using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WebDavLite.Services
{
    internal class AuthenticationService
    {
        private readonly Dictionary<string, string> _users; // username -> hashed password
        private readonly string _configPath;

        public AuthenticationService(string configPath)
        {
            _configPath = configPath;
            _users = new Dictionary<string, string>();
            LoadUsers();
        }

        /// <summary>
        /// Validates credentials using Basic Authentication
        /// </summary>
        public bool ValidateCredentials(string? authHeader)
        {
            if (string.IsNullOrEmpty(authHeader))
                return false;

            if (!authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                var encodedCredentials = authHeader.Substring(6).Trim();
                var credentialBytes = Convert.FromBase64String(encodedCredentials);
                var credentials = Encoding.UTF8.GetString(credentialBytes);
                var parts = credentials.Split(':', 2);

                if (parts.Length != 2)
                    return false;

                var username = parts[0];
                var password = parts[1];

                return ValidateUser(username, password);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates a username and password
        /// </summary>
        public bool ValidateUser(string username, string password)
        {
            if (!_users.ContainsKey(username))
                return false;

            var hashedPassword = HashPassword(password);
            return _users[username] == hashedPassword;
        }

        /// <summary>
        /// Adds a new user or updates existing user's password
        /// </summary>
        public void AddUser(string username, string password)
        {
            _users[username] = HashPassword(password);
            SaveUsers();
        }

        /// <summary>
        /// Removes a user
        /// </summary>
        public bool RemoveUser(string username)
        {
            if (_users.Remove(username))
            {
                SaveUsers();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Lists all usernames
        /// </summary>
        public IEnumerable<string> GetUsernames()
        {
            return _users.Keys;
        }

        /// <summary>
        /// Hashes a password using SHA256
        /// </summary>
        private string HashPassword(string password)
        {
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = SHA256.HashData(bytes);
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Loads users from the config file
        /// </summary>
        private void LoadUsers()
        {
            if (!File.Exists(_configPath))
            {
                // Create default user if no config exists
                Console.WriteLine("No user configuration found. Creating default user...");
                AddUser("admin", "password");
                Console.WriteLine("Default credentials: admin / password");
                Console.WriteLine("CHANGE THESE IMMEDIATELY!");
                return;
            }

            try
            {
                var json = File.ReadAllText(_configPath);
                var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (loaded != null)
                {
                    foreach (var kvp in loaded)
                    {
                        _users[kvp.Key] = kvp.Value;
                    }
                    Console.WriteLine($"Loaded {_users.Count} user(s) from {_configPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading users: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves users to the config file
        /// </summary>
        private void SaveUsers()
        {
            try
            {
                var json = JsonSerializer.Serialize(_users, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_configPath, json);
                Console.WriteLine($"Saved {_users.Count} user(s) to {_configPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving users: {ex.Message}");
            }
        }
    }
}
