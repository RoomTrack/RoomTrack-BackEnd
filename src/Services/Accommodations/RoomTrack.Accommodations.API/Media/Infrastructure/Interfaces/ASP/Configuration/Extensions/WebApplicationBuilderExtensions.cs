using BackendAwRoomTrack.API.Media.Application.ACL;
using BackendAwRoomTrack.API.Media.Application.Internal.CommandServices;
using BackendAwRoomTrack.API.Media.Application.OutboundServices;
using BackendAwRoomTrack.API.Media.Infrastructure.Cloudinary;
using BackendAwRoomTrack.API.Media.Interfaces.ACL;
using Microsoft.Extensions.Options;

namespace BackendAwRoomTrack.API.Media.Infrastructure.Interfaces.ASP.Configuration.Extensions;

public static class WebApplicationBuilderExtensions
{
    /// <summary>
    ///     Media context: signed uploads of hotel images to Cloudinary. The account (<c>Cloudinary__*</c>) is
    ///     validated at startup: required in Production; in Development, without it the uploads answer 503.
    /// </summary>
    public static void AddMediaContextServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<CloudinarySettings>()
            .Bind(builder.Configuration.GetSection(CloudinarySettings.SectionName))
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<CloudinarySettings>, CloudinarySettingsValidator>();

        builder.Services.AddSingleton<IUploadSignatureService, CloudinaryUploadSignatureService>();
        builder.Services.AddScoped<HotelImageUploadCommandService>();
        builder.Services.AddScoped<IMediaContextFacade, MediaContextFacade>();
    }
}
