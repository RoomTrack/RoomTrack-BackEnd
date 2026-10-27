using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RoomTrack.Accommodations.Interfaces.ACL;
using RoomTrack.Profiles.Application.Internal.Commands;
using RoomTrack.Profiles.Application.Internal.CommandServices;
using RoomTrack.Profiles.Application.Internal.OutboundServices;
using RoomTrack.Shared.Domain.Repositories;
using RoomTrack.Profiles.Domain.Model.Aggregates;
using RoomTrack.Profiles.Domain.Model.Enums;
using RoomTrack.Profiles.Domain.Model.Exceptions;
using RoomTrack.Profiles.Domain.Model.ValueObjects;
using RoomTrack.Profiles.Domain.Repositories;
using RoomTrack.Profiles.Domain.Services;
using RoomTrack.Shared.Domain.Model.Events;
using RoomTrack.Shared.Domain.Model.Exceptions;
using FluentAssertions;
using Xunit;

namespace RoomTrack.Tests.Profiles.Application;

public class StaffProfileCommandServiceAclIntegrationTests
{
    private class FakeStaffRepository : IStaffProfileRepository
    {
        public StaffProfile? StaffToReturn { get; set; }
        public bool UpdateCalled { get; private set; }

        public Task AddAsync(StaffProfile staffProfile) => Task.CompletedTask;
        public Task<StaffProfile?> FindByIdAsync(StaffProfileId profileId) => Task.FromResult(StaffToReturn);
        public Task<StaffProfile?> FindByUserIdAsync(UserId userId) => Task.FromResult(StaffToReturn);
        public Task<StaffProfile?> FindByEmployeeCodeAsync(EmployeeCode code) => Task.FromResult(StaffToReturn);
        public Task<IEnumerable<StaffProfile>> FindByTargetIdAsync(TargetId targetId) => Task.FromResult<IEnumerable<StaffProfile>>(new List<StaffProfile>());
        public Task<IEnumerable<StaffProfile>> ListAsync() => Task.FromResult<IEnumerable<StaffProfile>>(new List<StaffProfile>());
        public Task<bool> ExistsByEmployeeCodeAsync(EmployeeCode code) => Task.FromResult(false);
        public Task<bool> ExistsByUserIdAsync(UserId userId) => Task.FromResult(false);
        public void Update(StaffProfile staffProfile) { UpdateCalled = true; }
        public void Remove(StaffProfile staffProfile) { }
    }

    private class FakeUnitOfWork : IUnitOfWork
    {
        public bool CompleteCalled { get; private set; }
        public Task CompleteAsync() { CompleteCalled = true; return Task.CompletedTask; }
        public Task ExecuteInTransactionAsync(Func<Task> work) => work();
    }

    private class FakeEmployeeCodeGenerator : IEmployeeCodeGenerator
    {
        public Task<EmployeeCode> GenerateNextCodeAsync() => Task.FromResult(new EmployeeCode("EMP-00001"));
    }

