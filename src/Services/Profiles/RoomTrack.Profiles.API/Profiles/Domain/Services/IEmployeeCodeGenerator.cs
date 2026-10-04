using BackendAwRoomTrack.Domain.Profiles.Domain.Model.ValueObjects;

namespace BackendAwRoomTrack.Domain.Profiles.Domain.Services;

public interface IEmployeeCodeGenerator
{
    Task<EmployeeCode> GenerateNextCodeAsync();
}
