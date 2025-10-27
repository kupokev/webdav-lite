# WebDAV Lite Server

A from-scratch WebDAV server implementation with configurable authentication to understand how the protocol works.

I used AI to produce the base code and have been modifying it for my use case. 
Feel free to use to play around with it. 
I'll likely be making changes to the project as I play around with this for my home lab.

Check the [Quickstart Guide](https://github.com/kupokev/webdav-lite/wiki/Quickstart-Guide) in the Wiki to see how it works. 


## Getting Started

**⚠️ IMPORTANT:** On first run, default credentials are created:
- Username: `admin`
- Password: `password`

**Change these immediately using the user management commands!**

## Configuration File

The server uses `config.json` for settings. It's created automatically on first run.

### Default Configuration
```json
{
  "RequireAuthentication": true,
  "ListenPrefix": "http://localhost:8080/",
  "StoragePath": "./webdav-storage",
  "UsersFile": "users.json"
}
```

### Configuration Options

| Setting | Description | Default | Notes |
|---------|-------------|---------|-------|
| `RequireAuthentication` | Enable/disable authentication | `true` | |
| `ListenPrefix` | URL to listen on | `http://localhost:8080/` | See binding notes below |
| `StoragePath` | Directory for file storage | `./webdav-storage` | |
| `UsersFile` | Path to users database | `users.json` | |

#### Network Binding Options

- `http://localhost:8080/` - Default, may bind to IPv6 only on Linux
- `http://127.0.0.1:8080/` - IPv4 localhost only (recommended for local access)
- `http://+:8080/` - All interfaces (IPv4 and IPv6) - requires sudo on Linux
- `http://0.0.0.0:8080/` - **NOT supported** on Linux (use `+` instead)

### Managing Configuration

View current configuration:
```bash
dotnet run -- config
```

Change settings:
```bash
# Enable/disable authentication
dotnet run -- config auth true
dotnet run -- config auth false

# Change listen address - Local IPv4 only
dotnet run -- config prefix http://127.0.0.1:8080/

# Change listen address - All interfaces (requires sudo)
dotnet run -- config prefix http://+:8080/

# Change storage directory
dotnet run -- config storage /var/webdav-data

# Change users file location
dotnet run -- config usersfile /etc/webdav/users.json
```

### ⚠️ Security Warning

If you set `RequireAuthentication` to `false`:
- **Anyone can access, upload, delete, and modify your files!**
- Only use this for:
  - Local development/testing
  - Behind a firewall
  - When you have other security measures in place (VPN, firewall rules, etc.)
- **NEVER expose an unauthenticated server to the internet!**


## How to Connect

### With Authentication Enabled (Default)

All clients will prompt for username and password when connecting.

#### Windows
1. Open File Explorer
2. Right-click "This PC" → "Add a network location"
3. Enter: `http://localhost:8080/`
4. Enter your username and password when prompted
5. Check "Remember my credentials" for convenience

#### macOS
1. Finder → Go → Connect to Server (⌘K)
2. Enter: `http://username:password@localhost:8080/`
   - Or: `http://localhost:8080/` and enter credentials when prompted

#### Linux
```bash
# Mount with davfs2
sudo mount -t davfs http://localhost:8080/ /mnt/webdav
# Enter credentials when prompted
```

### Without Authentication (RequireAuthentication: false)

Simply connect without providing credentials:
- Windows: `http://localhost:8080/`
- macOS: `http://localhost:8080/`
- Linux: `http://localhost:8080/`

## Connecting WebDAV Clients

If you experience "connection refused" errors on Linux:

1. **Configure the server for IPv4:**
   ```bash
   dotnet run -- config prefix http://127.0.0.1:8080/
   dotnet run
   ```

2. **In WebDAV settings, use:**
   - **WebDAV URL:** `http://127.0.0.1:8080`
   - **Username:** `admin`
   - **Password:** your password

Note: Use `127.0.0.1` (explicit IPv4) instead of `localhost` to avoid IPv6 binding issues.

### Other Devices on Your Network

To access from other devices (phones, tablets, other computers):

1. **Configure for all interfaces:**
   ```bash
   dotnet run -- config prefix http://+:8080/
   sudo dotnet run  # Requires elevated privileges
   ```

2. **Find your server's IP address:**
   ```bash
   hostname -I
   # OR
   ip addr show | grep "inet "
   ```

3. **Connect using:**
   - **URL:** `http://YOUR_IP_ADDRESS:8080`
   - **Username:** your username
   - **Password:** your password

Example: `http://192.168.1.100:8080`

## Authentication Implementation

### How Basic Authentication Works

1. **Client makes request without credentials**
   ```
   GET / HTTP/1.1
   ```

2. **Server responds with 401 Unauthorized** (if auth required)
   ```
   HTTP/1.1 401 Unauthorized
   WWW-Authenticate: Basic realm="WebDAV Server"
   ```

3. **Client sends credentials**
   ```
   GET / HTTP/1.1
   Authorization: Basic YWRtaW46cGFzc3dvcmQ=
   ```
   The value is Base64-encoded `username:password`

4. **Server validates and responds**
   - Valid: Processes request normally
   - Invalid: Returns 401 again
   - Auth disabled: Processes request without checking credentials

### Security Features

1. **Password Hashing**
   - Passwords are hashed using SHA256
   - Never stored in plain text
   - Hash: `Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password)))`

2. **Persistent Storage**
   - Users stored in `users.json` (or configured file)
   - Format:
   ```json
   {
     "admin": "XohImNooBHFR0OVvjcYpJ3NgPQ1qq73WKhHvch0VQtg=",
     "john": "5E884898DA28047151D0E56F8DC6292773603D0D6AABBDD62A11EF721D1542D8"
   }
   ```

3. **Request Validation**
   - Every request checks the `Authorization` header (when auth enabled)
   - Invalid/missing credentials → 401 response
   - The `WWW-Authenticate` header tells clients what auth method to use

### Code Implementation

**ServerConfig.cs:**
```csharp
public class ServerConfig
{
    public bool RequireAuthentication { get; set; } = true;
    // ... other settings
}
```

**WebDavServer.cs:**
```csharp
// Check authentication at start of every request
if (_requireAuth && !_authManager.ValidateCredentials(request.Headers["Authorization"]))
{
    response.StatusCode = 401;
    response.AddHeader("WWW-Authenticate", "Basic realm=\"WebDAV Server\"");
    return;
}
```

## Testing with curl

### With Authentication
```bash
# Upload a file
curl -u admin:password -T myfile.txt http://localhost:8080/

# Download a file
curl -u admin:password http://localhost:8080/myfile.txt

# List directory
curl -u admin:password -X PROPFIND http://localhost:8080/ -H "Depth: 1"
```

### Without Authentication (if disabled)
```bash
# Upload a file
curl -T myfile.txt http://localhost:8080/

# Download a file
curl http://localhost:8080/myfile.txt

# List directory
curl -X PROPFIND http://localhost:8080/ -H "Depth: 1"
```

## WebDAV Protocol Explained

### Core HTTP Methods

WebDAV extends HTTP with these additional methods:

#### 1. **PROPFIND** - Get Properties
Retrieves properties (metadata) about resources.

**Request Headers:**
- `Depth: 0` - Only the resource itself
- `Depth: 1` - Resource and its immediate children
- `Depth: infinity` - Resource and all descendants (rarely used)

#### 2. **MKCOL** - Make Collection
Creates a new directory (collection in WebDAV terminology).

#### 3. **COPY** - Copy Resource
Copies a file or directory to a new location.

#### 4. **MOVE** - Move Resource
Moves a file or directory.

#### 5. **DELETE** - Delete Resource
Deletes a file or directory.

#### 6. **PUT** - Upload File
Creates or updates a file.

#### 7. **GET** - Download File
Retrieves file content.

#### 8. **OPTIONS** - Discover Capabilities
Returns what the server supports.

## Production Deployment Considerations

### ⚠️ CRITICAL: Use HTTPS in Production!

**Basic Authentication sends credentials in Base64, which is NOT encryption!** Anyone intercepting the traffic can decode the credentials.

**You MUST use HTTPS/TLS when exposing to the internet!**

### Recommended Production Setup:

1. **Always Enable Authentication in Production**
   ```bash
   dotnet run -- config auth true
   ```

2. **Use a Reverse Proxy** (Nginx, Caddy, or Apache)
   ```nginx
   server {
       listen 443 ssl http2;
       server_name webdav.yourdomain.com;
       
       ssl_certificate /path/to/cert.pem;
       ssl_certificate_key /path/to/key.pem;
       
       location / {
           proxy_pass http://localhost:8080;
           proxy_set_header Host $host;
           proxy_set_header X-Real-IP $remote_addr;
           proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
           proxy_set_header X-Forwarded-Proto $scheme;
           
           # Increase timeout for large file uploads
           proxy_read_timeout 300s;
           proxy_send_timeout 300s;
           client_max_body_size 0;
       }
   }
   ```

3. **Get a Free SSL Certificate from Let's Encrypt**
   ```bash
   # Using Certbot
   sudo certbot --nginx -d webdav.yourdomain.com
   ```

4. **Firewall Configuration**
   ```bash
   # Only allow HTTPS
   sudo ufw allow 443/tcp
   
   # Block HTTP if not needed
   sudo ufw deny 80/tcp
   
   # Allow only from specific IP (optional)
   sudo ufw allow from 192.168.1.0/24 to any port 8080
   ```

5. **Run as a System Service** (systemd example)
   ```ini
   [Unit]
   Description=WebDAV Server
   After=network.target
   
   [Service]
   Type=simple
   User=webdav
   WorkingDirectory=/opt/webdav
   ExecStart=/usr/bin/dotnet /opt/webdav/WebDavServer.dll
   Restart=always
   RestartSec=10
   
   # Security hardening
   NoNewPrivileges=true
   PrivateTmp=true
   ProtectSystem=strict
   ProtectHome=true
   ReadWritePaths=/opt/webdav/webdav-storage
   
   [Install]
   WantedBy=multi-user.target
   ```

6. **Additional Security Measures**
   - **Use strong passwords** (minimum 16 characters, mixed case, numbers, symbols)
   - **Implement rate limiting** (via reverse proxy)
   - **Add IP whitelist** if possible
   - **Regular security updates**
   - **Monitor access logs**
   - **Consider fail2ban** to block brute force attempts
   - **Backup regularly**

### Rate Limiting Example (Nginx)

```nginx
# Limit to 10 requests per second per IP
limit_req_zone $binary_remote_addr zone=webdav:10m rate=10r/s;

server {
    location / {
        limit_req zone=webdav burst=20 nodelay;
        # ... proxy settings
    }
}
```

## Common Use Cases

### 1. Development/Testing (No Auth)
```bash
dotnet run -- config auth false
dotnet run
```
Great for local testing where you want quick access without credentials.

### 2. Home Network (With Auth)
```bash
dotnet run -- config auth true
dotnet run -- config prefix http://+:8080/
sudo dotnet run
```
Access from other devices on your home network with password protection.

### 3. Internet-Facing (HTTPS + Auth + Reverse Proxy)
```bash
dotnet run -- config auth true
dotnet run -- config prefix http://127.0.0.1:8080/
# Set up Nginx reverse proxy with SSL
# Configure firewall to only allow HTTPS
```
Full security for public internet access.

## Troubleshooting

**Issue:** Connection refused (ECONNREFUSED) on Linux
- **Cause:** Server bound to IPv6, client trying IPv4
- **Solution:** Use `dotnet run -- config prefix http://127.0.0.1:8080/` for IPv4
- **Alternative:** Use `http://127.0.0.1:8080` (not `localhost`) in your client

**Issue:** "The request is not supported" error when using 0.0.0.0
- **Cause:** HttpListener on Linux doesn't support `0.0.0.0` binding
- **Solution:** Use `http://+:8080/` instead for all interfaces

**Issue:** Can access without password even though auth is enabled
- Check `config.json` - ensure `RequireAuthentication` is `true`
- Restart the server after changing config
- Verify with: `dotnet run -- config`

**Issue:** "Authentication required" but credentials are correct
- Check the `users.json` file exists
- Try removing and re-adding the user: `dotnet run -- adduser username password`
- Ensure no typos in username/password

**Issue:** Windows keeps asking for credentials
- Make sure "Remember my credentials" is checked
- Try: `cmdkey /add:localhost /user:admin /pass:password`

**Issue:** curl shows "401 Unauthorized"
- Verify you're using `-u username:password`
- Check for typos in credentials
- Confirm auth is enabled: `dotnet run -- config`

**Issue:** Can't connect from other devices
- Check if `ListenPrefix` is set to `0.0.0.0` instead of `localhost`
- Update: `dotnet run -- config prefix http://0.0.0.0:8080/`
- Verify firewall allows connections on port 8080

**Issue:** Files being saved to a folder called "config" or "adduser"
- **You forgot the `--`!** Use: `dotnet run -- config auth true`
- Without `--`, dotnet interprets arguments incorrectly
- Delete the wrongly created folders and use the correct syntax

## Architecture Diagram

```
┌──────────────┐
│   Client     │
│ (File Mgr,   │
│  Browser,    │
│  curl, etc)  │
└──────┬───────┘
       │ HTTP Request + Authorization Header (if auth enabled)
       │
┌──────▼────────────────────────────────────┐
│         WebDavServer.cs                   │
│  ┌────────────────────────────────────┐   │
│  │  1. Check Configuration             │   │
│  │     - Is auth required?             │   │
│  └─────────────────┬──────────────────┘   │
│                    │                       │
│         ┌──────────▼──────────┐            │
│         │  Auth Required?     │            │
│         └──┬───────────────┬──┘            │
│           YES              NO               │
│            │                │               │
│  ┌─────────▼─────────┐     │               │
│  │  Validate Creds   │     │               │
│  │  with AuthManager │     │               │
│  └─────────┬─────────┘     │               │
│       Valid│   Invalid     │               │
│            │     │          │               │
│            │  ┌──▼──────┐  │               │
│            │  │ 401     │  │               │
│            │  │ Return  │  │               │
│            │  └─────────┘  │               │
│            │               │               │
│  ┌─────────▼───────────────▼───────────┐   │
│  │  2. Route Request                   │   │
│  │     - GET, PUT, DELETE, MKCOL...    │   │
│  └─────────────────┬───────────────────┘   │
│                    │                       │
│  ┌─────────────────▼───────────────────┐   │
│  │  3. File System Operations          │   │
│  │     - Read/Write files              │   │
│  │     - Create/Delete directories     │   │
│  └─────────────────────────────────────┘   │
└───────────────────────────────────────────┘
       │
┌──────▼────────────────────┐
│  ServerConfig.cs          │
│  ┌─────────────────────┐  │
│  │  config.json        │  │
│  │  {                  │  │
│  │    "RequireAuth":   │  │
│  │      true/false     │  │
│  │  }                  │  │
│  └─────────────────────┘  │
└───────────────────────────┘
       │
┌──────▼────────────────────┐
│  AuthenticationManager    │
│  ┌─────────────────────┐  │
│  │  users.json         │  │
│  │  {                  │  │
│  │    "admin": "hash"  │  │
│  │  }                  │  │
│  └─────────────────────┘  │
└───────────────────────────┘
```

## What's Missing (Advanced Features)

This is a learning implementation. Production servers might add:

1. **Digest Authentication** (more secure than Basic)
2. **OAuth/Bearer Tokens**
3. **Resource Locking** (LOCK/UNLOCK methods)
4. **ETags** for caching
5. **Quota Management** (per-user storage limits)
6. **Access Control Lists** (per-user/per-directory permissions)
7. **Audit Logging** (track who accessed what)
8. **Multi-factor Authentication**
9. **Session Management**
10. **CORS Headers** (for web clients)

## Resources

- [RFC 4918 - WebDAV Specification](https://datatracker.ietf.org/doc/html/rfc4918)
- [RFC 7617 - HTTP Basic Authentication](https://datatracker.ietf.org/doc/html/rfc7617)
- [OWASP Authentication Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)