    private class FakeDomainEventPublisher : IDomainEventPublisher
    {
        public Task PublishAsync(IReadOnlyCollection<IEvent> domainEvents, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private class FakeAccommodationsContextFacade : IAccommodationsContextFacade
    {
        public Func<int, Task<bool>>? HotelExistsHandler { get; set; }
        public int LastCheckedHotelId { get; private set; }
        public bool WasCalled { get; private set; }

        public Task<bool> HotelExistsAsync(int hotelId)
        {
            WasCalled = true;
            LastCheckedHotelId = hotelId;
            return HotelExistsHandler != null ? HotelExistsHandler(hotelId) : Task.FromResult(false);
        }

        // Not used by the staff assignments.
        public Task<IReadOnlyDictionary<int, string>> FetchRoomNumbersAsync(IReadOnlyCollection<int> roomIds) => throw new NotSupportedException();
        public Task<RoomOffer?> FetchRoomAsync(int roomId) => throw new NotSupportedException();
        public Task<HotelSummary?> FetchHotelAsync(int hotelId) => throw new NotSupportedException();
        public Task<HotelPaymentInstructions?> FetchPaymentInstructionsAsync(int hotelId) => throw new NotSupportedException();
        public Task<bool> RoomExistsAsync(int roomId) => throw new NotSupportedException();
        public Task<decimal?> FetchRoomPricePerNightAsync(int roomId) => throw new NotSupportedException();
        public Task<int?> FetchHotelIdOfRoomAsync(int roomId) => throw new NotSupportedException();
        public Task<RoomOffer?> LockRoomForBookingAsync(int roomId) => throw new NotSupportedException();
        public Task OccupyRoomForCheckInAsync(int roomId, int? guestUserId, string? guestEmail) => throw new NotSupportedException();
        public Task VacateRoomAfterCheckOutAsync(int roomId, int? checkedOutByUserId, string? checkedOutByEmail) => throw new NotSupportedException();
        public Task<IReadOnlyList<RoomOffer>> FetchRoomsOfferedForBookingAsync(int? hotelId) => throw new NotSupportedException();
    }

    private static StaffProfile CreateSampleStaff()
    {
        return new StaffProfile(
            StaffProfileId.New(),
            new UserId(1),
            new EmployeeCode("EMP-00001"),
            new PersonName("Jane", "Doe"),
            new EmailAddress("jane.doe@roomtrack.com"),
            new JobPosition("Manager"),
            HabitualShift.Morning);
    }

    [Fact]
    public async Task AddAssignment_ScopeLevelHotel_ValidHotel_ShouldCreateAssignment()
    {
        // Arrange
        const int validHotelId = 101;
        var staff = CreateSampleStaff();
        var repo = new FakeStaffRepository { StaffToReturn = staff };
        var unitOfWork = new FakeUnitOfWork();
        var generator = new FakeEmployeeCodeGenerator();
        var publisher = new FakeDomainEventPublisher();
        var facade = new FakeAccommodationsContextFacade
        {
            HotelExistsHandler = id => Task.FromResult(id == validHotelId)
        };

        var service = new StaffProfileCommandService(repo, generator, unitOfWork, publisher, facade);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var command = new AddStaffAssignmentCommand(
            staff.Id,
            ScopeLevel.Hotel,
            new TargetId(validHotelId),
            StaffRole.Admin,
            new DateRange(today, today.AddMonths(3)),
            today);

        // Act
        var result = await service.Handle(command);

        // Assert
        result.Should().NotBeNull();
        facade.WasCalled.Should().BeTrue();
        facade.LastCheckedHotelId.Should().Be(validHotelId);
        unitOfWork.CompleteCalled.Should().BeTrue();
        repo.UpdateCalled.Should().BeTrue();
        staff.Assignments.Should().ContainSingle(a => a.TargetId == new TargetId(validHotelId));
    }

    [Fact]
    public async Task AddAssignment_ScopeLevelHotel_NonExistentHotel_ShouldFailValidation_AndNotPersist()
    {
        // Arrange
        const int nonExistentHotelId = 999;
        var staff = CreateSampleStaff();
        var repo = new FakeStaffRepository { StaffToReturn = staff };
        var unitOfWork = new FakeUnitOfWork();
        var generator = new FakeEmployeeCodeGenerator();
        var publisher = new FakeDomainEventPublisher();
        var facade = new FakeAccommodationsContextFacade
        {
            HotelExistsHandler = _ => Task.FromResult(false)
        };

        var service = new StaffProfileCommandService(repo, generator, unitOfWork, publisher, facade);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var command = new AddStaffAssignmentCommand(
            staff.Id,
            ScopeLevel.Hotel,
            new TargetId(nonExistentHotelId),
            StaffRole.Admin,
            new DateRange(today, today.AddMonths(3)),
            today);

        // Act
        Func<Task> action = async () => await service.Handle(command);

        // Assert
        (await action.Should().ThrowAsync<DomainValidationException>()
            .WithMessage($"*Hotel with ID {nonExistentHotelId} does not exist in Accommodations*"))
            .Which.Code.Should().Be(ProfileErrorCodes.HotelNotFound);

        facade.WasCalled.Should().BeTrue();
        facade.LastCheckedHotelId.Should().Be(nonExistentHotelId);
        unitOfWork.CompleteCalled.Should().BeFalse();
        repo.UpdateCalled.Should().BeFalse();
        staff.Assignments.Should().BeEmpty();
    }

    [Fact]
    public async Task AddAssignment_ScopeLevelChain_ShouldNotInvokeHotelExistsAsync()
    {
        // Arrange
        const int chainId = 10;
        var staff = CreateSampleStaff();
        var repo = new FakeStaffRepository { StaffToReturn = staff };
        var unitOfWork = new FakeUnitOfWork();
        var generator = new FakeEmployeeCodeGenerator();
        var publisher = new FakeDomainEventPublisher();
        var facade = new FakeAccommodationsContextFacade();

        var service = new StaffProfileCommandService(repo, generator, unitOfWork, publisher, facade);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var command = new AddStaffAssignmentCommand(
            staff.Id,
            ScopeLevel.Chain,
            new TargetId(chainId),
            StaffRole.ChainAdmin,
            new DateRange(today, today.AddMonths(12)),
            today);

        // Act
        var result = await service.Handle(command);

        // Assert
        result.Should().NotBeNull();
        facade.WasCalled.Should().BeFalse();
        unitOfWork.CompleteCalled.Should().BeTrue();
        staff.Assignments.Should().ContainSingle(a => a.TargetId == new TargetId(chainId));
    }
}
