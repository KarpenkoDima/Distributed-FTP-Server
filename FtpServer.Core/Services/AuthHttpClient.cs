using System.Net.Http.Json;
using System.Text.Json;

namespace FtpServer.Core.Services;

public class AuthHttpClient
{
    private readonly HttpClient _httpClient;
    

    public AuthHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient; 
        
    }
    public async Task<AuthRequest> GetUserByUsernameAsync(string username)
    {
        var request = new { Username = username};
        Console.WriteLine($"GetUsername: {username}");
        var response = await _httpClient.PostAsJsonAsync("/api/Auth/user/", request);
        Console.WriteLine($"Response status code from AuthService: {response.StatusCode}");
        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Parsin: {response}");
            var jsonContent = await response.Content.ReadAsStringAsync();
            var user = JsonSerializer.Deserialize<User>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
            
            return new AuthRequest
            {
                IsSuccess = user != null,
                Message = user != null ? user.ToString() :  "User not found",
                Username = user?.Username
            };
        }
        return new AuthRequest
        {
            IsSuccess = false,
            Message = "service unavaliable"
        };
    }

    public async Task<AuthRequest> AuthenticateAsync(string username, string password)
    {
        try
        {
            var request = new {Username = username, Password = password};
            
            var response = await _httpClient.PostAsJsonAsync("api/auth/authenticate", request);

            if (response.IsSuccessStatusCode)
            {
                var jsonContent = await response.Content.ReadAsStringAsync();
                var authResponse = JsonSerializer.Deserialize<AuthResponse>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });

                return new AuthRequest
                {
                    IsSuccess = authResponse?.IsSuccess ?? false,
                    Message = authResponse?.Message ?? "Unknown error",
                    Username = authResponse?.User?.Username ?? ""
                };                 
            }
            return new AuthRequest
            {
                IsSuccess = false,
                Message = "service unavaliable"                
            };
        }
        catch (Exception ex )
        {
            return new AuthRequest
            {
                IsSuccess = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }
}

 // Модели для HTTP клиента
public class AuthRequest
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = "";
    public string Username { get; set; } = "";
}

public class AuthResponse
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = "";
    public User? User { get; set; }
}

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Email { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
