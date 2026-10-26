using System.ComponentModel.DataAnnotations;
using RoomTrack.GuestRequests.Domain.Model.ValueObjects;
using RoomTrack.IAM.Domain.Model.Constants;
using RoomTrack.IAM.Interfaces.Authorization;
using RoomTrack.Messaging.Domain.Model.Aggregates;
using RoomTrack.Messaging.Domain.Model.Commands;
using RoomTrack.Messaging.Domain.Model.Entities;
using RoomTrack.Messaging.Domain.Model.Queries;
using RoomTrack.Messaging.Domain.Model.ValueObjects;
using RoomTrack.Messaging.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace RoomTrack.Messaging.Interfaces.REST;

public record PostMessageResource([Required] string? Body);

public record MessageResource(long Id, int ConversationId, int SenderUserId, string SenderName, bool SenderIsGuest,
    string Body, DateTimeOffset SentAt);

public record ConversationResource(int Id, int HotelId, ConversationKind Kind, int? GuestUserId, ServiceArea? Area,
    ThreadSubject? Subject, int? SubjectId, DateTimeOffset CreatedAt, DateTimeOffset? LastMessageAt, int MessageCount);

/// <summary>A conversation with its latest messages (newest first).</summary>
public record ConversationWithMessagesResource(ConversationResource Conversation, IReadOnlyList<MessageResource> Messages);

