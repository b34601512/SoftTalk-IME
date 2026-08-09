using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace SoftTalkIme.Core.Sync;

public sealed class SoftTalkImeSyncConnection
{
    public SoftTalkImeSyncConnection(string baseUrl, string token, string source)
    {
        BaseUrl = baseUrl;
        Token = token;
        Source = source;
    }

    public string BaseUrl { get; }

    public string Token { get; }

    public string Source { get; }
}

public sealed class SoftTalkImeSyncProbe
{
    public SoftTalkImeSyncProbe(
        bool ready,
        string source,
        string baseUrl,
        string clientDatabasePath,
        bool clientDatabaseExists,
        bool tokenPresent,
        string reason)
    {
        Ready = ready;
        Source = source;
        BaseUrl = baseUrl;
        ClientDatabasePath = clientDatabasePath;
        ClientDatabaseExists = clientDatabaseExists;
        TokenPresent = tokenPresent;
        Reason = reason;
    }

    public bool Ready { get; }

    public string Source { get; }

    public string BaseUrl { get; }

    public string ClientDatabasePath { get; }

    public bool ClientDatabaseExists { get; }

    public bool TokenPresent { get; }

    public string Reason { get; }
}

public static class SoftTalkImeSyncConnectionResolver
{
    public const string ClientPublicDatabaseEnvironmentVariable = "SOFTTALK_IME_CLIENT_PUBLIC_DB";
    public const string DefaultOnlineBaseUrl = "https://v3.api.kefuhaohaoshuohua.cn";
    public const string LocalDevelopmentBaseUrl = "http://127.0.0.1:8200";

    public static bool TryResolve(
        out SoftTalkImeSyncConnection? connection,
        out SoftTalkImeSyncProbe probe)
    {
        var baseUrl = ResolveBaseUrl();
        var explicitToken = ReadEnvironment("SOFTTALK_IME_SYNC_TOKEN");
        if (!string.IsNullOrWhiteSpace(explicitToken))
        {
            var validBaseUrl = TryNormalizeBaseUrl(baseUrl, out var normalizedBaseUrl);
            probe = new SoftTalkImeSyncProbe(
                ready: validBaseUrl,
                source: "environment",
                baseUrl: validBaseUrl ? normalizedBaseUrl : baseUrl,
                clientDatabasePath: string.Empty,
                clientDatabaseExists: false,
                tokenPresent: true,
                reason: validBaseUrl ? "ready" : "invalid_base_url");
            connection = validBaseUrl
                ? new SoftTalkImeSyncConnection(normalizedBaseUrl, explicitToken, "environment")
                : null;
            return validBaseUrl;
        }

        var clientAuth = ReadClientAuth();
        var validClientBaseUrl = TryNormalizeBaseUrl(baseUrl, out var normalizedClientBaseUrl);
        var ready = clientAuth.Token is not null && validClientBaseUrl;
        probe = new SoftTalkImeSyncProbe(
            ready: ready,
            source: clientAuth.Token is not null ? "client-public-db" : "none",
            baseUrl: validClientBaseUrl ? normalizedClientBaseUrl : baseUrl,
            clientDatabasePath: clientAuth.DatabasePath,
            clientDatabaseExists: clientAuth.DatabaseExists,
            tokenPresent: clientAuth.Token is not null,
            reason: ready
                ? "ready"
                : clientAuth.Reason);
        connection = ready
            ? new SoftTalkImeSyncConnection(normalizedClientBaseUrl, clientAuth.Token!, "client-public-db")
            : null;
        return ready;
    }

    public static SoftTalkImeSyncProbe ProbeClientAuth(string? databasePath = null)
    {
        var auth = ReadClientAuth(databasePath);
        return new SoftTalkImeSyncProbe(
            ready: auth.Token is not null,
            source: auth.Token is not null ? "client-public-db" : "none",
            baseUrl: string.Empty,
            clientDatabasePath: auth.DatabasePath,
            clientDatabaseExists: auth.DatabaseExists,
            tokenPresent: auth.Token is not null,
            reason: auth.Reason);
    }

