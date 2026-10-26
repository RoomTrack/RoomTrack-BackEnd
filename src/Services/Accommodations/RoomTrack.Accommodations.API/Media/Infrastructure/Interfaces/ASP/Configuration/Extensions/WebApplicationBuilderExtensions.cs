using RoomTrack.Media.Application.ACL;
using RoomTrack.Media.Application.Internal.CommandServices;
using RoomTrack.Media.Application.OutboundServices;
using RoomTrack.Media.Infrastructure.Cloudinary;
using RoomTrack.Media.Interfaces.ACL;
using Microsoft.Extensions.Options;

namespace RoomTrack.Media.Infrastructure.Interfaces.ASP.Configuration.Extensions;

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
