using FluentAssertions;
using RoomTrack.IAM.Domain.Model.Constants;
using RoomTrack.IAM.Domain.Model.Exceptions;
using RoomTrack.IAM.Domain.Services;
using RoomTrack.Shared.Domain.Model.Exceptions;
using Xunit;
using static RoomTrack.Identity.API.Tests.IAM.IdentityTestData;

namespace RoomTrack.Identity.API.Tests.IAM.Domain;

/// <summary>Who can see and manage whom: role hierarchy plus hotel/chain scope (D2).</summary>
public class AuthorizationRulesTests
{
    private readonly UserScopeService _scope = new();
    private readonly RoleAuthorizationService _authorization = new(new UserScopeService());

    [Fact]
    public void Staff_ShouldOnlySeeUsersOfTheirHotel()
    {
        var reception = NewUser(1, UserRoles.Reception, hotelId: 1);

        _scope.CanAccessUser(reception, NewUser(2, UserRoles.Housekeeping, hotelId: 1)).Should().BeTrue();
        _scope.CanAccessUser(reception, NewUser(3, UserRoles.Housekeeping, hotelId: 2)).Should().BeFalse();
        _scope.CanAccessHotel(reception, 1).Should().BeTrue();
        _scope.CanAccessHotel(reception, 2).Should().BeFalse();
    }

    [Fact]
    public void AGuest_ShouldOnlySeeThemselves()
    {
        var guest = NewUser(1, UserRoles.Guest);

        _scope.CanAccessUser(guest, guest).Should().BeTrue();
        _scope.CanAccessUser(guest, NewUser(2, UserRoles.Guest)).Should().BeFalse();
    }

    [Fact]
    public void AChainAdmin_ShouldSeeTheirChain_AndAGlobalOneEveryone()
    {
        var chainAdmin = NewUser(1, UserRoles.ChainAdmin, chainId: 10);
        var globalAdmin = NewUser(2, UserRoles.ChainAdmin);

        _scope.CanAccessUser(chainAdmin, NewUser(3, UserRoles.Admin, hotelId: 1, chainId: 10)).Should().BeTrue();
        _scope.CanAccessUser(chainAdmin, NewUser(4, UserRoles.Admin, hotelId: 2, chainId: 11)).Should().BeFalse();
        _scope.CanAccessUser(globalAdmin, NewUser(4, UserRoles.Admin, hotelId: 2, chainId: 11)).Should().BeTrue();
    }

    [Fact]
    public void Managing_ShouldNeedAHigherRole_InScope_AndNeverOneself()
    {
        var admin = NewUser(1, UserRoles.Admin, hotelId: 1);

        _authorization.CanManage(admin, NewUser(2, UserRoles.Reception, hotelId: 1)).Should().BeTrue();
        _authorization.CanManage(admin, NewUser(3, UserRoles.Reception, hotelId: 2)).Should().BeFalse();
        _authorization.CanManage(admin, NewUser(4, UserRoles.Admin, hotelId: 1)).Should().BeFalse();
        _authorization.CanManage(admin, admin).Should().BeFalse();
    }

    [Fact]
    public void Roles_ShouldOnlyBeAssignedBelowOnesOwn_AndChainAdminOnlyByAGlobalChainAdmin()
    {
        var admin = NewUser(1, UserRoles.Admin, hotelId: 1);
        var chainAdmin = NewUser(2, UserRoles.ChainAdmin, chainId: 10);
        var globalAdmin = NewUser(3, UserRoles.ChainAdmin);

        _authorization.CanAssignRole(admin, UserRoles.Reception).Should().BeTrue();
        _authorization.CanAssignRole(admin, UserRoles.Admin).Should().BeFalse();
        _authorization.CanAssignRole(chainAdmin, UserRoles.ChainAdmin).Should().BeFalse();
        // A chain admin is never above another chain admin, so not even a global one can create one.
        _authorization.CanAssignRole(globalAdmin, UserRoles.ChainAdmin).Should().BeFalse();
        _authorization.CanAssignChainId(globalAdmin, 10).Should().BeTrue();
        _authorization.CanAssignChainId(chainAdmin, 10).Should().BeFalse();
    }

    [Fact]
    public void AHotelAdmin_ShouldCreateStaffOnlyForTheirOwnHotel()
    {
        var admin = NewUser(1, UserRoles.Admin, hotelId: 1);
        var adminWithoutHotel = NewUser(2, UserRoles.Admin);

        StaffAccountPolicy.ResolveHotel(admin, UserRoles.Reception, null).Should().Be(1);
        StaffAccountPolicy.ResolveHotel(admin, UserRoles.Reception, 1).Should().Be(1);
        var otherHotel = () => StaffAccountPolicy.ResolveHotel(admin, UserRoles.Reception, 2);
        var noHotel = () => StaffAccountPolicy.ResolveHotel(adminWithoutHotel, UserRoles.Reception, null);

        otherHotel.Should().Throw<UnauthorizedOperationException>().Which.Code.Should().Be(IamErrorCodes.HotelOutOfScope);
        noHotel.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(IamErrorCodes.AdminWithoutHotel);
    }

    [Fact]
    public void AChainAdmin_ShouldChooseTheHotel_WhichFrontDeskStaffMustHave()
    {
        var chainAdmin = NewUser(1, UserRoles.ChainAdmin);

        StaffAccountPolicy.ResolveHotel(chainAdmin, UserRoles.Maintenance, 3).Should().Be(3);
        StaffAccountPolicy.ResolveHotel(chainAdmin, UserRoles.Admin, null).Should().BeNull();
        var noHotel = () => StaffAccountPolicy.ResolveHotel(chainAdmin, UserRoles.Housekeeping, null);

        noHotel.Should().Throw<DomainValidationException>().Which.Code.Should().Be(IamErrorCodes.HotelRequired);
    }
}
