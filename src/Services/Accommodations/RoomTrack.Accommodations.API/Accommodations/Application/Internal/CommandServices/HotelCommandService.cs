using BackendAwRoomTrack.API.Accommodations.Domain.Model.Aggregates;
using BackendAwRoomTrack.API.Accommodations.Domain.Model.Exceptions;
using BackendAwRoomTrack.API.Accommodations.Domain.Model.ValueObjects;
using BackendAwRoomTrack.API.Bookings.Interfaces.ACL;
using BackendAwRoomTrack.API.Accommodations.Domain.Model.Commands;
using BackendAwRoomTrack.API.Accommodations.Domain.Repositories;
using BackendAwRoomTrack.API.Accommodations.Domain.Services;
using BackendAwRoomTrack.API.IAM.Interfaces.ACL;
using BackendAwRoomTrack.API.Media.Interfaces.ACL;
using BackendAwRoomTrack.Domain.Shared.Domain.Model.Exceptions;
using BackendAwRoomTrack.API.Shared.Domain.Repositories;
using MassTransit;
using RoomTrack.Contracts.Messaging;

namespace BackendAwRoomTrack.API.Accommodations.Application.Internal.CommandServices;

/// <summary>
/// Service implementation for handling hotel commands.
/// Orchestrates the flow between the repository and the domain logic.
/// </summary>
public class HotelCommandService(
    IHotelRepository hotelRepository,
    IIamContextFacade iamContextFacade,
    IRoomRepository roomRepository,
    IRoomReservationsFacade roomReservationsFacade,
    IMediaContextFacade mediaContextFacade,
    IUnitOfWork unitOfWork,
    IPublishEndpoint publishEndpoint,
    TimeProvider timeProvider)
    : IHotelCommandService
{
    /// <summary>
    /// Handles the creation of a new hotel.
    /// </summary>
    /// <param name="command">The command containing the hotel creation data.</param>
    /// <returns>The created hotel and the new session of a hotel administrator who registered their own hotel.</returns>
    public async Task<HotelRegistration> Handle(CreateHotelCommand command)
    {
        var registrant = command.Registrant;
        var alreadyHostsAHotel = !registrant.ManagesChain
                                 && await hotelRepository.ExistsByHostIdAsync(registrant.UserId);
        var hostId = HotelRegistrationPolicy.ResolveHost(registrant, command.RequestedHostId, alreadyHostsAHotel);
        EnsureImageIsFromTheMediaLibrary(command.ImageUrl);

        var hotel = new Hotel(hostId, command);
        ReissuedSession? registrantSession = null;
        await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await hotelRepository.AddAsync(hotel);
            await unitOfWork.CompleteAsync();

            // D2: the hotel a hotel administrator registers is the one they administer (Identity owns that scope).
            // Their tokens without the hotel are revoked and Identity issues new ones with it. That call commits in
            // the Identity service: it is made last, so a failure there rolls the new hotel back here.
            if (HotelRegistrationPolicy.AssignsHotelToRegistrant(registrant))
                registrantSession = await iamContextFacade.AssignHotelToAdministratorAsync(
                    registrant.UserId, hotel.Id, command.RegistrantSession);
        });
        return new HotelRegistration(hotel, registrantSession);
    }

    /// <summary>
    /// Handles the update of an existing hotel.
    /// </summary>
    /// <param name="command">The command containing the hotel update data.</param>
    /// <returns>The updated hotel or null if the hotel was not found.</returns>
    public async Task<Hotel?> Handle(UpdateHotelCommand command)
    {
        var hotel = await hotelRepository.FindByIdAsync(command.Id);
        if (hotel is null) return null;

        // Images registered before signed uploads existed (e.g. the seed placeholders) stay valid while unchanged.
        if (!string.Equals(hotel.ImageUrl, command.ImageUrl, StringComparison.Ordinal))
            EnsureImageIsFromTheMediaLibrary(command.ImageUrl);

        // Apply domain logic update
        hotel.UpdateInformation(
            command.Name, 
            command.Address, 
            command.City, 
            command.Country, 
            command.ImageUrl, 
            command.Description, 
            command.Type, 
            command.Amenities);

        hotelRepository.Update(hotel);
        await unitOfWork.CompleteAsync();
        return hotel;
    }

    public async Task<Hotel?> Handle(ConfigureHotelPaymentSettingsCommand command)
    {
        var hotel = await hotelRepository.FindByIdAsync(command.HotelId);
        if (hotel is null) return null;

        hotel.ConfigurePaymentSettings(HotelPaymentSettings.Create(command.AccountHolder, command.YapeNumber,
            command.PlinNumber, command.BankName, command.BankAccountNumber, command.BankAccountCci));

        // The hotel is tracked: the change tracker replaces the owned settings (no Update() of the whole graph).
        await unitOfWork.CompleteAsync();
        return hotel;
    }

    /// <summary>
    ///     A hotel image must be one uploaded to the project's media library with a signature of this API (not any
    ///     URL of the internet): the Media context decides which URLs qualify.
    /// </summary>
    private void EnsureImageIsFromTheMediaLibrary(string imageUrl)
    {
        if (mediaContextFacade.IsAcceptedHotelImageUrl(imageUrl)) return;
        throw new InvalidFieldException("imageUrl", AccommodationErrorCodes.HotelImageUrlNotAllowed,
            $"Upload the image with the application: the hotel image must be an image of RoomTrack's media library ({mediaContextFacade.AcceptedHotelImageUrlPrefix}...).");
    }

    /// <summary>
    /// Handles the deletion of a hotel.
    /// </summary>
    /// <param name="command">The command containing the hotel deletion data.</param>
    /// <returns>The deleted hotel or null if the hotel was not found.</returns>
    public async Task<Hotel?> Handle(DeleteHotelCommand command)
    {
        var hotel = await hotelRepository.FindByIdAsync(command.Id);
        if (hotel is null) return null;

        // Deleting a hotel deletes its rooms: none of them may still hold bookings.
        var rooms = await roomRepository.ListByHotelAsync(hotel.Id);
        var active = await roomReservationsFacade.CountActiveBookingsAsync(rooms.Select(room => room.Id).ToList());
        if (active.Count > 0)
            throw new RoomHasActiveBookingsException(AccommodationErrorCodes.HotelHasActiveBookings,
                $"Hotel {hotel.Name} has {active.Values.Sum()} active booking(s). Cancel or complete them before deleting the hotel.",
                active.Values.Sum());

        hotelRepository.Remove(hotel);
        var now = timeProvider.GetUtcNow();
        foreach (var room in rooms)
            await publishEndpoint.Publish(new RoomRemovedIntegrationEvent(room.Id, room.HotelId, now));
        await unitOfWork.CompleteAsync();
        return hotel;
    }
}