using RoomTrack.ShiftHandovers.Domain.Model.Aggregates;
using RoomTrack.ShiftHandovers.Domain.Model.Commands;
using RoomTrack.ShiftHandovers.Domain.Model.Queries;

namespace RoomTrack.ShiftHandovers.Domain.Services;

public interface IShiftHandoverCommandService
{
    Task<ShiftHandover> Handle(CreateShiftHandoverCommand command);

    /// <returns>The handover, or null when it does not exist.</returns>
    Task<ShiftHandover?> Handle(AcknowledgeShiftHandoverCommand command);
}

public interface IShiftHandoverQueryService
{
    Task<ShiftHandover?> Handle(GetShiftHandoverByIdQuery query);

    Task<IReadOnlyList<ShiftHandover>> Handle(GetShiftHandoversQuery query);

    /// <returns>The last handover of the area, or null when it never had one.</returns>
    Task<ShiftHandover?> Handle(GetLatestShiftHandoverQuery query);
}
