using AutoMapper;
using IGB.Application.DTOs;
using IGB.Domain.Entities;

namespace IGB.Application.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<User, UserDto>()
            .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.LocalNumber ?? src.WhatsappNumber));
        CreateMap<CreateUserDto, User>()
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.PasswordHash, opt => opt.Ignore())
            .ForMember(dest => dest.LocalNumber, opt => opt.MapFrom(src => src.PhoneNumber))
            .ForMember(dest => dest.WhatsappNumber, opt => opt.Ignore())
            .ForMember(dest => dest.TimeZoneId, opt => opt.Ignore())
            .ForMember(dest => dest.CountryCode, opt => opt.Ignore())
            .ForMember(dest => dest.ProfileImagePath, opt => opt.Ignore())
            .ForMember(dest => dest.EmailConfirmed, opt => opt.Ignore())
            .ForMember(dest => dest.EmailConfirmationTokenHash, opt => opt.Ignore())
            .ForMember(dest => dest.EmailConfirmationSentAt, opt => opt.Ignore())
            .ForMember(dest => dest.ApprovalStatus, opt => opt.Ignore())
            .ForMember(dest => dest.ApprovedAt, opt => opt.Ignore())
            .ForMember(dest => dest.ApprovedByUserId, opt => opt.Ignore())
            .ForMember(dest => dest.ApprovalNote, opt => opt.Ignore())
            .ForMember(dest => dest.Guardians, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore());
        CreateMap<UpdateUserDto, User>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.Email, opt => opt.Ignore())
            .ForMember(dest => dest.Role, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.PasswordHash, opt => opt.Ignore())
            .ForMember(dest => dest.LocalNumber, opt => opt.MapFrom(src => src.PhoneNumber))
            .ForMember(dest => dest.WhatsappNumber, opt => opt.Ignore())
            .ForMember(dest => dest.TimeZoneId, opt => opt.Ignore())
            .ForMember(dest => dest.CountryCode, opt => opt.Ignore())
            .ForMember(dest => dest.ProfileImagePath, opt => opt.Ignore())
            .ForMember(dest => dest.EmailConfirmed, opt => opt.Ignore())
            .ForMember(dest => dest.EmailConfirmationTokenHash, opt => opt.Ignore())
            .ForMember(dest => dest.EmailConfirmationSentAt, opt => opt.Ignore())
            .ForMember(dest => dest.ApprovalStatus, opt => opt.Ignore())
            .ForMember(dest => dest.ApprovedAt, opt => opt.Ignore())
            .ForMember(dest => dest.ApprovedByUserId, opt => opt.Ignore())
            .ForMember(dest => dest.ApprovalNote, opt => opt.Ignore())
            .ForMember(dest => dest.Guardians, opt => opt.Ignore())
            .ForMember(dest => dest.IsDeleted, opt => opt.Ignore());
    }
}

