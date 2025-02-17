using AutoMapper;
using Common.DTO;
using Common.Entities;
using Common.Enums;
using Common.Helpers;
using Common.Models;


namespace Common.Mappers
{
	public class MappingProfile : Profile
	{
		public MappingProfile()
		{
			CreateMap<UserEntity, User>()
				.ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.RowKey))
				.ForMember(dest => dest.Role, opt => opt.MapFrom(src => Enum.Parse<UserRole>(src.PartitionKey)))
				.ForMember(dest => dest.Password, opt => opt.MapFrom(src => src.Password))
				.ReverseMap()
				.ForMember(dest => dest.Id, opt => opt.MapFrom(_ => Guid.NewGuid().ToString()))
				.ForMember(dest => dest.RowKey, opt => opt.MapFrom(src => src.Email))
				.ForMember(dest => dest.PartitionKey, opt => opt.MapFrom(src => src.Role.ToString()))
				.ForMember(dest => dest.Password, opt => opt.MapFrom(src => PasswordHasher.HashPassword(src.Password, 12)));

			CreateMap<UserEntity, UserReliableEntity>()
				.ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.RowKey))
				.ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.PartitionKey));

			CreateMap<UserReliableEntity, User>().ReverseMap();

			CreateMap<AnalysisEntity, Analysis>().ReverseMap();

			CreateMap<CourseEntity, Course>()
				.ForMember(dest => dest.CourseId, opt => opt.MapFrom(src => src.RowKey))
				.ForMember(dest => dest.AuthorId, opt => opt.MapFrom(src => src.PartitionKey))
				.ReverseMap()
				.ForMember(dest => dest.RowKey, opt => opt.MapFrom(src => src.CourseId))
				.ForMember(dest => dest.PartitionKey, opt => opt.MapFrom(src => src.AuthorId));

			CreateMap<DocumentDTO, Document>()
				.ForMember(dest => dest.Extension, opt => opt.MapFrom(src => src.Extension.ToString().ToLower()))
				.ReverseMap()
				.ForMember(dest => dest.Extension, opt => opt.MapFrom(src => Enum.Parse<DocumentExtension>(src.Extension)));
		}

	}
}
