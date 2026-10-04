using System.Collections.Concurrent;
using BackendAwRoomTrack.API.Models.IoT;

namespace BackendAwRoomTrack.API.Infrastructure.Telemetry;

public static class IoTEmulatorStore
{
    // Almacena el estado por cada número de habitación (RoomId)

    private static readonly ConcurrentDictionary<int, EmulatedRoomState> _roomStates = new();

    public static EmulatedRoomState GetOrAdd(int roomId)
    {
        return _roomStates.GetOrAdd(roomId, id => new EmulatedRoomState
        {
            RoomId = id
        });
    }

    public static void Update(int roomId, Action<EmulatedRoomState> updateAction)
    {
        var state = GetOrAdd(roomId);
        updateAction(state);
        state.Timestamp = DateTime.UtcNow;
    }
}