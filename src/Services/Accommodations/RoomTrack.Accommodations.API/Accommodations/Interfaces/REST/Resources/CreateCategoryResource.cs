using System.ComponentModel.DataAnnotations;

namespace BackendAwRoomTrack.API.Accommodations.Interfaces.REST.Resources;

public record CreateCategoryResource([Required] string Name);