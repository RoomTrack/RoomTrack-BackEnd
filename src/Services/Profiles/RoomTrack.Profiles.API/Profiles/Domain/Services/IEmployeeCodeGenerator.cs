using RoomTrack.Profiles.Domain.Model.ValueObjects;

namespace RoomTrack.Profiles.Domain.Services;

public interface IEmployeeCodeGenerator
{
    Task<EmployeeCode> GenerateNextCodeAsync();
}
