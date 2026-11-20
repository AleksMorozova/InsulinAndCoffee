import { DatePipe, DecimalPipe } from '@angular/common';
import { afterNextRender, Component, DestroyRef, ElementRef, Injector, ViewChild, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize, firstValueFrom } from 'rxjs';
import { getApiErrorMessage, isApiError } from '../core/api-error';
import { ApiService } from '../core/api.service';
import { AssistantConversation, AssistantDraft, AssistantOrder, AssistantTurn, assistantFoodName } from '../core/meal-assistant.models';

@Component({
  standalone: true,
  host: { class: 'meal-assistant-page' },
  imports: [FormsModule, RouterLink, DatePipe, DecimalPipe],
  template: `
    <section class="assistant">
      <aside class="conversation-sidebar">
        <button class="drawer-toggle" type="button" aria-controls="chat-conversations"
          [attr.aria-expanded]="drawerOpen" (click)="drawerOpen = !drawerOpen">Conversations</button>
        <div id="chat-conversations" class="conversation-panel" [class.open]="drawerOpen">
          <div class="sidebar-heading"><strong>Conversations</strong>
          <button type="button" [disabled]="loading" (click)="newChat()">+ New chat</button></div>
          <div class="conversation-list">
          @if (!conversations.length) { <p>No saved conversations yet.</p> }
          @for (chat of conversations; track chat.id) {
            <div class="conversation-row">
            <button class="conversation" type="button" [disabled]="loading"
              [attr.aria-current]="chat.id === conversationId ? 'true' : null" (click)="openChat(chat.id)">
              <span [title]="chat.title">{{ chat.title }}</span>
              <time [attr.datetime]="chat.updatedAt">{{ chat.updatedAt | date:'mediumDate' }}</time>
            </button>
            <details class="chat-actions" (keydown.escape)="chatMenu.open = false; menuToggle.focus()" #chatMenu>
              <summary #menuToggle [attr.aria-label]="'Actions for ' + chat.title" title="Conversation actions">⋯</summary>
              <button type="button" [disabled]="loading" (click)="chatMenu.open = false; deleteChat(chat.id)">Delete</button>
            </details>
            </div>
          }
          </div>
        </div>
      </aside>
      <div class="chat">
        <div class="chat-header">
      <h1>Meal Assistant</h1>
      <p>Your food memory, ready when you need it.</p>
      <details><summary>Privacy · OpenAI processes chat messages</summary><p>Questions and recent chat context are sent to OpenAI. Saved historical meal records stay in the app.</p></details>
        </div>
        <div #transcript class="transcript" tabindex="0" aria-label="Conversation messages">
      @if (!turns.length && !sending && !failedSend) {
        <div class="welcome">
        <h2>What are we eating today?</h2>
        <p>Ask about a saved meal, compare portions, or build a draft for the calculator.</p>
        <div class="prompt-grid">
          @for (prompt of prompts; track prompt) {
            <button type="button" [disabled]="loading" (click)="sendPrompt(prompt)">{{ prompt }}</button>
          }
        </div>
        </div>
      }
      @for (turn of turns; track $index) {
        <article class="turn">
          <h2>{{ turn.question }}</h2>
          <div class="assistant-reply" [class.has-results]="turn.answer.meals.length || turn.answer.knowledge?.foods?.length || turn.answer.knowledge?.orders?.length">
          <div class="reply-text">
            <span class="assistant-avatar" aria-hidden="true">✦</span>
            <p>{{ responseText(turn, $index) }}</p>
          </div>
          @if (turn.answer.draft?.items?.length && turn.answer.knowledge; as alternatives) {
            <details class="source-choices"><summary>{{ draftLabel(turn.question, 'sources') }}</summary>
            @for (food of alternatives.foods; track food.id) { <p>{{ displayFoodName(food.name) }}</p> }
            @for (order of alternatives.orders; track order.id) { <p>{{ order.placeName }} · {{ order.dishName }}</p> }
            </details>
          }
          @for (food of (!turn.answer.draft?.items?.length ? turn.answer.knowledge?.foods : null) ?? []; track food.id) {
            <section class="card meal">
              <p class="result-meta">Saved Food</p>
              <h3>{{ displayFoodName(food.name) }}</h3>
              @if ((food.unit === 'Grams' ? food.carbsPer100g : food.carbsPerUnit); as carbs) {
                <p>{{ carbs | number }} g carbs per {{ food.unit === 'Grams' ? '100 g' : unitLabel(food.unit) }}</p>
              } @else if ((food.unit === 'Grams' ? food.carbsPer100g : food.carbsPerUnit) === 0) {
                <p>0 g carbs per {{ food.unit === 'Grams' ? '100 g' : unitLabel(food.unit) }}</p>
              } @else { <p>Carbohydrate value unavailable.</p> }
            </section>
          }
          @for (order of (!turn.answer.draft?.items?.length ? turn.answer.knowledge?.orders : null) ?? []; track order.id) {
            <section class="card meal">
              <p class="result-meta">Ask Past Me</p>
              <h3>{{ order.placeName }} · {{ order.dishName }}</h3>
              <p>{{ order.portionDescription }}</p>
              <p><strong>{{ order.carbs | number }} g total carbs</strong></p>
              @if (order.lastUsedAt) { <p>Last reused {{ order.lastUsedAt | date:'mediumDate' }}</p> }
              <button type="button" [disabled]="loading" (click)="useOrder(order)">Reuse whole order</button>
            </section>
          }
          @if ((turn.answer.draft || turn.answer.knowledge?.foods?.length || turn.answer.knowledge?.orders?.length) && turn.answer.meals.length) {
            <h3 class="history-heading">From your history</h3>
          }
          @for (meal of turn.answer.meals.slice(0, expandedTurns.has(turn) ? undefined : 3); track meal.id) {
            <section class="card meal">
              <h3>{{ meal.mealType }} · {{ meal.mealTime | date:'mediumDate' }}</h3>
              <p class="result-meta">{{ meal.match }}</p>
              <ul>
                @for (item of meal.items; track $index) {
                  <li>
                    {{ item.name }} · {{ item.quantity | number }} {{ unitLabel(item.unit) }} · {{ item.carbs | number }} g carbs
                    @if (item.requestedQuantity !== null && (item.requestedQuantity !== item.quantity || item.requestedUnit !== item.unit)) {
                      <div>Requested {{ item.requestedQuantity | number }} {{ item.requestedUnit ? unitLabel(item.requestedUnit) : "" }}.
                        @if (item.estimatedCarbs !== null) {
                          Estimated {{ item.estimatedCarbs | number }} g carbs from this saved food snapshot.
                        } @else {
                          A comparable carb estimate is unavailable.
                        }
                      </div>
                    }
                  </li>
                }
              </ul>
                            <p class="history-total"><strong>Total carbs · {{ meal.totalCarbs | number }} g</strong></p>
              <details class="result-meta"><summary>Historical insulin</summary><p>Calculated dose: {{ meal.suggestedBolus | number }} U (historical)</p>
              <p>
                @if (meal.confirmedBolus !== null) {
                  Actual dose: {{ meal.confirmedBolus | number }} U (historical)
                } @else {
                  Actual dose: Not confirmed
                }
              </p>
              </details>
              <div class="actions">
                <a class="original-link" [routerLink]="['/meals', meal.id]">View original meal</a>
                @if (meal.canUseInCalculator) {
                  <button type="button" (click)="useMeal(meal.id)">Use in Calculator</button>
                }
              </div>
            </section>
          }
          @if (turn.answer.meals.length > 3) {
            <button type="button" (click)="toggleResults(turn)">{{ expandedTurns.has(turn) ? 'Show fewer meals' : 'Show all matching meals' }}</button>
          }
          </div>
        </article>
      }
        @if (sending || failedSend) {
          <article class="turn pending-turn">
            <h2>{{ sending || failedSend?.question }}</h2>
            @if (sending) { <p role="status">Searching your meal history…</p> }
            @else if (error) { <p role="alert">{{ error }}</p> }
          </article>
        }
        </div>
      @if (activeDraft; as draft) {
        @if (draft.items.length) {
          <details class="live-meal" [open]="mealSummaryOpen" (toggle)="mealSummaryOpen = mealDetails.open" #mealDetails>
            <summary>
              <span>{{ draftLabel(activeDraftTurn?.question ?? '', 'title') }}</span>
              <strong>{{ draft.totalCarbs | number }} {{ draftUnit(activeDraftTurn?.question ?? '', 'Grams', draft.totalCarbs) }} ·
                {{ draftLabel(activeDraftTurn?.question ?? '', draft.issues.length ? 'subtotal' : 'total') }}</strong>
            </summary>
            <div class="live-meal-items">
              @for (item of draft.items; track $index) {
                <div class="food-line">
                  <span><strong>{{ displayFoodName(item.foodName) }}</strong><small>{{ item.quantity | number }}
                    {{ draftUnit(activeDraftTurn?.question ?? '', item.measurementType, item.quantity) }}
                    @if (isOrderItem(draft, item.foodName)) { · Ask Past Me }</small></span>
                  <strong>{{ item.effectiveCarbs | number }} {{ draftUnit(activeDraftTurn?.question ?? '', 'Grams', item.effectiveCarbs) }}</strong>
                </div>
              }
              @for (issue of draft.issues; track $index) { <p class="draft-warning">{{ issue }}</p> }
              <button class="draft-cta" type="button" [disabled]="loading" (click)="useDraft(draft, !!draft.issues.length)">
                {{ draftLabel(activeDraftTurn?.question ?? '', draft.issues.length ? 'partial' : 'action') }}
              </button>
            </div>
          </details>
        }
      }
      <form (ngSubmit)="send()" [attr.aria-busy]="loading">
        <div class="composer">
        <textarea id="assistant-message" name="message" [(ngModel)]="message" [disabled]="loading"
          maxlength="1000" rows="1" aria-label="Ask about your meals" placeholder="Ask about your meals…" (keydown.enter)="handleEnter($event)"></textarea>
        <button type="submit" [disabled]="loading || !message.trim() || sessionExpired">{{ loading ? 'Searching…' : failedSend?.question === message.trim() ? 'Retry' : 'Send' }}</button>
        </div>
        @if (error && !failedSend) { <p role="alert">{{ error }}</p> }
        @if (sessionExpired) { <a routerLink="/login">Sign in</a> }
        @if (handoffError) { <p role="alert">{{ handoffError }}</p> }
      </form>
      </div>
    </section>
  `,
  styles: [`
    :host, .assistant { display: grid; min-height: 0; }
    .assistant { grid-template-columns: 250px minmax(0, 1fr); grid-template-rows: minmax(0, 1fr); gap: 1rem; }
    .conversation-sidebar, .chat { background: var(--cream); border: 1px solid var(--line); border-radius: 1rem; min-width: 0; min-height: 0; }
    .conversation-panel { display: flex; flex-direction: column; gap: .75rem; height: 100%; padding: .8rem; }
    .sidebar-heading { display: flex; align-items: center; justify-content: space-between; gap: .4rem; }
    .sidebar-heading button { padding: .5rem; white-space: nowrap; }
    .conversation-list { min-height: 0; flex: 1; overflow: auto; scrollbar-width: thin; }
    .conversation { display: grid; gap: .2rem; width: 100%; text-align: start; margin-block: .2rem; background: transparent; color: var(--coffee-dark); box-shadow: none; }
    .conversation-row { display: grid; grid-template-columns: minmax(0, 1fr) auto; align-items: start; gap: .15rem; }
    .chat-actions { margin-block: .2rem; }
    .chat-actions summary { cursor: pointer; list-style: none; padding: .5rem; border-radius: .5rem; color: var(--coffee-dark); }
    .chat-actions summary::-webkit-details-marker { display: none; }
    .chat-actions summary:hover, .chat-actions[open] summary { background: #f3e7da; }
    .chat-actions button { display: block; font-size: .8rem; padding: .4rem; }
    .conversation span { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; min-width: 0; }
    .conversation time { font-size: .8rem; font-weight: 400; }
    .conversation:hover { background: #f3e7da; }
    .conversation[aria-current] { background: var(--coffee-dark); color: white; }
    .drawer-toggle { display: none; }
    .chat { display: flex; flex-direction: column; }
    .chat-header, form { flex-shrink: 0; padding: .75rem 1rem; }
    .chat-header h1, .chat-header p { margin: 0; }
    .chat-header p { font-size: .85rem; margin-top: .25rem; }
    .transcript { flex: 1; min-height: 0; overflow: auto; scrollbar-width: thin; padding: .75rem 1rem; }
    .welcome { padding-block: 1.5rem; display: flex; flex-direction: column; justify-content: center; max-width: 38rem; margin-inline: auto; text-align: center; }
    .prompt-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(14rem, 100%), 1fr)); gap: .55rem; }
    .prompt-grid button { background: #fffaf4; border: 1px solid var(--line); color: var(--coffee-dark); box-shadow: none; }
    .prompt-grid button:hover { background: #f3e7da; }
    .turn { display: grid; gap: .75rem; margin-block-end: 1rem; }
    .turn h2 { justify-self: end; max-width: 85%; background: #ebdbcb; color: var(--coffee-dark); font-size: 1rem; font-weight: 500; white-space: pre-wrap; }
    .turn h2, .assistant-reply { margin: 0; padding: .8rem; border-radius: .85rem; overflow-wrap: anywhere; }
    .assistant-reply { justify-self: start; max-width: min(100%, 34rem); padding: 0; min-width: 0; }
    .assistant-reply.has-results { width: min(100%, 52rem); max-width: 100%; }
    .reply-text { display: flex; gap: .5rem; align-items: flex-start; }
    .reply-text p { margin: .15rem 0 .35rem; min-width: 0; }
    .assistant-avatar { flex-shrink: 0; }
    .history-heading { margin: 1rem 0 .3rem; font-size: .95rem; color: var(--muted); }
    .result-meta { font-size: .85rem; color: var(--muted); }
    .original-link { display: inline-flex; align-items: center; padding: .5rem .65rem; border: 1px solid var(--line); border-radius: .55rem; }
    .assistant :focus-visible { outline: 2px solid var(--coffee-dark); outline-offset: 2px; }
    .assistant-avatar { display: inline-grid; place-items: center; width: 1.6rem; height: 1.6rem; border-radius: 50%; color: var(--coffee-dark); background: #ebdbcb; }
    .meal { margin-block: .75rem; padding: .8rem; box-shadow: none; }
    .meal h3, .meal p { margin-block: .35rem; }
    .food-line { display: flex; justify-content: space-between; gap: .75rem; padding-block: .35rem; }
    .food-line small { display: block; color: var(--muted); }
    .food-line > strong { flex-shrink: 0; }
    .live-meal { flex-shrink: 0; border-top: 1px solid var(--line); background: #fffaf4; }
    .live-meal summary { cursor: pointer; min-height: 48px; padding: .7rem 1rem; }
    .live-meal summary strong { float: inline-end; margin-inline-start: .5rem; }
    .live-meal-items { max-height: min(30dvh, 16rem); overflow: auto; padding: 0 1rem .65rem; }
    .live-meal-items .food-line { overflow-wrap: anywhere; }
    .food-line + .food-line { border-top: 1px solid var(--line); }

    .draft-cta { width: 100%; min-height: 44px; margin-top: .5rem; }
    .source-choices { margin-block: .55rem; }
    .draft-warning { border-radius: .5rem; background: #f3e7da; padding: .55rem; }
    .acknowledgment { display: flex; align-items: center; gap: .5rem; margin-block: .55rem; }
    .acknowledgment input { width: auto; }
    .actions { display: flex; flex-wrap: wrap; align-items: center; gap: .75rem; }
    form { border-top: 1px solid var(--line); }
    .composer { display: flex; align-items: end; gap: .5rem; }
    .composer button { min-width: 4.5rem; }
    textarea { flex: 1; min-width: 0; max-height: 7rem; field-sizing: content; resize: none; }
    .pending-turn p { margin: 0; color: var(--muted); }
    @media (max-width: 1100px) { .assistant { grid-template-columns: 220px minmax(0, 1fr); } }
    @media (max-width: 760px) {
      .assistant { grid-template-columns: minmax(0, 1fr); grid-template-rows: auto minmax(0, 1fr); gap: .5rem; }
      .drawer-toggle { display: block; width: 100%; }
      .conversation-panel { display: none; height: auto; max-height: 35dvh; }
      .conversation-panel.open { display: flex; }
      .chat-header, form, .transcript { padding-inline: .65rem; }
    }
  `]
})
export class MealAssistantComponent {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  private readonly injector = inject(Injector);
  @ViewChild('transcript') private transcript?: ElementRef<HTMLElement>;
  conversations: AssistantConversation[] = [];
  readonly expandedTurns = new Set<AssistantTurn>();

