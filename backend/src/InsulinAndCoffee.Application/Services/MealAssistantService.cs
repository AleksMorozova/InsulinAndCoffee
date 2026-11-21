using System.Diagnostics;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
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
    private static readonly JsonSerializerOptions StoredJson = new() { PropertyNameCaseInsensitive = true };
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
                    : JsonSerializer.Deserialize<MealAssistantResponse>(message.ResponseJson, StoredJson);
                if (response is not null) turns.Add(new AssistantTurn(question, response));
                question = null;
            }
        }
        if (turns.Count > 0)
        {
            var draftIndex = turns.FindLastIndex(turn => turn.Answer.Draft is not null);
            if (draftIndex >= 0 && conversation.DraftJson is not null)
                turns[draftIndex] = turns[draftIndex] with { Answer = turns[draftIndex].Answer with
                { Draft = JsonSerializer.Deserialize<AssistantDraft>(conversation.DraftJson, StoredJson), DraftVersion = conversation.DraftVersion } };
            turns[^1] = turns[^1] with { Answer = turns[^1].Answer with { DraftVersion = conversation.DraftVersion } };
        }
        return turns;
    }

    public async Task<AssistantDraftState> GetDraftStateAsync(Guid id, CancellationToken cancellationToken)
    {
        var owner = currentUser.UserId;
        var conversation = await db.AssistantConversations.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == id && item.UserId == owner, cancellationToken) ?? throw new NotFoundException("Conversation", id);
        var draft = conversation.DraftJson is not null ? JsonSerializer.Deserialize<AssistantDraft>(conversation.DraftJson, StoredJson)
            : (await ReadAsync(id, cancellationToken)).LastOrDefault(turn => turn.Answer.Draft is not null)?.Answer.Draft;
        return new(conversation.DraftVersion, draft);
    }

    public async Task<IReadOnlyList<CalculatedMealItemDto>> PrepareDraftAsync(Guid id, CancellationToken cancellationToken)
    {
        var draft = (await GetDraftStateAsync(id, cancellationToken)).Draft
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
        var receipts = await db.AssistantRequestReceipts.Where(receipt => receipt.UserId == userId && receipt.ConversationId == id)
            .ToListAsync(cancellationToken);
        foreach (var receipt in receipts) receipt.ResponseJson = "";
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            db.DiscardChanges();
            throw new AssistantConflictException("draft_version_conflict", "The conversation changed while it was being deleted. Refresh before deleting it.");
        }
    }

    public async Task<MealAssistantResponse> SendAsync(MealAssistantRequest request, CancellationToken cancellationToken)
    {
        var message = request.Message?.Trim();
        if (string.IsNullOrEmpty(message) || message.Length > 1000)
            throw new ValidationException("Enter a message between 1 and 1,000 characters.");
        if (request.ExpectedDraftVersion is < 0 || request.RequestId == Guid.Empty)
            throw new ValidationException("Send a valid request ID and non-negative draft version.");

        var started = Stopwatch.GetTimestamp();
        logger.LogInformation("Meal assistant request started");
        var userId = currentUser.UserId;
        var payloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            request.ConversationId, Message = message, request.ExpectedDraftVersion,
            Selection = request.Selection is null ? null : request.Selection.Kind == "Food" ? (object)new
            { request.Selection.FoodId, Quantity = request.Selection.Quantity?.ToString("G29", CultureInfo.InvariantCulture), request.Selection.Unit }
                : new { request.Selection.FoodId, Quantity = request.Selection.Quantity?.ToString("G29", CultureInfo.InvariantCulture), request.Selection.Unit, request.Selection.Kind }
        }))));
        var requestMessageId = request.RequestId is Guid requestId
            ? new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{userId:D}/{requestId:D}"))[..16])
            : Guid.NewGuid();
        if (request.RequestId.HasValue)
        {
            var replay = await ReplayAsync(request.RequestId.Value, payloadHash, cancellationToken);
            if (replay is not null) return replay;
            if (await db.AssistantMessages.AsNoTracking().AnyAsync(item => item.Id == requestMessageId &&
                    db.AssistantConversations.Any(chat => chat.Id == item.ConversationId && chat.UserId == userId), cancellationToken))
                throw new AssistantConflictException("assistant_legacy_retry", "This older request has no verifiable retry receipt. Refresh the draft before sending a new request.");
        }
        var now = clock.GetUtcNow();
        var conversation = request.ConversationId is Guid id
            ? await db.AssistantConversations.SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId, cancellationToken)
                ?? throw new NotFoundException("Conversation", id)
            : new AssistantConversation { Id = Guid.NewGuid(), UserId = userId };

        var recent = await db.AssistantMessages.Where(item => item.ConversationId == conversation.Id)
            .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Take(20).ToListAsync(cancellationToken);
        var legacy = recent.Where(item => item.Role == "assistant" && item.ResponseJson is not null)
            .Select(item => (Message: item, Draft: JsonSerializer.Deserialize<MealAssistantResponse>(item.ResponseJson!, StoredJson)?.Draft))
            .FirstOrDefault(entry => entry.Draft is not null);
        var active = (legacy.Message, Draft: conversation.DraftJson is not null
            ? JsonSerializer.Deserialize<AssistantDraft>(conversation.DraftJson, StoredJson) : legacy.Draft);
        var includedDraft = false;
        var includedSources = false;
        var contextMessages = recent.Take(6).ToList();
        if (active.Message is not null && !contextMessages.Contains(active.Message)) contextMessages.Add(active.Message);
        var context = contextMessages.Select(item =>
        {
            if (item.ResponseJson is null || (includedDraft && includedSources)) return new AssistantContext(item.Role, item.Content);
            var saved = JsonSerializer.Deserialize<MealAssistantResponse>(item.ResponseJson, StoredJson);
            var draft = includedDraft ? null : active.Draft ?? saved?.Draft;
            var content = item.Content;
            if (!includedSources && saved?.Knowledge is { Orders.Count: > 0 } knowledgeResult)
            {
                includedSources = true;
                content += " Recent saved order references: " + JsonSerializer.Serialize(knowledgeResult.Orders.Take(3).Select(order => new { order.PlaceName, order.DishName }));
            }
            if (draft is null) return new AssistantContext(item.Role, content);
            includedDraft = true;
            return new AssistantContext(item.Role, content + DescribeDraft(draft));
        })
            .Reverse().Append(new AssistantContext("user", message)).ToArray();
        if (!includedDraft && active.Draft is not null)
            context = context[..^1].Append(new AssistantContext("assistant", DescribeDraft(active.Draft)))
                .Append(context[^1]).ToArray();
        var recentMeals = message.Equals("What did I eat last time?", StringComparison.OrdinalIgnoreCase);
        var lookupName = FoodKnowledgeSearchService.LookupName(message);
        var standalone = active.Draft?.PendingEdit is null && active.Draft?.Pending is null;
        var lookup = AssistantFoodMatching.Query(new(lookupName, null, null));
        var exactFood = standalone && knowledge is not null && !string.IsNullOrWhiteSpace(lookup.Name)
            && string.IsNullOrWhiteSpace(lookup.Restaurant)
            && await FoodKnowledgeSearchService.FindFoods(db.FoodItems.AsNoTracking().Where(food => food.UserId == userId), lookup.Name, false)
                .AnyAsync(AssistantFoodMatching.Positive(AssistantFoodMatching.On<FoodItem>(food => food.Name,
                    AssistantFoodMatching.Score(lookup.Name)), 3), cancellationToken);
        var restaurantLookup = lookup.Restaurant is not null && (lookup.Name.Length == 0
            || FoodKnowledgeSearchService.IsKnownTerm(lookup.Name));
        var namedLookup = standalone && (FoodKnowledgeSearchService.IsKnownTerm(lookupName) || exactFood || restaurantLookup);
        AssistantSearch? selectedSearch = null;
        var ordinalUnresolved = false;
        var ordinal = Regex.Match(message, @"(?i)\b(first|second|third|1st|2nd|3rd)\s+item\b|\b(перв\w*|втор\w*|трет\w*)\s+(?:позици\w*|блюд\w*)");
        if (request.Selection is null && ordinal.Success && Regex.IsMatch(message, @"(?i)order|заказ|замовлен"))
        {
            var positionText = ordinal.Groups[1].Success ? ordinal.Groups[1].Value.ToLowerInvariant() : ordinal.Groups[2].Value.ToLowerInvariant();
            var position = positionText is "first" or "1st" || positionText.StartsWith("перв") ? 0
                : positionText is "second" or "2nd" || positionText.StartsWith("втор") ? 1 : 2;
            var remembered = recent.Where(turn => turn.Role == "assistant" && turn.ResponseJson is not null)
                .Select(turn => JsonSerializer.Deserialize<MealAssistantResponse>(turn.ResponseJson!, StoredJson)?.Knowledge)
                .FirstOrDefault(result => result is not null && (result.Orders.Count > 0 || result.Items?.Count > 0));
            var referenced = (remembered?.Orders.Select(order => order.Id) ?? []).Concat(remembered?.Items?.Select(item => item.OrderId) ?? []).Distinct().ToArray();
            var possible = db.DeliveryMeals.AsNoTracking().Where(order => order.UserId == userId);
            if (referenced.Length > 0) possible = possible.Where(order => referenced.Contains(order.Id));
            var knownOrders = await possible.Select(order => order.Id).Take(2).ToListAsync(cancellationToken);
            if (knownOrders.Count == 1)
            {
                var source = await db.DeliveryMealItems.AsNoTracking().Where(item => item.DeliveryMealId == knownOrders[0])
                    .OrderBy(item => item.DisplayOrder).Skip(position).FirstOrDefaultAsync(cancellationToken);
                if (source is not null)
                    selectedSearch = new([new(source.Name, source.Quantity, source.MeasurementType, DeliveryMealItemId: source.Id)], "draft",
                        active.Draft is not null ? "add" : "create");
            }
            if (selectedSearch is null) { ordinalUnresolved = true; selectedSearch = new([], "lookup"); }
        }
        if (request.Selection is { Kind: "DeliveryMealItem" or "DeliveryMeal" } orderSelection)
        {
            if (orderSelection.Quantity is <= 0 or > 10000 || orderSelection.Unit is { } orderUnit && !Enum.IsDefined(orderUnit))
                throw new ValidationException("Specify a valid quantity and measurement unit.");
            string selectedName;
            if (orderSelection.Kind == "DeliveryMealItem")
                selectedName = await db.DeliveryMealItems.AsNoTracking().Where(item => item.Id == orderSelection.FoodId && item.DeliveryMeal!.UserId == userId)
                    .Select(item => item.Name).SingleOrDefaultAsync(cancellationToken) ?? throw new ValidationException("The selected order item is unavailable.");
            else
                selectedName = await db.DeliveryMeals.AsNoTracking().Where(order => order.Id == orderSelection.FoodId && order.UserId == userId)
                    .Select(order => order.DishName).SingleOrDefaultAsync(cancellationToken) ?? throw new ValidationException("The selected order is unavailable.");
            selectedSearch = new([new(selectedName, orderSelection.Quantity, orderSelection.Unit,
                DeliveryMealItemId: orderSelection.Kind == "DeliveryMealItem" ? orderSelection.FoodId : null,
                DeliveryMealId: orderSelection.Kind == "DeliveryMeal" ? orderSelection.FoodId : null)], "draft",
                active.Draft?.Pending is not null ? "clarify" : active.Draft is not null ? "add" : "create");
        }
        else if (request.Selection is { } selection)
        {
            if (selection.Kind != "Food") throw new ValidationException("Unknown saved source kind.");
            if (selection.Quantity is <= 0 or > 10000 || selection.Unit is { } selectedUnit && !Enum.IsDefined(selectedUnit))
                throw new ValidationException("Specify a valid quantity and measurement unit.");
            if (active.Draft?.Issues.Count > 1)
                throw new ValidationException("Several foods need clarification. Resend the complete meal with the intended foods and quantities.");
            var selectedFood = await db.FoodItems.AsNoTracking().SingleOrDefaultAsync(
                food => food.Id == selection.FoodId && food.UserId == userId, cancellationToken)
                ?? throw new ValidationException("The selected food is no longer available. Search again.");
            if (selection.Quantity is null && active.Draft?.Pending is { Quantity: not null, Unit: { } pendingUnit }
                && pendingUnit != selectedFood.MeasurementType)
                throw new ValidationException($"Enter a quantity in {selectedFood.MeasurementType} for the selected food.");
            selectedSearch = new([new(selectedFood.Name, selection.Quantity ?? active.Draft?.Pending?.Quantity,
                selection.Unit ?? selectedFood.MeasurementType, FoodItemId: selectedFood.Id)], "draft",
                active.Draft?.Pending is not null ? "clarify" : active.Draft is not null ? "add" : "create");
        }
        var search = selectedSearch ?? (recentMeals ? new AssistantSearch([], "recent")
            : namedLookup ? new AssistantSearch([new(lookupName, null, null)], "lookup")
            : await llm.InterpretAsync(context, cancellationToken));
        if (recentMeals) logger.LogInformation("Meal assistant recent history handled with {LlmCalls} provider calls", 0);
        if (search.Intent is not ("history" or "recent" or "draft" or "lookup") || search.Foods is null || search.Foods.Length > 6 || search.Foods.Any(food =>
                food is null || string.IsNullOrWhiteSpace(food.Name) || (food.FoodItemId is null && food.DeliveryMealId is null && food.DeliveryMealItemId is null && food.Name.Length > 80) ||
                !food.Name.Any(char.IsLetterOrDigit) || food.Quantity is <= 0 or > 10000 ||
                (food.Unit is { } unit && !Enum.IsDefined(unit))))
            throw new MealAssistantUnavailableException("The assistant could not interpret the search. Try rephrasing your question.");

        if (selectedSearch is null)
            search = search with { Foods = search.Foods.Select(food => food with { FoodItemId = null, DeliveryMealId = null, DeliveryMealItemId = null }).ToArray() };

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
        var mutation = search.Intent == "draft" && search.Operation != "show";
        if (mutation)
        {
            if (request.RequestId is null) throw new ValidationException("A request ID is required to edit an assistant draft safely.");
            if (request.ConversationId.HasValue && request.ExpectedDraftVersion is null)
                throw new AssistantConflictException("draft_version_required", "Refresh the draft and send its expected version.", conversation.DraftVersion);
            if ((request.ExpectedDraftVersion ?? 0) != conversation.DraftVersion)
                throw new AssistantConflictException("draft_version_conflict", "The draft changed in another request. Refresh it before applying your edit.", conversation.DraftVersion);
        }
        logger.LogInformation("Meal assistant routing {Intent}, {TermCount} terms, {Deterministic} deterministic", search.Intent, search.Foods.Length, recentMeals || namedLookup || selectedSearch is not null);
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
        if (ordinalUnresolved) reply = "Choose one saved order and an available structured item first. Legacy orders can only be reused as a whole.";
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
        if (sources is not null && (sources.Foods.Count > 0 || sources.Orders.Count > 0 || sources.Items?.Count > 0) && draft?.Items.Count is not > 0)
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
        if (mutation && draft is not null)
        {
            conversation.DraftVersion = checked(conversation.DraftVersion + 1);
            conversation.DraftJson = JsonSerializer.Serialize(draft);
        }
        var response = new MealAssistantResponse(conversation.Id, draft?.EditFeedback ?? reply, meals, draft, sources,
            conversation.DraftVersion, request.RequestId);
        if (request.ConversationId is null) db.AssistantConversations.Add(conversation);
        conversation.UpdatedAt = now;
        conversation.Revision = checked(conversation.Revision + 1);
        var preserve = draft is null && active.Message is not null
            ? recent.Where(item => item.CreatedAt == active.Message.CreatedAt ||
                item.CreatedAt == active.Message.CreatedAt.AddTicks(-10)).Select(item => item.Id).ToHashSet()
            : [];
        var removeCount = Math.Max(0, recent.Count - 18);
        db.AssistantMessages.RemoveRange(recent.AsEnumerable().Reverse().Where(item => !preserve.Contains(item.Id)).Take(removeCount));
        db.AssistantMessages.AddRange(
            new AssistantMessage { ConversationId = conversation.Id, Role = "user", Content = message, CreatedAt = now },
            new AssistantMessage { Id = requestMessageId, ConversationId = conversation.Id, Role = "assistant", Content = reply, ResponseJson = JsonSerializer.Serialize(response), CreatedAt = now.AddTicks(10) });
        if (request.RequestId is Guid receiptId)
            db.AssistantRequestReceipts.Add(new AssistantRequestReceipt
            {
                UserId = userId, RequestId = receiptId, ConversationId = conversation.Id,
                PayloadHash = payloadHash, ResponseJson = JsonSerializer.Serialize(response), CreatedAt = now
            });
        var saveStarted = Stopwatch.GetTimestamp();
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception)
        {
            db.DiscardChanges();
            if (request.RequestId.HasValue)
            {
                var replay = await ReplayAsync(request.RequestId.Value, payloadHash, cancellationToken);
                if (replay is not null) return replay;
            }
            if (exception is not DbUpdateConcurrencyException) throw;
            var latest = await GetDraftStateAsync(conversation.Id, cancellationToken);
            throw new AssistantConflictException("draft_version_conflict", "The conversation changed while this request was running. Refresh before applying your edit.", latest.DraftVersion);
        }
        logger.LogInformation("Meal assistant persistence took {ElapsedMs} ms", Stopwatch.GetElapsedTime(saveStarted).TotalMilliseconds);
        logger.LogInformation("Meal assistant request completed in {ElapsedMs} ms", Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return response;
    }

    private async Task<MealAssistantResponse?> ReplayAsync(Guid requestId, string payloadHash, CancellationToken token)
    {
        var owner = currentUser.UserId;
        var receipt = await db.AssistantRequestReceipts.AsNoTracking().SingleOrDefaultAsync(
            item => item.UserId == owner && item.RequestId == requestId, token);
        if (receipt is null) return null;
        if (receipt.PayloadHash != payloadHash)
            throw new AssistantConflictException("assistant_request_reused", "This request ID was already used with different content. Send a new request ID.");
        if (!await db.AssistantConversations.AsNoTracking().AnyAsync(item => item.Id == receipt.ConversationId && item.UserId == owner, token))
            throw new NotFoundException("Conversation", receipt.ConversationId);
        logger.LogInformation("Meal assistant replay served with {LlmCalls} provider calls", 0);
        return (JsonSerializer.Deserialize<MealAssistantResponse>(receipt.ResponseJson, StoredJson)
            ?? throw new InvalidOperationException("The saved assistant response is unavailable.")) with { Replayed = true };
    }

    private static string DescribeDraft(AssistantDraft draft)
    {
        var portions = draft.Items.Select(food => new { name = food.FoodName, food.Quantity, unit = food.MeasurementType.ToString() });
        var pending = draft.Pending is null ? "" : " Pending clarification: " + JsonSerializer.Serialize(
            new { draft.Pending.Name, draft.Pending.Quantity, draft.Pending.Unit, draft.Pending.Restaurant });
        var edit = draft.PendingEdit is null ? "" : " Pending quantity edit: " + JsonSerializer.Serialize(new
        {
            draft.PendingEdit.Intent, draft.PendingEdit.Operation, draft.PendingEdit.Target,
            Foods = draft.PendingEdit.Foods.Select(food => new { food.Name, food.Quantity, food.Unit, food.Restaurant })
        });
        return " Matched draft portions: " + JsonSerializer.Serialize(portions) + pending + edit;
    }
}
