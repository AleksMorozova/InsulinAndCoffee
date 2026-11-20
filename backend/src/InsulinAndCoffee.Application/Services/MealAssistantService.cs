using System.Diagnostics;
using System.Text.Json;
using InsulinAndCoffee.Application.Abstractions;
using InsulinAndCoffee.Application.Dtos;
using InsulinAndCoffee.Domain.Entities;
using InsulinAndCoffee.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InsulinAndCoffee.Application.Services;

public class MealAssistantService(IAppDbContext db, ICurrentUser currentUser, ILlmClient llm,
    MealHistorySearchService history, TimeProvider clock, ILogger<MealAssistantService> logger, AssistantDraftBuilder? drafts = null, FoodKnowledgeSearchService? knowledge = null)
{
    public async Task<IReadOnlyList<AssistantConversationSummary>> ListAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        return await db.AssistantConversations.AsNoTracking().Where(item => item.UserId == userId)
            .OrderByDescending(item => item.UpdatedAt).ThenBy(item => item.Id).Take(50)
            .Select(item => new AssistantConversationSummary(item.Id,
                item.Messages.Where(message => message.Role == "user").OrderBy(message => message.CreatedAt)
                    .Select(message => message.Content).FirstOrDefault() ?? "Conversation", item.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AssistantTurn>> ReadAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        var conversation = await db.AssistantConversations.AsNoTracking()
            .Where(item => item.Id == id && item.UserId == userId)
            .Include(item => item.Messages.OrderBy(message => message.CreatedAt).ThenBy(message => message.Id))
            .SingleOrDefaultAsync(cancellationToken) ?? throw new NotFoundException("Conversation", id);
        var turns = new List<AssistantTurn>();
        string? question = null;
        foreach (var message in conversation.Messages)
        {
            if (message.Role == "user") question = message.Content;
            else if (question is not null)
            {
                var response = message.ResponseJson is null ? new MealAssistantResponse(id, message.Content, [])
                    : JsonSerializer.Deserialize<MealAssistantResponse>(message.ResponseJson);
                if (response is not null) turns.Add(new AssistantTurn(question, response));
                question = null;
            }
        }
        return turns;
    }

    public async Task<IReadOnlyList<CalculatedMealItemDto>> PrepareDraftAsync(Guid id, CancellationToken cancellationToken)
    {
        var turns = await ReadAsync(id, cancellationToken);
        var draft = turns.LastOrDefault(turn => turn.Answer.Draft is not null)?.Answer.Draft
            ?? throw new ValidationException("This conversation has no meal draft.");
        if (drafts is null) throw new InvalidOperationException("The assistant draft builder is unavailable.");
        return await drafts.PrepareAsync(draft.Items, cancellationToken, draft.Sources);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        var conversation = await db.AssistantConversations
            .SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Conversation", id);
        db.AssistantConversations.Remove(conversation);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<MealAssistantResponse> SendAsync(MealAssistantRequest request, CancellationToken cancellationToken)
    {
        var message = request.Message?.Trim();
        if (string.IsNullOrEmpty(message) || message.Length > 1000)
            throw new ValidationException("Enter a message between 1 and 1,000 characters.");

        var started = Stopwatch.GetTimestamp();
        logger.LogInformation("Meal assistant request started");
        var userId = currentUser.UserId;
        var requestMessageId = request.RequestId is Guid requestId
            ? new Guid(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes($"{userId:D}/{requestId:D}"))[..16])
            : Guid.NewGuid();
        if (request.RequestId.HasValue)
        {
            var saved = await db.AssistantMessages.AsNoTracking()
                .Where(item => item.Id == requestMessageId && item.Role == "assistant" &&
                    db.AssistantConversations.Any(chat => chat.Id == item.ConversationId && chat.UserId == userId))
                .Select(item => item.ResponseJson).SingleOrDefaultAsync(cancellationToken);
            if (saved is not null)
            {
                logger.LogInformation("Meal assistant replay served with {LlmCalls} provider calls", 0);
                return JsonSerializer.Deserialize<MealAssistantResponse>(saved)
                    ?? throw new InvalidOperationException("The saved assistant response is unavailable.");
            }
        }
        var now = clock.GetUtcNow();
        var conversation = request.ConversationId is Guid id
            ? await db.AssistantConversations.SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId, cancellationToken)
                ?? throw new NotFoundException("Conversation", id)
            : new AssistantConversation { Id = Guid.NewGuid(), UserId = userId };

        var recent = await db.AssistantMessages.Where(item => item.ConversationId == conversation.Id)
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Take(20).ToListAsync(cancellationToken);
        var active = recent.Where(item => item.Role == "assistant" && item.ResponseJson is not null)
            .Select(item => (Message: item, Draft: JsonSerializer.Deserialize<MealAssistantResponse>(item.ResponseJson!)?.Draft))
            .FirstOrDefault(entry => entry.Draft is not null);
        var includedDraft = false;
        var includedSources = false;
        var contextMessages = recent.Take(6).ToList();
        if (active.Message is not null && !contextMessages.Contains(active.Message)) contextMessages.Add(active.Message);
        var context = contextMessages.Select(item =>
        {
            if (item.ResponseJson is null || (includedDraft && includedSources)) return new AssistantContext(item.Role, item.Content);
            var saved = JsonSerializer.Deserialize<MealAssistantResponse>(item.ResponseJson);
            var draft = includedDraft ? null : saved?.Draft;
            var content = item.Content;
            if (!includedSources && saved?.Knowledge is { Orders.Count: > 0 } knowledgeResult)
            {
                includedSources = true;
                content += " Recent saved order references: " + JsonSerializer.Serialize(knowledgeResult.Orders.Take(3).Select(order => new { order.PlaceName, order.DishName }));
            }
            if (draft is null) return new AssistantContext(item.Role, content);
            includedDraft = true;
            var portions = draft.Items.Select(food => new { name = food.FoodName, food.Quantity, unit = food.MeasurementType.ToString() });
            return new AssistantContext(item.Role, content + " Matched draft portions: " + JsonSerializer.Serialize(portions) + (draft.Pending is null ? "" : " Pending clarification: " + JsonSerializer.Serialize(draft.Pending)) + (draft.PendingEdit is null ? "" : " Pending quantity edit: " + JsonSerializer.Serialize(draft.PendingEdit)));
        })
            .Reverse().Append(new AssistantContext("user", message)).ToArray();
        var recentMeals = message.Equals("What did I eat last time?", StringComparison.OrdinalIgnoreCase);
        var lookupName = FoodKnowledgeSearchService.LookupName(message);
        var namedLookup = active.Draft?.PendingEdit is null && FoodKnowledgeSearchService.IsKnownTerm(lookupName);
        var search = recentMeals ? new AssistantSearch([], "recent")
            : namedLookup ? new AssistantSearch([new(lookupName, null, null)], "lookup")
            : await llm.InterpretAsync(context, cancellationToken);
        if (recentMeals) logger.LogInformation("Meal assistant recent history handled with {LlmCalls} provider calls", 0);
        if (search.Intent is not ("history" or "recent" or "draft" or "lookup") || search.Foods is null || search.Foods.Length > 6 || search.Foods.Any(food =>
                food is null || string.IsNullOrWhiteSpace(food.Name) || food.Name.Length > 80 ||
                !food.Name.Any(char.IsLetterOrDigit) || food.Quantity is <= 0 or > 10000 ||
                (food.Unit is { } unit && !Enum.IsDefined(unit))))
            throw new MealAssistantUnavailableException("The assistant could not interpret the search. Try rephrasing your question.");

        search = search with
        {
            Foods = search.Foods.Select(AssistantPortionText.Normalize).ToArray(),
            Target = string.IsNullOrWhiteSpace(search.Target) ? search.Target
                : AssistantPortionText.Normalize(new AssistantFoodQuery(search.Target, null, null)).Name
        };
        if (search.Foods.Any(food => food.Quantity is <= 0 or > 10000))
            throw new ValidationException("Specify a quantity between zero and 10,000.");
        if (search.Operation is not (null or "create" or "add" or "clarify" or "remove" or "replace" or "set" or "increase" or "decrease" or "clear" or "show")
            || search.Target?.Length > 160 || search.Foods.Any(food => food.Restaurant?.Length > 180))
            throw new MealAssistantUnavailableException("The draft edit could not be interpreted.");
        logger.LogInformation("Meal assistant routing {Intent}, {TermCount} terms, {Deterministic} deterministic", search.Intent, search.Foods.Length, recentMeals || namedLookup);
        var searchStarted = Stopwatch.GetTimestamp();
        var meals = search.Intent == "draft" || (search.Foods.Length == 0 && search.Intent != "recent")
            ? [] : await history.SearchAsync(search, cancellationToken);
        var sources = search.Intent == "lookup" && knowledge is not null
            ? await knowledge.SearchAsync(search.Foods, cancellationToken) : null;
        logger.LogInformation("Meal assistant history search returned {Count} records in {ElapsedMs} ms",
            meals.Count, Stopwatch.GetElapsedTime(searchStarted).TotalMilliseconds);
        var reply = meals.Count == 0
            ? "I couldn't find matching meals in your saved history."
            : "Here are saved meals from your history. Suggested and confirmed doses are shown separately as historical values.";
        if (search.Foods.Length == 0 && search.Intent != "recent") reply = "Which food, order, or recorded meal do you mean?";
        var draftStarted = Stopwatch.GetTimestamp();
        var draft = search.Intent == "draft" && drafts is not null
            ? await drafts.UpdateAsync(search, active.Draft, cancellationToken) : null;
        if (search.Intent == "draft")
        {
            logger.LogInformation("Meal assistant draft took {ElapsedMs} ms", Stopwatch.GetElapsedTime(draftStarted).TotalMilliseconds);
            reply = draft is null ? "Tell me which foods and portions you want to include."
                : draft.Issues.Count > 0 ? "Review the matched foods and clarify any missing items."
                : "Here is your draft using your saved foods.";
        }
        if (sources is not null && (sources.Foods.Count > 0 || sources.Orders.Count > 0) && draft?.Items.Count is not > 0)
            reply = "Here’s what I found in your saved records.";
        if (draft is { Issues.Count: > 0, EditFeedback: null } && knowledge is not null && search.Foods.Length > 0)
            sources = await knowledge.SearchAsync(search.Foods, cancellationToken);
        if (draft is not null)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(message, "[іїєґІЇЄҐ]|борщу|хліб|дві|прибери|додай"))
                reply = draft.Issues.Count > 0 ? "Перевір продукти, які вдалося знайти, й уточни решту." : "Ось твій план прийому їжі.";
            else if (System.Text.RegularExpressions.Regex.IsMatch(message, "[а-яА-ЯёЁ]"))
                reply = draft.Issues.Count > 0 ? "Проверь найденные продукты и уточни остальные." : "Вот твой план приёма пищи.";
        }
        if (namedLookup && active.Draft is { Items.Count: 0, Issues.Count: > 0 })
            draft = new AssistantDraft([], [], 0m);
        var response = new MealAssistantResponse(conversation.Id, draft?.EditFeedback ?? reply, meals, draft, sources);
        if (request.ConversationId is null) db.AssistantConversations.Add(conversation);
        conversation.UpdatedAt = now;
        var preserve = draft is null && active.Message is not null
            ? recent.Where(item => item.CreatedAt == active.Message.CreatedAt ||
                item.CreatedAt == active.Message.CreatedAt.AddTicks(-10)).Select(item => item.Id).ToHashSet()
            : [];
        var removeCount = Math.Max(0, recent.Count - 18);
        db.AssistantMessages.RemoveRange(recent.AsEnumerable().Reverse().Where(item => !preserve.Contains(item.Id)).Take(removeCount));
        db.AssistantMessages.AddRange(
            new AssistantMessage { ConversationId = conversation.Id, Role = "user", Content = message, CreatedAt = now },
            new AssistantMessage { Id = requestMessageId, ConversationId = conversation.Id, Role = "assistant", Content = reply, ResponseJson = JsonSerializer.Serialize(response), CreatedAt = now.AddTicks(10) });
        var saveStarted = Stopwatch.GetTimestamp();
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Meal assistant persistence took {ElapsedMs} ms", Stopwatch.GetElapsedTime(saveStarted).TotalMilliseconds);
        logger.LogInformation("Meal assistant request completed in {ElapsedMs} ms", Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return response;
    }
}
