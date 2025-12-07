namespace JobPosts.DTOs;

public class UserDto
{
    public string ID { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Surname { get; set; } = string.Empty;
}