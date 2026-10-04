using BackendAwRoomTrack.API.Media.Application.OutboundServices;
using BackendAwRoomTrack.API.Media.Interfaces.ACL;

namespace BackendAwRoomTrack.API.Media.Application.ACL;

public class MediaContextFacade(IUploadSignatureService uploadSignatureService) : IMediaContextFacade
{
    public bool IsAcceptedHotelImageUrl(string imageUrl) =>
        AcceptedHotelImageUrlPrefix is not { } prefix
        || (imageUrl.StartsWith(prefix, StringComparison.Ordinal) && imageUrl.Length > prefix.Length);

    public string? AcceptedHotelImageUrlPrefix =>
        uploadSignatureService.IsConfigured ? uploadSignatureService.ImageDeliveryUrlPrefix : null;
}
