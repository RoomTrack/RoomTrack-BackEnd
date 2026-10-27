using System.ComponentModel.DataAnnotations;

namespace RoomTrack.Accommodations.Interfaces.REST.Resources;

public record CreateCategoryResource([Required] string Name);