  readonly prompts = ['What did I eat last time?', 'How many carbs do I usually count for borscht?', 'Find my previous chicken and potatoes meals.', 'I want pizza and salad.'];
  turns: AssistantTurn[] = [];
  conversationId: string | null = null;
  message = '';
  failedSend?: { question: string; conversationId: string | null; requestId: string };
  loading = false;
  sending = '';
  drawerOpen = false;
  sessionExpired = false;
  error = '';
  handoffError = '';

  ngOnInit() { this.refreshConversations(); }

  private refreshConversations() {
    this.api.getMealAssistantConversations().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: chats => this.conversations = chats,
      error: () => this.handoffError = 'Could not load conversation history.'
    });
  }

  private readonly chatScroll = new Map<string, number>();

  openChat(id: string) {
    if (this.loading) return;
    if (this.conversationId && this.transcript)
      this.chatScroll.set(this.conversationId, this.transcript.nativeElement.scrollTop);
    this.loading = true;
    this.error = '';
    this.api.getMealAssistantConversation(id).pipe(
      takeUntilDestroyed(this.destroyRef), finalize(() => this.loading = false)
    ).subscribe({
      next: turns => {
        this.newChat();
        this.conversationId = id;
        this.turns = turns;
        this.scrollToLatest(this.chatScroll.get(id));
      },
      error: response => this.error = getApiErrorMessage(response, 'Could not open the conversation.')
    });
  }

  private nearBottom() {
    const transcript = this.transcript?.nativeElement;
    return !transcript || transcript.scrollHeight - transcript.scrollTop - transcript.clientHeight < 100;
  }

  private scrollToLatest(position?: number) {
    afterNextRender(() => {
      const element = this.transcript?.nativeElement;
      if (element) element.scrollTop = position ?? element.scrollHeight;
    }, { injector: this.injector });
  }

  toggleResults(turn: AssistantTurn) {
    if (this.expandedTurns.has(turn)) this.expandedTurns.delete(turn);
    else this.expandedTurns.add(turn);
  }

  sendPrompt(prompt: string) {
    this.message = prompt;
    this.send();
  }

  handleEnter(event: Event) {
    if (!(event instanceof KeyboardEvent) || event.shiftKey || event.isComposing) return;
    event.preventDefault();
    this.send();
  }

  mealSummaryOpen = false;

  get activeDraftTurn() {
    for (let index = this.turns.length - 1; index >= 0; index--) {
      if (this.turns[index].answer.draft) return this.turns[index];
    }
    return undefined;
  }

  get activeDraft() {
    return this.activeDraftTurn?.answer.draft;
  }

  isOrderItem(draft: AssistantDraft, name: string) {
    return draft.sources?.some(source => source.kind === 'AskPastMe' && source.name === name) ?? false;
  }


  async useDraft(draft: AssistantDraft, allowPartial = false) {
    if (this.loading || !this.conversationId || draft !== this.activeDraft || !draft.items.length || (draft.issues.length && !allowPartial)) return;
    this.handoffError = '';
    this.loading = true;
    try {
      const items = await firstValueFrom(this.api.getMealAssistantCalculatorDraft(this.conversationId).pipe(takeUntilDestroyed(this.destroyRef)));
      const opened = await this.router.navigate(['/calculator'], { state: { assistantLocale: this.draftLocale(this.turns.find(turn => turn.answer.draft === draft)?.question ?? ''), assistantMealItems: items.map(item => ({
        foodItemId: item.foodItemId, quantity: item.quantity, measurementType: item.measurementType,
        foodNameSnapshot: assistantFoodName(item.foodName), carbsPer100gSnapshot: item.carbsPer100g,
        carbsPerUnitSnapshot: item.carbsPerUnit, carbOverride: item.carbOverride
      })) } });
      if (opened) return;
      this.handoffError = 'Could not open the calculator. Try again.';
    } catch (error) {
      this.handoffError = getApiErrorMessage(error, 'Could not prepare the calculator draft. Try again.');
    } finally {
      this.loading = false;
    }
  }

  send() {
    const question = this.message.trim();
    if (!question || this.loading || this.sessionExpired) return;
    const shouldScroll = this.nearBottom();
    this.sending = question;
    if (shouldScroll) this.scrollToLatest();
    this.loading = true;
    this.error = '';
    const pending = this.failedSend?.question === question && this.failedSend.conversationId === this.conversationId
      ? this.failedSend : { question, conversationId: this.conversationId, requestId: crypto.randomUUID() };
    this.api.sendMealAssistantMessage(pending.conversationId, question, pending.requestId).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => { this.loading = false; this.sending = ''; })
    ).subscribe({
      next: answer => {
        const followReply = this.nearBottom();
        this.failedSend = undefined;
        this.conversationId = answer.conversationId;
        if (answer.draft && !answer.draft.editFeedback) this.mealSummaryOpen = false;
        this.turns = [...this.turns, { question, answer }];
        this.message = '';
        if (followReply) this.scrollToLatest();
        this.refreshConversations();
      },
      error: response => {
        this.failedSend = pending;
        this.sessionExpired = isApiError(response) && response.status === 401;
        this.error = this.sendError(response);
      }
    });
  }

  private sendError(response: unknown): string {
    if (!isApiError(response)) return 'Could not send your message. Try again.';
    if (response.status === 401) return 'Your session expired. Copy your message and sign in again to continue.';
    if (response.status === 403) return 'Your account cannot access this conversation.';
    if (response.status === 429) return 'A search is already running. Try again shortly.';
    if (response.status === 504) return 'Meal Assistant took too long to respond. Retry your message.';
    if (response.code === 'assistant_configuration') return 'Meal Assistant needs to be configured on the server.';
    if (response.code === 'assistant_provider_auth') return 'Meal Assistant could not connect to its provider. Check the server credentials.';
    if (response.status >= 500) return 'Meal Assistant could not complete the request. Retry your message.';
    return response.message;
  }

  deleteChat(id: string) {
    if (this.loading || !window.confirm('Delete this conversation and its saved messages?')) return;
    this.loading = true;
    this.error = '';
    this.api.deleteMealAssistantConversation(id).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.loading = false)
    ).subscribe({
      next: () => { this.chatScroll.delete(id); if (this.conversationId === id) this.newChat(); this.refreshConversations(); },
      error: response => this.error = getApiErrorMessage(response, 'Could not delete this conversation. Try again.')
    });
  }

  newChat() {
    this.drawerOpen = false;
    this.conversationId = null;
    this.failedSend = undefined;
    this.turns = [];
    this.mealSummaryOpen = false;
    this.expandedTurns.clear();
    this.message = '';
    this.error = '';
    this.handoffError = '';
  }

  useOrder(order: AssistantOrder) {
    if (this.loading) return;
    this.loading = true;
    this.handoffError = '';
    this.api.createMealDraftFromDeliveryMeal(order.id).pipe(
      takeUntilDestroyed(this.destroyRef), finalize(() => this.loading = false)
    ).subscribe({
      next: draft => void this.router.navigate(['/calculator'], {
        state: { deliveryMeal: { ...draft, placeName: order.placeName, dishName: order.dishName,
          usualInsulinUnits: null, requireCurrentGlucose: true } }
      }),
      error: response => this.handoffError = getApiErrorMessage(response, 'Could not reuse this saved order.')
    });
  }

  useMeal(id: string) {
    this.handoffError = '';
    this.api.getMeal(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: meal => {
        const items = meal.items.map(item => {
          const basis = item.measurementType === 'Grams' ? item.carbsPer100gSnapshot : item.carbsPerUnitSnapshot;
          const valid = item.quantity > 0 && basis !== null && basis >= 0 &&
            (item.measurementType !== 'Piece' || Number.isInteger(item.quantity));
          return { ...item, foodItemId: valid ? item.foodItemId : '' };
        });
        void this.router.navigate(['/calculator'], { state: {
          assistantMealItems: items,
          assistantCarbAdjustment: meal.carbAdjustment,
          assistantOriginalMealId: meal.id
        } });
      },
      error: () => this.handoffError = 'Could not load the original meal. Try again.'
    });
  }

  private draftLocale(question: string): 'en' | 'ru' | 'uk' {
    if (/[іїєґ]|борщу|хліб|дві|прибери|додай/i.test(question)) return 'uk';
    return /[а-яё]/i.test(question) ? 'ru' : 'en';
  }

  draftUnit(question: string, unit: string, quantity: number) {
    const locale = this.draftLocale(question);
    if (unit === 'Grams') return locale === 'en' ? 'g' : 'г';
    const forms = locale === 'ru' ? (unit === 'Piece' ? ['кусочек', 'кусочка', 'кусочков'] : ['порция', 'порции', 'порций'])
      : locale === 'uk' ? (unit === 'Piece' ? ['шматочок', 'шматочки', 'шматочків'] : ['порція', 'порції', 'порцій'])
      : unit === 'Piece' ? ['piece', 'pieces', 'pieces'] : ['serving', 'servings', 'servings'];
    const plural = new Intl.PluralRules(locale).select(quantity);
    return forms[plural === 'one' ? 0 : plural === 'many' ? 2 : 1];
  }

  draftLabel(question: string, key: 'title' | 'subtotal' | 'total' | 'action' | 'partial' | 'sources') {
    if (/[іїєґІЇЄҐ]|борщу|хліб|дві|прибери|додай/i.test(question))
      return { title: 'Поточний прийом їжі', subtotal: 'Вуглеводи знайдених продуктів', total: 'Усього вуглеводів', action: 'Відкрити в калькуляторі', partial: 'Відкрити знайдені продукти', sources: 'Збережені варіанти' }[key];
    if (/[а-яё]/i.test(question))
      return { title: 'Текущий приём пищи', subtotal: 'Углеводы найденных продуктов', total: 'Всего углеводов', action: 'Открыть в калькуляторе', partial: 'Открыть найденные продукты', sources: 'Сохранённые варианты' }[key];
    return { title: 'Current meal', subtotal: 'Matched carbs', total: 'Total carbohydrates', action: 'Use in Calculator', partial: 'Open matched foods in Calculator', sources: 'Saved options' }[key];
  }

  responseText(turn: AssistantTurn, index: number): string {
    const locale = this.draftLocale(turn.question);
    const draft = turn.answer.draft;
    if (!draft || draft.editFeedback) return turn.answer.message;
    if (draft.items.length) {
      const total = new Intl.NumberFormat(locale).format(draft.totalCarbs);
      const label = this.draftLabel(turn.question, draft.issues.length ? 'subtotal' : 'total');
      const previous = this.turns.slice(0, index).reverse().find(entry => entry.answer.draft)?.answer.draft;
      let action = previous?.items.length
        ? { en: 'Meal updated', ru: 'Приём пищи обновлён', uk: 'Прийом їжі оновлено' }[locale]
        : { en: 'Meal planned', ru: 'Приём пищи собран', uk: 'Прийом їжі заплановано' }[locale];
      if (previous?.items.length && !draft.issues.length) {
        const same = (left: AssistantDraft['items'][number], right: AssistantDraft['items'][number]) =>
          left.foodItemId === right.foodItemId && left.foodName === right.foodName;
        const added = draft.items.filter(item => !previous.items.some(other => same(item, other)));
        const removed = previous.items.filter(item => !draft.items.some(other => same(item, other)));
        const changed = draft.items.filter(item => previous.items.some(other => same(item, other) && item.quantity !== other.quantity));
        if (added.length === 1 && !removed.length && !changed.length) {
          const name = assistantFoodName(added[0].foodName);
          action = { en: 'Added ', ru: 'Добавлено: ', uk: 'Додано: ' }[locale] + name;
        } else if (removed.length === 1 && !added.length && !changed.length) {
          const name = assistantFoodName(removed[0].foodName);
          action = { en: 'Removed ', ru: 'Убрано: ', uk: 'Вилучено: ' }[locale] + name;
        } else if (changed.length === 1 && !added.length && !removed.length) {
          const item = changed[0];
          action = assistantFoodName(item.foodName) + ' → ' + new Intl.NumberFormat(locale).format(item.quantity)
            + ' ' + this.draftUnit(turn.question, item.measurementType, item.quantity);
        }
      }
      const issues = draft.issues.length ? ' · ' + draft.issues.join(' ') : '';
      return `${action} · ${label}: ${total} ${this.draftUnit(turn.question, 'Grams', draft.totalCarbs)}${issues}`;
    }
    if (!draft.issues.length) return turn.answer.message;
    if (!draft.items.length) {
      const hasOptions = !!(turn.answer.knowledge?.foods.length || turn.answer.knowledge?.orders.length);
      return hasOptions ? {
        en: 'I found saved options below. Choose a whole order or tell me which food you mean.',
        ru: 'Ниже есть сохранённые варианты. Выбери целый заказ или уточни, какой продукт ты имеешь в виду.',
        uk: 'Нижче є збережені варіанти. Вибери ціле замовлення або уточни, який продукт маєш на увазі.'
      }[locale] : {
        en: 'I couldn’t build this meal from your saved foods. Try a different name or specify the portion.',
        ru: 'Не удалось собрать приём пищи из сохранённых продуктов. Попробуй другое название или уточни порцию.',
        uk: 'Не вдалося скласти прийом їжі зі збережених продуктів. Спробуй іншу назву або уточни порцію.'
      }[locale];
    }
    return turn.answer.message;
  }

  readonly displayFoodName = assistantFoodName;
  unitLabel(unit: string) {
    return unit === 'Grams' ? 'g' : unit === 'Piece' ? 'piece(s)' : 'serving(s)';
  }
}