/// <summary>
///     Conversations inside the platform: a guest with the front desk (US-20), the channel of each operational area
///     and the comments on a task (US-12). The other side is notified of every message.
/// </summary>
[Authorize(Policy = Policies.UseConversations)]
[ApiController]
[Route("api/v1/conversations")]
[Produces("application/json")]
[SwaggerTag("Conversations: guests with the front desk, area channels and comments on tasks")]
public class ConversationsController(
    IMessagingCommandService commandService,
    IMessagingQueryService queryService) : ControllerBase
{
    /// <summary>
    ///     The conversations I take part in, latest activity first: a guest their conversations with the front desk;
    ///     staff the channels of their area (the front desk and the administrators also the conversations of the guests).
    /// </summary>
    [HttpGet]
    [SwaggerOperation(Summary = "List my conversations", OperationId = "GetConversations")]
    [ProducesResponseType(typeof(IEnumerable<ConversationResource>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConversations([FromQuery] int? hotelId) =>
        Ok((await queryService.Handle(new GetConversationsQuery(Actor(), hotelId))).Select(ToResource));

    /// <summary>A guest writes to the front desk of the hotel of their stay today (US-20).</summary>
    [HttpPost("front-desk/messages")]
    [SwaggerOperation(Summary = "Write to the front desk (guest)", OperationId = "WriteToFrontDesk")]
    [ProducesResponseType(typeof(MessageResource), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> WriteToFrontDesk([FromBody] PostMessageResource resource)
    {
        if (!User.IsGuest()) return Forbid();
        return Created(await commandService.Handle(new WriteToFrontDeskCommand(resource.Body!, Actor())));
    }

    /// <summary>A staff member writes to the channel of an area of their hotel (US-12): everyone of the area gets it.</summary>
    [HttpPost("areas/{area}/messages")]
    [Authorize(Policy = Policies.ViewRoomOperations)]
    [SwaggerOperation(Summary = "Write to the channel of an area", OperationId = "WriteToArea")]
    [ProducesResponseType(typeof(MessageResource), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> WriteToArea(ServiceArea area, [FromBody] PostMessageResource resource, [FromQuery] int? hotelId)
    {
        var targetHotelId = User.IsChainAdmin() ? hotelId : User.GetHotelId();
        if (targetHotelId is null) return Forbid();
        return Created(await commandService.Handle(new WriteToAreaCommand(targetHotelId.Value, area, resource.Body!, Actor())));
    }

    /// <summary>Comments on a cleaning task, an incident or a guest request (US-12); its assignee is notified.</summary>
    [HttpPost("threads/{subject}/{subjectId:int}/messages")]
    [Authorize(Policy = Policies.ViewRoomOperations)]
    [SwaggerOperation(Summary = "Comment on a task", OperationId = "CommentOnTask")]
    [ProducesResponseType(typeof(MessageResource), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CommentOnTask(ThreadSubject subject, int subjectId, [FromBody] PostMessageResource resource)
    {
        var message = await commandService.Handle(new CommentOnTaskCommand(subject, subjectId, resource.Body!, Actor()));
        return message is null ? NotFound() : Created(message);
    }

    /// <summary>The comments on a task with its latest messages, also once it is closed (US-12, scenario 3).</summary>
    [HttpGet("threads/{subject}/{subjectId:int}")]
    [Authorize(Policy = Policies.ViewRoomOperations)]
    [SwaggerOperation(Summary = "Get the comments on a task", OperationId = "GetTaskThread")]
    [ProducesResponseType(typeof(ConversationWithMessagesResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTaskThread(ThreadSubject subject, int subjectId, [FromQuery] int limit = 50)
    {
        var conversation = await queryService.Handle(new GetTaskThreadQuery(subject, subjectId));
        return conversation is null || !conversation.CanAccess(Actor()) ? NotFound() : Ok(await WithMessagesAsync(conversation, null, limit));
    }

    /// <summary>A conversation with a page of its messages, newest first (<c>before</c>: the id of the oldest message seen).</summary>
    [HttpGet("{conversationId:int}")]
    [SwaggerOperation(Summary = "Get a conversation and its messages", OperationId = "GetConversation")]
    [ProducesResponseType(typeof(ConversationWithMessagesResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConversation(int conversationId, [FromQuery] long? before, [FromQuery] int limit = 50)
    {
        var conversation = await queryService.Handle(new GetConversationByIdQuery(conversationId));
        return conversation is null || !conversation.CanAccess(Actor()) ? NotFound() : Ok(await WithMessagesAsync(conversation, before, limit));
    }

    /// <summary>Writes in a conversation I take part in (e.g. the front desk answers a guest).</summary>
    [HttpPost("{conversationId:int}/messages")]
    [SwaggerOperation(Summary = "Write in a conversation", OperationId = "PostMessage")]
    [ProducesResponseType(typeof(MessageResource), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PostMessage(int conversationId, [FromBody] PostMessageResource resource)
    {
        var conversation = await queryService.Handle(new GetConversationByIdQuery(conversationId));
        // Somebody outside the conversation does not learn that it exists.
        if (conversation is null || !conversation.CanAccess(Actor())) return NotFound();
        var message = await commandService.Handle(new PostMessageCommand(conversationId, resource.Body!, Actor()));
        return message is null ? NotFound() : Created(message);
    }

    /// <summary>The role that decides what the requester may read and write (one per account).</summary>
    private ConversationActor Actor()
    {
        string[] byPrecedence = [UserRoles.ChainAdmin, UserRoles.Admin, UserRoles.Reception, UserRoles.Housekeeping,
            UserRoles.Maintenance, UserRoles.Guest];
        return new ConversationActor(User.GetUserId(), User.GetUsername(),
            byPrecedence.FirstOrDefault(User.IsInRole) ?? UserRoles.Guest, User.GetHotelId());
    }

    private CreatedAtActionResult Created(ConversationMessage message) =>
        CreatedAtAction(nameof(GetConversation), new { conversationId = message.ConversationId }, ToResource(message));

    private async Task<ConversationWithMessagesResource> WithMessagesAsync(Conversation conversation, long? before, int limit) =>
        new(ToResource(conversation),
            (await queryService.Handle(new GetMessagesQuery(conversation.Id, before, limit))).Select(ToResource).ToList());

    private static ConversationResource ToResource(Conversation conversation) =>
        new(conversation.Id, conversation.HotelId, conversation.Kind, conversation.GuestUserId, conversation.Area,
            conversation.Subject, conversation.SubjectId, conversation.CreatedAt, conversation.LastMessageAt, conversation.MessageCount);

    private static MessageResource ToResource(ConversationMessage message) =>
        new(message.Id, message.ConversationId, message.SenderUserId, message.SenderName, message.SenderIsGuest, message.Body,
            message.SentAt);
}
