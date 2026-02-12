using ApiWebTrackerGanado.Dtos;
using ApiWebTrackerGanado.Helpers;
using ApiWebTrackerGanado.Models;
using AutoMapper;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ApiWebTrackerGanado.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Animal mappings
            CreateMap<Animal, AnimalDto>()
                .ForMember(dest => dest.CurrentLocation, opt => opt.Ignore());

            CreateMap<CreateAnimalDto, Animal>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow));

            CreateMap<UpdateAnimalDto, Animal>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.FarmId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow));

            // Health records mappings
            CreateMap<HealthRecord, HealthRecordDto>()
                .ForMember(dest => dest.AnimalName, opt => opt.MapFrom(src => src.Animal.Name))
                .ForMember(dest => dest.AnimalTag, opt => opt.MapFrom(src => src.Animal.Tag))
                .ForMember(dest => dest.FarmName, opt => opt.MapFrom(src => src.Animal.Farm.Name));
            CreateMap<CreateHealthRecordDto, HealthRecord>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.Animal, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow));

            // Alert mappings
            CreateMap<Alert, AlertDto>()
                .ForMember(dest => dest.AnimalName, opt => opt.MapFrom(src => src.Animal.Name));

            // Farm mappings with boundary coordinates
            CreateMap<Farm, FarmDto>()
                .ForMember(dest => dest.BoundaryCoordinates, opt => opt.MapFrom(src =>
                    src.BoundaryCoordinates != null ?
                    src.BoundaryCoordinates.OrderBy(b => b.SequenceOrder)
                        .Select(b => new LatLngDto { Lat = b.Latitude, Lng = b.Longitude })
                        .ToList() : new List<LatLngDto>()));

            CreateMap<CreateFarmDto, Farm>()
                .ForMember(dest => dest.BoundaryCoordinates, opt => opt.Ignore()) // Handle separately in controller
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.UserId, opt => opt.Ignore());
        }
    }
}