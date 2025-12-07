namespace JobPosts.DTOs;

public class LoginResponse
{
    public string AccessToken { get; set; } = "";
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
}