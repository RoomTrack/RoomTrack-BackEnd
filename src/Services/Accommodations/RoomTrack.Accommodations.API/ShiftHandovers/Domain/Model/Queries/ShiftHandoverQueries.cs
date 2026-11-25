using RoomTrack.ShiftHandovers.Domain.Model.ValueObjects;

namespace RoomTrack.ShiftHandovers.Domain.Model.Queries;

/// <summary>A handover with its items and confirmations.</summary>
public record GetShiftHandoverByIdQuery(int ShiftHandoverId);

/// <summary>The handovers of a hotel, newest first, optionally of one area.</summary>
public record GetShiftHandoversQuery(int HotelId, ShiftArea? Area = null, int Limit = 20);

/// <summary>The last handover of the area: what the shift starting now reads (US-32).</summary>
public record GetLatestShiftHandoverQuery(int HotelId, ShiftArea Area);
