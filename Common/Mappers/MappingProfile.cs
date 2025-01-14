using AutoMapper;
using Common.Entities;
using Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Mappers
{
	public class MappingProfile : Profile
	{
		public MappingProfile()
		{
			CreateMap<UserEntity, User>().ReverseMap();
			CreateMap<UserReliableEntity, User>().ReverseMap();
			CreateMap<UserEntity, UserReliableEntity>().ReverseMap();
		}
	}
}