    private static ClientAuthResult ReadClientAuth(string? databasePath = null)
    {
        var path = ResolveClientDatabasePath(databasePath);
        if (!File.Exists(path))
        {
            return new ClientAuthResult(null, path, false, "client_public_db_missing");
        }

        try
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Shared,
            };
            using var connection = new SqliteConnection(builder.ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT account_name, employee_no, access_token_cipher, login_mode
                FROM st_public_auth_state
                WHERE id = 1
                LIMIT 1
                """;
            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return new ClientAuthResult(null, path, true, "client_login_state_missing");
            }

            var accountName = ReadColumn(reader, 0);
            var employeeNo = ReadColumn(reader, 1);
            var tokenCipher = ReadColumn(reader, 2);
            var loginMode = ReadColumn(reader, 3);
            if (string.Equals(loginMode, "offline", StringComparison.OrdinalIgnoreCase))
            {
                return new ClientAuthResult(null, path, true, "client_offline_mode");
            }

            if (string.IsNullOrWhiteSpace(accountName) || string.IsNullOrWhiteSpace(employeeNo))
            {
                return new ClientAuthResult(null, path, true, "client_identity_missing");
            }

            var token = DecryptToken(tokenCipher);
            return string.IsNullOrWhiteSpace(token)
                ? new ClientAuthResult(null, path, true, "client_token_missing")
                : new ClientAuthResult(token, path, true, "ready");
        }
        catch (SqliteException)
        {
            return new ClientAuthResult(null, path, true, "client_public_db_unreadable");
        }
        catch (CryptographicException)
        {
            return new ClientAuthResult(null, path, true, "client_token_decryption_failed");
        }
        catch (FormatException)
        {
            return new ClientAuthResult(null, path, true, "client_token_format_invalid");
        }
        catch (InvalidDataException)
        {
            return new ClientAuthResult(null, path, true, "client_token_format_invalid");
        }
        catch (UnauthorizedAccessException)
        {
            return new ClientAuthResult(null, path, true, "client_public_db_access_denied");
        }
    }

    private static string ResolveClientDatabasePath(string? databasePath)
    {
        if (!string.IsNullOrWhiteSpace(databasePath))
        {
            return Path.GetFullPath(databasePath.Trim());
        }

        var configuredPath = ReadEnvironment(ClientPublicDatabaseEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        var root = ReadEnvironment("SOFTTALK_ROOT_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "SoftTalk");
        }

        return Path.Combine(root, "public.db");
    }

    private static string ResolveBaseUrl()
    {
        var explicitImeBaseUrl = ReadEnvironment("SOFTTALK_IME_SYNC_BASE_URL");
        if (!string.IsNullOrWhiteSpace(explicitImeBaseUrl))
        {
            return explicitImeBaseUrl;
        }

        var clientBaseUrl = ReadEnvironment("SOFTTALK_SYNC_SERVER_BASE_URL");
        if (!string.IsNullOrWhiteSpace(clientBaseUrl))
        {
            return clientBaseUrl;
        }

        return IsLocalSyncServerListening()
            ? LocalDevelopmentBaseUrl
            : DefaultOnlineBaseUrl;
    }

    private static bool IsLocalSyncServerListening()
    {
        try
        {
            return IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpListeners()
                .Any(endpoint => endpoint.Port == 8200
                    && (IPAddress.IsLoopback(endpoint.Address) || endpoint.Address.Equals(IPAddress.Any)));
        }
        catch (NetworkInformationException)
        {
            return false;
        }
    }

    private static bool TryNormalizeBaseUrl(string value, out string normalized)
    {
        normalized = string.Empty;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        normalized = uri.ToString().TrimEnd('/');
        return true;
    }

    private static string ReadColumn(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal).Trim();
    }

    private static string DecryptToken(string ciphertext)
    {
        const string prefix = "dpapi:";
        if (!ciphertext.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new InvalidDataException("客户端登录令牌不是 DPAPI 密文。");
        }

        var encrypted = Convert.FromBase64String(ciphertext[prefix.Length..]);
        try
        {
#pragma warning disable CA1416
            var plain = ProtectedData.Unprotect(encrypted, optionalEntropy: null, DataProtectionScope.CurrentUser);
#pragma warning restore CA1416
            try
            {
                return Encoding.UTF8.GetString(plain).Trim();
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plain);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encrypted);
        }
    }

    private static string ReadEnvironment(string name)
    {
        return Environment.GetEnvironmentVariable(name)?.Trim() ?? string.Empty;
    }

    private sealed record ClientAuthResult(
        string? Token,
        string DatabasePath,
        bool DatabaseExists,
        string Reason);
}
