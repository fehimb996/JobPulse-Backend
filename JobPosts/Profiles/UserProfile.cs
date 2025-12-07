using AutoMapper;
using JobPosts.Commands;
using JobPosts.DTOs;
using JobPosts.Models;

namespace JobPosts.Profiles;

public class UserProfile : Profile
{
    public UserProfile()
    {
        CreateMap<RegisterCommand, ApplicationUser>()
           .ForMember(dest => dest.EmailConfirmed, opt => opt.MapFrom(src => false)).ReverseMap();
        CreateMap<ApplicationUser, UserDto>().ReverseMap();
    }
}

