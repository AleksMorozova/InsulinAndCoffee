import { DatePipe, DecimalPipe } from '@angular/common';
import { afterNextRender, Component, DestroyRef, ElementRef, Injector, ViewChild, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize, firstValueFrom, map, switchMap, TimeoutError } from 'rxjs';
import { getApiErrorMessage, isApiError } from '../core/api-error';
import { ApiService } from '../core/api.service';
import { CarbProvenanceComponent } from '../shared/carb-provenance.component';
import { AssistantConversation, AssistantDraft, AssistantFoodSelection, AssistantSavedFood, AssistantOrder, AssistantOrderItem, AssistantTurn, assistantFoodName, AssistantResolution, AssistantUnresolvedItem, AssistantDraftEdit } from '../core/meal-assistant.models';
import { FoodMeasurementType } from '../core/models';

@Component({
  standalone: true,
  host: { class: 'meal-assistant-page' },
  imports: [FormsModule, RouterLink, DatePipe, DecimalPipe, CarbProvenanceComponent],
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
              <button type="button" [disabled]="loading" (click)="chatMenu.open = false; renameChat(chat)">Rename</button>
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
      <details class="privacy"><summary>Privacy · OpenAI</summary><p>Questions and recent chat context are sent to OpenAI. Saved historical meal records stay in the app.</p></details>
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
          <div class="assistant-reply" [class.has-results]="turn.answer.meals.length || turn.answer.knowledge?.foods?.length || turn.answer.knowledge?.orders?.length || turn.answer.knowledge?.items?.length">
          <div class="reply-text">
            <span class="assistant-avatar" aria-hidden="true">✦</span>
            <p>{{ responseText(turn, $index) }}</p>
          </div>
          @if (turn.answer.draft?.items?.length && turn.answer.knowledge; as alternatives) {
            <details class="source-choices"><summary>{{ draftLabel(turn.question, 'sources') }}</summary>
            @for (food of alternatives.foods; track food.id) {
              <p>{{ displayFoodName(food.name) }} · {{ unitLabel(food.unit) }}</p>
              <label>{{ ui('quantity') }} ({{ unitLabel(food.unit) }}) <input type="number" min="0.1" [step]="food.unit === 'Piece' ? 1 : 0.1" [ngModel]="candidateQuantities[food.id] ?? (food.unit === 'Grams' ? null : 1)" (ngModelChange)="candidateQuantities[food.id] = $event"></label>
              <button type="button" [disabled]="loading" (click)="selectFood(food, $event)">Choose this food</button>
            }
            @for (order of alternatives.orders; track order.id) { <p>{{ order.placeName }} · {{ order.dishName }}</p><button type="button" [disabled]="loading" (click)="selectOrder(order, $event)">Add entire order</button> }
            </details>
          }
          @for (food of (!turn.answer.draft?.items?.length ? turn.answer.knowledge?.foods : null) ?? []; track food.id) {
            <section class="card meal">
              <p class="result-meta">{{ ui('savedFood') }}</p>
              <h3>{{ displayFoodName(food.name) }}</h3>
              <app-carb-provenance [compact]="true" [provenance]="food.provenance" />
              @if ((food.unit === 'Grams' ? food.carbsPer100g : food.carbsPerUnit); as carbs) {
                <p>{{ carbs | number }} g carbs per {{ food.unit === 'Grams' ? '100 g' : unitLabel(food.unit) }}</p>
              } @else if ((food.unit === 'Grams' ? food.carbsPer100g : food.carbsPerUnit) === 0) {
                <p>0 g carbs per {{ food.unit === 'Grams' ? '100 g' : unitLabel(food.unit) }}</p>
              } @else { <p>Carbohydrate value unavailable.</p> }
              <label>{{ ui('quantity') }} ({{ unitLabel(food.unit) }}) <input type="number" min="0.1" [step]="food.unit === 'Piece' ? 1 : 0.1" [ngModel]="candidateQuantities[food.id] ?? (food.unit === 'Grams' ? null : 1)" (ngModelChange)="candidateQuantities[food.id] = $event"></label>
              <button type="button" [disabled]="loading" (click)="selectFood(food, $event)">Choose this food</button>
            </section>
          }
          @for (item of turn.answer.knowledge?.items ?? []; track item.id) {
            <section class="card meal">
              <p class="result-meta">Ask Past Me item · {{ item.placeName }} · {{ item.orderName }}</p>
              <h3>{{ item.name }}</h3>
              <p>{{ ui('saved') }}: {{ item.quantity }} {{ unitLabel(item.unit) }} · {{ item.effectiveCarbs | number }} {{ ui('carbs') }}</p>
              <app-carb-provenance [compact]="true" [provenance]="item.provenance" />
              <div class="result-actions"><label>{{ ui('quantity') }} ({{ unitLabel(item.unit) }}) <input type="number" min="0.01" [step]="item.unit === 'Piece' ? 1 : 0.01" [ngModel]="candidateQuantities[item.id] ?? item.quantity" (ngModelChange)="candidateQuantities[item.id] = $event"></label>
              <button type="button" [disabled]="loading" (click)="selectOrderItem(item, $event)">{{ ui('addItem') }}</button></div>
            </section>
          }
          @for (order of (!turn.answer.draft?.items?.length ? turn.answer.knowledge?.orders : null) ?? []; track order.id) {
            <section class="card meal">
              <p class="result-meta">Ask Past Me</p>
              <h3>{{ order.placeName }} · {{ order.dishName }}</h3>
              @if (order.portionDescription) { <p>{{ order.portionDescription }}</p> }
              <p><strong>{{ order.carbs | number }} g total carbs</strong></p>
              <button type="button" [disabled]="loading" (click)="selectOrder(order, $event)">{{ ui('addOrder') }}</button>
              <app-carb-provenance [compact]="true" [provenance]="order.provenance" />
              @if (order.lastUsedAt) { <p>Last reused {{ order.lastUsedAt | date:'mediumDate' }}</p> }
            </section>
          }
          @if ((turn.answer.draft || turn.answer.knowledge?.foods?.length || turn.answer.knowledge?.orders?.length || turn.answer.knowledge?.items?.length) && turn.answer.meals.length) {
            <h3 class="history-heading">From your history</h3>
          }
          @for (meal of turn.answer.meals.slice(0, expandedTurns.has(turn) ? undefined : 3); track meal.id) {
            <section class="card meal">
              <h3>{{ meal.mealType }} · {{ meal.mealTime | date:'mediumDate' }}</h3>
              @if (meal.carbsVerifiedAt) { <small>Meal carbs verified by you</small> }
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
        @if ((sending && !pendingItemId) || failedSend) {
          <article class="turn pending-turn">
            <h2>{{ sending || failedSend?.question }}</h2>
            @if (sending) { <p role="status">Searching your meal history…</p> }
            @else if (error) { <p role="alert">{{ error }}</p> }
            @if (failedSend && !loading && !draftConflict && message.trim() !== failedSend.question) {
              <button type="button" (click)="retryFailed()">Retry failed request</button>
            }
          </article>
        }
      @if (activeDraft; as draft) {
        @if (draft.items.length || draft.issues.length) {
          <details class="live-meal" [open]="mealSummaryOpen" (toggle)="mealSummaryOpen = mealDetails.open" #mealDetails>
            <summary>
              <span class="meal-heading"><strong>{{ draftLabel(activeDraftTurn?.question ?? '', 'title') }}</strong><small>{{ mealItemCount(draft.items.length + (draft.unresolved?.length ?? 0)) }}</small></span>
              <span class="meal-total"><strong>{{ mealTotal(draft) }}</strong>
                @if (draft.issues.length || draft.unresolved?.length) { <small>{{ draft.unresolved?.length ? mealItemCount(draft.unresolved?.length ?? 0) + ' · ' : '' }}{{ ui('clarify') }}</small> }</span>
            </summary>
            <div class="live-meal-items">
              @for (group of draft.duplicateGroups ?? []; track $index) {
                <details class="source-choices"><summary>{{ duplicateLabel('similar') }}: {{ duplicateName(group.draftItemIds) }}</summary>
                  <p class="result-meta">{{ duplicateLabel(group.canCombine ? 'safe' : 'different') }}</p>
                  <div class="result-actions">
                    <button type="button" class="subtle" [disabled]="loading || draftConflict" (click)="duplicateAction(group.draftItemIds, 'keepSeparate')">{{ duplicateLabel('separate') }}</button>
                    @if (group.canCombine) { <button type="button" class="subtle" [disabled]="loading || draftConflict" (click)="duplicateAction(group.draftItemIds, 'combine')">{{ duplicateLabel('combine') }}</button> }
                  </div>
                </details>
              }
              @for (item of draft.items; track draftItemKey($index, item)) {
                <div class="food-line" [attr.aria-busy]="loading && pendingItemId !== null && pendingItemId === item.draftItemId">
                  <div class="meal-food"><strong>{{ displayFoodName(item.foodName) }}</strong>
                    @if (editingItem !== $index) { <small>{{ item.quantity | number }} {{ draftUnit(activeDraftTurn?.question ?? '', item.measurementType, item.quantity) }}</small> }
                    @else {
                      <div class="quantity-editor" (keydown.escape)="$event.preventDefault(); cancelQuantity()">
                        <input #quantityInput type="number" min="0.01" [step]="item.measurementType === 'Piece' ? 1 : 0.01" [attr.aria-label]="ui('quantity') + ': ' + item.foodName" [attr.aria-invalid]="!validQuantity($index)"
                          [ngModel]="draftQuantities[$index]" (ngModelChange)="draftQuantities[$index] = $event" [disabled]="loading || draftConflict" (keydown.enter)="$event.preventDefault(); saveQuantity($index)">
                        <span>{{ draftUnit(activeDraftTurn?.question ?? '', item.measurementType, draftQuantities[$index] ?? item.quantity) }}</span>
                        <button type="button" class="subtle" [disabled]="loading || draftConflict || !validQuantity($index)" (click)="saveQuantity($index)">{{ mealControl('save') }}</button>
                        <button type="button" class="subtle" [disabled]="loading" (click)="cancelQuantity()">{{ mealControl('cancel') }}</button>
                      </div>
                    }
                    @if (unitWasInferred(draft, item.foodName)) { <small>{{ clarifyLabel('inferred') }}</small> }
                    <details class="meal-source"><summary>{{ mealSource(item) }}</summary><app-carb-provenance [provenance]="item.provenance" [overridden]="item.carbOverride !== null" /></details>
                  </div>
                  <div class="meal-item-actions"><strong class="item-carbs">{{ item.effectiveCarbs | number }} {{ ui('carbs') }}</strong>
                    @if (loading && pendingItemId !== null && pendingItemId === item.draftItemId) { <small role="status">{{ mealControl('saving') }}</small> }
                    @if (editingItem !== $index) { <button type="button" class="subtle meal-icon" [attr.data-item-edit]="$index" [attr.aria-label]="mealControl('edit') + ': ' + item.foodName" [title]="mealControl('edit')" [disabled]="loading || draftConflict" (click)="startQuantityEdit($index)">✎</button> }
                    <button type="button" class="subtle meal-icon" [attr.aria-label]="ui('remove') + ': ' + item.foodName" [title]="ui('remove')" [disabled]="loading || draftConflict" (click)="editCurrentItem($index, 'remove')">×</button>
                  </div>
                </div>
              }
              @if (!draft.unresolved?.length) { @for (issue of draft.issues; track $index) { <p class="draft-warning">{{ issue }}</p> } }
              @for (missing of draft.unresolved ?? []; track missing.id) {
                <section class="unresolved-item" role="group" [attr.aria-label]="clarifyLabel('unresolved') + ': ' + missing.originalText + ((draft.unresolved?.length ?? 0) > 1 ? ' · ' + duplicateLabel('request') + ' ' + ($index + 1) : '')">
                  <strong>{{ clarifyLabel('unresolved') }} · {{ missing.originalText }}</strong>
                  @if ((draft.unresolved?.length ?? 0) > 1) { <small>{{ duplicateLabel('request') }} {{ $index + 1 }}</small> }
                  <p>{{ missing.detail }}</p>
                  @if (missing.query.unitInferred) { <small>{{ clarifyLabel('inferred') }}</small> }
                  <div class="clarification-fields">
                    <label>{{ clarifyLabel('name') }}<input [ngModel]="missingNames[missing.id] ?? missing.query.name" (ngModelChange)="missingNames[missing.id] = $event" [attr.aria-label]="clarifyLabel('name') + ': ' + missing.originalText"></label>
                    <label>{{ ui('quantity') }}<input type="number" min="0.01" step="0.01" [ngModel]="missingQuantities[missing.id] ?? missing.query.quantity" (ngModelChange)="missingQuantities[missing.id] = $event" [attr.aria-label]="ui('quantity') + ': ' + missing.originalText"></label>
                    <label>{{ clarifyLabel('unit') }}<select [ngModel]="missingUnits[missing.id] ?? missing.query.unit ?? ''" (ngModelChange)="missingUnits[missing.id] = $event" [attr.aria-label]="clarifyLabel('unit') + ': ' + missing.originalText"><option value="">—</option><option value="Grams">{{ draftUnit(activeDraftTurn?.question ?? '', 'Grams', 1) }}</option><option value="Portion">{{ draftUnit(activeDraftTurn?.question ?? '', 'Portion', 2) }}</option><option value="Piece">{{ draftUnit(activeDraftTurn?.question ?? '', 'Piece', 2) }}</option></select></label>
                  </div>
                  <div class="clarification-actions"><button type="button" class="subtle" [disabled]="loading || draftConflict" (click)="resolveMissing(missing, 'search')">{{ clarifyLabel('search') }}</button>
                  <button type="button" class="subtle" [disabled]="loading || draftConflict" (click)="resolveMissing(missing, 'remove')">{{ ui('remove') }}</button></div>
                  @for (candidate of missing.candidates; track candidate.id) {
                    <button type="button" class="subtle clarification-choice" [disabled]="loading || draftConflict" (click)="resolveMissing(missing, 'select', candidate)">{{ candidate.name }} @if (candidate.context) { · {{ candidate.context }} }</button>
                  }
                  <details><summary>{{ clarifyLabel('manual') }}</summary><div class="clarification-actions"><label>{{ clarifyLabel('total') }}<input type="number" min="0" step="0.01" [ngModel]="missingCarbs[missing.id]" (ngModelChange)="missingCarbs[missing.id] = $event" [attr.aria-label]="clarifyLabel('total') + ': ' + missing.originalText"></label>
                    <button type="button" [disabled]="loading || draftConflict || missingCarbs[missing.id] === undefined || missingCarbs[missing.id] === null" (click)="resolveMissing(missing, 'manual')">{{ clarifyLabel('useManual') }}</button></div><small>{{ clarifyLabel('manualNote') }}</small></details>
                </section>
              }
              @if (draft.issues.length) { <p class="draft-status">{{ ui(draft.items.length ? 'partial' : 'clarify') }}</p> }
              @if (draft.items.length) { <button class="draft-cta" [class.partial-cta]="draft.issues.length" type="button" [disabled]="loading || draftConflict || sessionExpired" (click)="useDraft(draft, !!draft.issues.length)">
                {{ draftLabel(activeDraftTurn?.question ?? '', draft.issues.length ? 'partial' : 'action') }}
              </button> }
            </div>
          </details>
        }
      }
        </div>
      <form (ngSubmit)="send()" [attr.aria-busy]="loading">
        <div class="composer">
        <textarea id="assistant-message" name="message" [(ngModel)]="message"
          maxlength="1000" rows="1" aria-label="Ask about your meals" placeholder="Ask about your meals…" (keydown.enter)="handleEnter($event)"></textarea>
        <button type="submit" [disabled]="loading || draftConflict || !message.trim() || sessionExpired">{{ loading ? 'Searching…' : failedSend?.question === message.trim() ? 'Retry' : 'Send' }}</button>
        </div>
        @if (error && !failedSend) { <p role="alert">{{ error }}</p> }
        @if (sessionExpired) { <a routerLink="/login">Sign in</a> }
        @if (draftConflict && conversationId) { <button type="button" [disabled]="loading" (click)="refreshDraft()">Refresh latest draft</button> }
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
    .chat-header { display:flex; flex-wrap:wrap; align-items:center; justify-content:space-between; gap:.4rem; }
    .chat-header h1 { font-size:1.2rem; }
    .privacy { font-size:.75rem; color:var(--coffee-dark); }
    .privacy p { max-width:32rem; color:inherit; }
    .welcome p, .conversation-panel > p, .chat-header > p { color:var(--coffee-dark); }
    .assistant button:disabled { opacity:1; background:var(--line); color:var(--coffee-dark); }
    .transcript { flex: 1; min-height: 0; overflow: auto; scrollbar-width: thin; padding: .75rem 1rem; }
    .welcome { padding-block: 1.5rem; display: flex; flex-direction: column; justify-content: center; max-width: 38rem; margin-inline: auto; text-align: center; }
    .prompt-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(14rem, 100%), 1fr)); gap: .55rem; }
    .prompt-grid button { background: #fffaf4; border: 1px solid var(--line); color: var(--coffee-dark); box-shadow: none; }
    .prompt-grid button:hover { background: #f3e7da; }
    .turn { display: grid; gap: .5rem; margin-block-end: .75rem; }
    .turn h2 { justify-self: end; max-width: 85%; background: #ebdbcb; color: var(--coffee-dark); font-size: 1rem; font-weight: 500; white-space: pre-wrap; }
    .turn h2, .assistant-reply { margin: 0; padding: .8rem; border-radius: .85rem; overflow-wrap: anywhere; }
    .assistant-reply { justify-self: start; max-width: min(100%, 34rem); padding: 0; min-width: 0; }
    .assistant-reply.has-results { width: min(100%, 52rem); max-width: 100%; }
    .reply-text { display: flex; gap: .5rem; align-items: flex-start; }
    .reply-text p { margin: .15rem 0 .35rem; min-width: 0; color:var(--coffee-dark); }
    .assistant-avatar { flex-shrink: 0; }
    .history-heading { margin: 1rem 0 .3rem; font-size: .95rem; color: var(--coffee-dark); }
    .result-meta { font-size: .85rem; color: var(--muted); }
    .original-link { display: inline-flex; align-items: center; padding: .5rem .65rem; border: 1px solid var(--line); border-radius: .55rem; }
    .assistant :focus-visible { outline: 2px solid var(--coffee-dark); outline-offset: 2px; }
    .assistant-avatar { display: inline-grid; place-items: center; width: 1.6rem; height: 1.6rem; border-radius: 50%; color: var(--coffee-dark); background: #ebdbcb; }
    .meal { margin-block: .5rem; padding: .65rem; box-shadow: none; }
    .meal h3, .meal p { margin-block: .35rem; }
    .food-line { display:grid; grid-template-columns:minmax(0,1fr) auto; align-items:center; gap:.65rem; padding:.45rem 0; }
    .meal-food { min-width:0; } .meal-food > strong { font-size:.94rem; }
    .meal-item-actions { display:flex; align-items:center; gap:.25rem; }
    .item-carbs { font-size:.9rem; max-width:12rem; text-align:end; }
    .food-line button { padding:.3rem .5rem; min-height:36px; }
    .food-line .meal-icon { width:44px; min-height:44px; flex-shrink:0; font-size:1.15rem; }
    .quantity-editor { display:flex; align-items:center; flex-wrap:wrap; gap:.3rem; margin-block:.2rem; font-size:.8rem; }
    .quantity-editor input { width:5rem; min-width:0; padding:.35rem; }
    .meal-source { font-size:.74rem; color:var(--muted); }
    .meal-source summary { cursor:pointer; width:fit-content; } .meal-source app-carb-provenance { margin-top:.2rem; }
    .result-actions { display:flex; align-items:end; gap:.5rem; flex-wrap:wrap; }
    .result-actions label { max-width:150px; font-size:.8rem; }
    .food-line small { display: block; color: var(--muted); }
    .live-meal { max-width:52rem; margin-top:.6rem; border:1px solid var(--line); border-radius:.75rem; background:#fffaf4; }
    .live-meal > summary { display:flex; align-items:center; justify-content:space-between; flex-wrap:wrap; gap:.4rem 1rem; cursor:pointer; padding:.6rem .75rem; list-style:none; }
    .live-meal > summary::-webkit-details-marker { display:none; }
    .meal-heading,.meal-total { display:grid; gap:.1rem; } .meal-total { font-size:.95rem; }
    .meal-heading > strong::before { content:'⌄'; margin-inline-end:.4rem; }
    .live-meal > summary small { color:var(--muted); font-size:.76rem; font-weight:400; }
    .live-meal-items { padding:0 .75rem .6rem; }
    .live-meal-items .food-line { overflow-wrap: anywhere; }
    .food-line + .food-line { border-top: 1px solid var(--line); }

    .draft-cta { display:block; width:auto; min-height:44px; margin:.4rem 0 0 auto; padding:.5rem .8rem; }
    .source-choices { margin-block: .55rem; }
    .draft-warning { border-radius: .5rem; background: #f3e7da; padding: .55rem; }
    .unresolved-item { display:grid; min-width:0; overflow-wrap:anywhere; gap:.4rem; padding:.6rem; border:1px solid var(--line); border-radius:.5rem; margin:.4rem 0; font-size:.82rem; }
    .unresolved-item p { margin:0; } .clarification-fields { display:grid; grid-template-columns:2fr 1fr 1fr; gap:.4rem; }
    .clarification-fields label,.clarification-actions label { min-width:0; font-size:.75rem; }
    .clarification-fields input,.clarification-fields select { width:100%; min-width:0; padding:.4rem; }
    .clarification-actions { display:flex; flex-wrap:wrap; align-items:end; gap:.4rem; }
    .clarification-actions label { max-width:150px; } .clarification-choice { text-align:start; overflow-wrap:anywhere; }
    @media(max-width:540px) { .food-line { grid-template-columns:minmax(0,1fr); gap:.15rem; } .meal-item-actions { justify-content:flex-end; } .food-line button { min-height:44px; } .food-line .meal-icon { width:44px; } .draft-cta { width:100%; } .live-meal > summary { align-items:start; flex-direction:column; } }
    .acknowledgment { display: flex; align-items: center; gap: .5rem; margin-block: .55rem; }
    .acknowledgment input { width: auto; }
    .actions { display: flex; flex-wrap: wrap; align-items: center; gap: .75rem; }
    form { border-top: 1px solid var(--line); }
    .composer { display: flex; align-items: end; gap: .5rem; }
    .composer button { min-width: 4.5rem; }
    textarea { flex: 1; min-width: 0; max-height: 7rem; field-sizing: content; resize: none; }
    .pending-turn p { margin: 0; color: var(--coffee-dark); }
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
  private readonly hostElement = inject<ElementRef<HTMLElement>>(ElementRef);
  @ViewChild('quantityInput') quantityInput?: ElementRef<HTMLInputElement>;
  editingItem: number | null = null;
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
  failedSend?: { question: string; conversationId: string | null; requestId: string; selection?: AssistantFoodSelection; expectedDraftVersion: number; edit?: AssistantDraftEdit; resolution?: AssistantResolution };
  draftVersion = 0;
  draftConflict = false;
  private currentDraft?: AssistantDraft | null;
  candidateQuantities: Record<string, number | undefined> = {};
  private selectedFood?: AssistantFoodSelection;
  private selectedEdit?: AssistantDraftEdit;
  pendingItemId: string | null = null;
  private selectedResolution?: AssistantResolution;
  missingNames: Partial<Record<string, string>> = {};
  missingQuantities: Partial<Record<string, number>> = {};
  missingUnits: Partial<Record<string, FoodMeasurementType | ''>> = {};
  missingCarbs: Partial<Record<string, number>> = {};
  draftQuantities: Partial<Record<number, number>> = {};
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
        const failed = this.failedSend;
        this.newChat();
        this.conversationId = id;
        this.turns = turns;
        if (failed && (failed.conversationId === id || turns.some(turn => turn.answer.requestId === failed.requestId)))
          this.failedSend = failed;
        this.draftVersion = Math.max(0, ...turns.map(turn => turn.answer.draftVersion ?? 0));
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
    return this.currentDraft !== undefined ? this.currentDraft : this.activeDraftTurn?.answer.draft;
  }

  isOrderItem(draft: AssistantDraft, name: string) {
    return draft.sources?.some(source => ['AskPastMe', 'AskPastMeItem'].includes(source.kind) && source.name === name) ?? false;
  }
  draftItemKey(index: number, item: AssistantDraft['items'][number]) { return item.draftItemId ?? index; }

  editCurrentItem(index: number, action: AssistantDraftEdit['action']) {
    const item = this.activeDraft?.items[index];
    if (!item || this.loading || this.draftConflict || this.sessionExpired || !this.conversationId || (action === 'set' && !this.validQuantity(index))) return;
    this.selectedEdit = { itemIndex: index, action, ...(item.draftItemId ? { draftItemId: item.draftItemId } : {}), ...(action === 'set' ? { quantity: this.draftQuantities[index] ?? item.quantity } : {}) };
    this.message = (action === 'combine' ? this.duplicateLabel('combine') : action === 'keepSeparate' ? this.duplicateLabel('separate') : this.ui(action === 'remove' ? 'remove' : 'apply')) + ': ' + item.foodName;
    this.send();
  }

  duplicateName(ids: string[]) { return this.activeDraft?.items.filter(item => !!item.draftItemId && ids.includes(item.draftItemId)).map(item => this.displayFoodName(item.foodName)).filter((name, index, names) => names.indexOf(name) === index).join(' · '); }
  duplicateAction(ids: string[], action: 'combine' | 'keepSeparate') {
    const index = this.activeDraft?.items.findIndex(item => !!item.draftItemId && ids.includes(item.draftItemId)) ?? -1;
    if (index >= 0) this.editCurrentItem(index, action);
  }
  duplicateLabel(key: 'similar' | 'safe' | 'different' | 'separate' | 'combine' | 'request') {
    return { en: { similar: 'Same saved source', safe: 'Combine only if you want one row. The total will stay the same.', different: 'Different quantities or carb values. Keep these entries separate or edit an exact row.', separate: 'Keep separate', combine: 'Combine quantities', request: 'Requested item' },
      ru: { similar: 'Один сохранённый источник', safe: 'Можно объединить в одну строку. Сумма углеводов не изменится.', different: 'Количество или углеводы различаются. Оставь отдельно или измени нужную строку.', separate: 'Оставить отдельно', combine: 'Объединить количество', request: 'Позиция' },
      uk: { similar: 'Одне збережене джерело', safe: 'Можна об’єднати в один рядок. Сума вуглеводів не зміниться.', different: 'Кількість або вуглеводи різняться. Залиш окремо або зміни потрібний рядок.', separate: 'Залишити окремо', combine: 'Об’єднати кількість', request: 'Позиція' } }[this.draftLocale(this.activeDraftTurn?.question ?? '')][key];
  }

  startQuantityEdit(index: number) {
    const item = this.activeDraft?.items[index];
    if (!item || this.loading || this.draftConflict || this.sessionExpired) return;
    this.cancelQuantity();
    this.editingItem = index;
    this.draftQuantities[index] = item.quantity;
    afterNextRender(() => { if (this.editingItem === index) { this.quantityInput?.nativeElement.focus(); this.quantityInput?.nativeElement.select(); } }, { injector: this.injector });
  }

  validQuantity(index: number) {
    const quantity = this.draftQuantities[index];
    const item = this.activeDraft?.items[index];
    return !!item && typeof quantity === 'number' && Number.isFinite(quantity) && quantity >= 0.01 &&
      (item.measurementType !== 'Piece' || Number.isInteger(quantity));
  }

  saveQuantity(index: number) {
    if (this.editingItem !== index || !this.validQuantity(index) || this.loading || this.draftConflict) return;
    if (this.draftQuantities[index] === this.activeDraft?.items[index]?.quantity) this.cancelQuantity();
    else this.editCurrentItem(index, 'set');
  }

  cancelQuantity() {
    if (this.loading) return;
    const index = this.editingItem;
    if (index !== null) {
      delete this.draftQuantities[index];
      afterNextRender(() => this.hostElement.nativeElement.querySelector<HTMLButtonElement>(`[data-item-edit="${index}"]`)?.focus(), { injector: this.injector });
    }
    this.editingItem = null;
  }

  mealControl(key: 'edit' | 'save' | 'cancel' | 'saving') {
    return { en: { edit: 'Edit quantity', save: 'Save', cancel: 'Cancel', saving: 'Saving…' },
      ru: { edit: 'Изменить количество', save: 'Сохранить', cancel: 'Отмена', saving: 'Сохранение…' },
      uk: { edit: 'Змінити кількість', save: 'Зберегти', cancel: 'Скасувати', saving: 'Збереження…' } }[this.draftLocale(this.activeDraftTurn?.question ?? '')][key];
  }

  mealItemCount(count: number) {
    const locale = this.draftLocale(this.activeDraftTurn?.question ?? '');
    const plural = new Intl.PluralRules(locale).select(count);
    const labels = locale === 'ru' ? ['позиция', 'позиции', 'позиций'] : locale === 'uk' ? ['позиція', 'позиції', 'позицій'] : ['item', 'items', 'items'];
    return count + ' ' + labels[plural === 'one' ? 0 : plural === 'many' ? 2 : 1];
  }

  mealTotal(draft: AssistantDraft) {
    const locale = this.draftLocale(this.activeDraftTurn?.question ?? '');
    const partial = !!(draft.issues.length || draft.unresolved?.length);
    const label = { en: partial ? 'Known carbs' : 'Total', ru: partial ? 'Известные углеводы' : 'Всего', uk: partial ? 'Відомі вуглеводи' : 'Усього' }[locale];
    const unit = partial ? (locale === 'en' ? 'g' : 'г') : { en: 'g carbs', ru: 'г углеводов', uk: 'г вуглеводів' }[locale];
    return `${label}: ${new Intl.NumberFormat(locale).format(draft.totalCarbs)} ${unit}`;
  }

  mealSource(item: AssistantDraft['items'][number]) {
    const locale = this.draftLocale(this.activeDraftTurn?.question ?? '');
    const source = item.provenance?.referenceSource ?? 'Unknown';
    const history = item.provenance?.transformation.includes('HistoricalReuse') || (this.activeDraft && this.isOrderItem(this.activeDraft, item.foodName));
    const key = item.carbOverride !== null ? 'manual' : source === 'AiEstimate' ? 'ai' : source === 'Mixed' ? 'mixed'
      : source === 'Unknown' ? 'unknown' : history ? 'history' : item.foodItemId ? 'food' : 'manual';
    return { en: { manual: 'Entered manually', ai: 'AI estimate', mixed: 'Mixed sources', unknown: 'Source unknown', history: 'From history', food: 'Saved food' },
      ru: { manual: 'Введено вручную', ai: 'Оценка AI', mixed: 'Смешанные источники', unknown: 'Источник неизвестен', history: 'Из истории', food: 'Сохранённый продукт' },
      uk: { manual: 'Введено вручну', ai: 'Оцінка AI', mixed: 'Змішані джерела', unknown: 'Джерело невідоме', history: 'З історії', food: 'Збережений продукт' } }[locale][key];
  }

  unitWasInferred(draft: AssistantDraft, name: string) { return draft.sources?.some(source => source.name === name && source.unitInferred) ?? false; }

  resolveMissing(missing: AssistantUnresolvedItem, action: AssistantResolution['action'], candidate?: AssistantUnresolvedItem['candidates'][number]) {
    if (this.loading || this.draftConflict || this.sessionExpired || !this.conversationId) return;
    const unit = this.missingUnits[missing.id] ?? missing.query.unit ?? '';
    this.selectedResolution = { unresolvedId: missing.id, action,
      ...(action !== 'remove' ? { quantity: this.missingQuantities[missing.id] ?? missing.query.quantity ?? undefined,
        ...(unit ? { unit } : {}), searchName: this.missingNames[missing.id] ?? missing.query.name } : {}),
      ...(action === 'manual' ? { carbs: this.missingCarbs[missing.id] } : {}),
      ...(candidate ? { sourceId: candidate.id, sourceKind: candidate.kind } : {}) };
    this.message = this.clarifyLabel(action === 'remove' ? 'discard' : 'resolve') + ': ' + missing.originalText;
    this.send();
  }


  async useDraft(draft: AssistantDraft, allowPartial = false) {
    if (this.loading || this.draftConflict || this.sessionExpired || !this.conversationId || draft !== this.activeDraft || !draft.items.length || (draft.issues.length && !allowPartial)) return;
    this.handoffError = '';
    this.loading = true;
    try {
      const items = await firstValueFrom(this.api.getMealAssistantCalculatorDraft(this.conversationId, this.draftVersion).pipe(takeUntilDestroyed(this.destroyRef)));
      const opened = await this.router.navigate(['/calculator'], { state: { assistantLocale: this.draftLocale(this.turns.find(turn => turn.answer.draft === draft)?.question ?? ''), assistantMealItems: items.map(item => ({
        foodItemId: item.foodItemId, quantity: item.quantity, measurementType: item.measurementType,
        foodNameSnapshot: assistantFoodName(item.foodName), carbsPer100gSnapshot: item.carbsPer100g,
        carbsPerUnitSnapshot: item.carbsPerUnit, carbOverride: item.carbOverride,
        ...(item.sourceMealItemId ? { sourceMealItemId: item.sourceMealItemId } : {}),
        ...(item.sourceDeliveryMealId ? { sourceDeliveryMealId: item.sourceDeliveryMealId } : {}),
        ...(item.sourceDeliveryMealItemId ? { sourceDeliveryMealItemId: item.sourceDeliveryMealItemId } : {})
      })) } });
      if (opened) return;
      this.handoffError = 'Could not open the calculator. Try again.';
    } catch (error) {
      if (isApiError(error) && error.status === 409) {
        this.draftConflict = true;
        this.handoffError = 'The meal changed. Review the refreshed Current Meal, then choose Use in Calculator again.';
        this.loadAuthoritativeDraft();
      } else this.handoffError = getApiErrorMessage(error, 'Could not prepare the calculator draft. Try again.');
    } finally {
      this.loading = false;
    }
  }

  selectFood(food: AssistantSavedFood, event?: MouseEvent) {
    if (this.loading || this.sessionExpired || this.draftConflict || (event?.detail ?? 0) > 1) return;
    this.selectedFood = { foodId: food.id, quantity: this.candidateQuantities[food.id], unit: food.unit };
    this.message = 'Use ' + this.displayFoodName(food.name);
    this.send();
  }

  selectOrderItem(item: AssistantOrderItem, event?: MouseEvent) {
    if (this.loading || this.sessionExpired || this.draftConflict || (event?.detail ?? 0) > 1) return;
    this.selectedFood = { foodId: item.id, quantity: this.candidateQuantities[item.id] ?? item.quantity, unit: item.unit, kind: 'DeliveryMealItem' };
    this.message = `Add ${item.name} from ${item.placeName}`;
    this.send();
  }
  selectOrder(order: AssistantOrder, event?: MouseEvent) {
    if (this.loading || this.sessionExpired || this.draftConflict || (event?.detail ?? 0) > 1) return;
    this.selectedFood = { foodId: order.id, kind: 'DeliveryMeal' };
    this.message = `Add ${order.dishName} from ${order.placeName}`;
    this.send();
  }

  send() {
    const question = this.message.trim();
    if (!question || this.loading || this.sessionExpired || this.draftConflict) return;
    const shouldScroll = this.nearBottom();
    this.sending = question;
    if (shouldScroll) this.scrollToLatest();
    this.loading = true;
    this.error = '';
    const pending = this.failedSend?.question === question
      && (this.failedSend.conversationId === this.conversationId || this.turns.some(turn => turn.answer.requestId === this.failedSend?.requestId))
      && (!this.selectedFood || JSON.stringify(this.selectedFood) === JSON.stringify(this.failedSend.selection))
      && (!this.selectedEdit || JSON.stringify(this.selectedEdit) === JSON.stringify(this.failedSend.edit))
      && (!this.selectedResolution || JSON.stringify(this.selectedResolution) === JSON.stringify(this.failedSend.resolution))
      ? this.failedSend : { question, conversationId: this.conversationId, requestId: crypto.randomUUID(), selection: this.selectedFood, expectedDraftVersion: this.draftVersion, edit: this.selectedEdit, resolution: this.selectedResolution };
    const request = pending.resolution ? this.api.sendMealAssistantMessage(pending.conversationId, question, pending.requestId, undefined, pending.expectedDraftVersion, undefined, pending.resolution)
      : pending.edit ? this.api.sendMealAssistantMessage(pending.conversationId, question, pending.requestId, pending.selection, pending.expectedDraftVersion, pending.edit)
      : this.api.sendMealAssistantMessage(pending.conversationId, question, pending.requestId, pending.selection, pending.expectedDraftVersion);
    this.pendingItemId = pending.edit?.draftItemId ?? null;
    this.selectedFood = undefined;
    this.selectedEdit = undefined;
    this.selectedResolution = undefined;
    request.pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => { this.loading = false; this.sending = ''; this.pendingItemId = null; })
    ).subscribe({
      next: answer => {
        const followReply = this.nearBottom();
        const stale = (answer.draftVersion ?? 0) < this.draftVersion;
        if ((answer.replayed || stale) && this.currentDraft === undefined) this.currentDraft = this.activeDraft ?? null;
        this.failedSend = undefined;
        this.conversationId = answer.conversationId;
        if (answer.draft && !this.activeDraft) this.mealSummaryOpen = true;
        const responseId = answer.requestId ?? pending.requestId;
        if (!this.turns.some(turn => turn.answer.requestId === responseId))
          this.turns = [...this.turns, { question, answer: { ...answer, requestId: responseId } }];
        if (!answer.replayed && (answer.draftVersion ?? 0) >= this.draftVersion) {
          if (answer.draft) { this.currentDraft = answer.draft; this.draftQuantities = {}; this.editingItem = null; }
          else if ((answer.draftVersion ?? 0) !== this.draftVersion) {
            this.draftConflict = true;
            this.error = 'Your draft changed elsewhere. Refresh the latest draft before editing.';
          }
          this.draftVersion = answer.draftVersion ?? this.draftVersion;
        }
        if (answer.replayed || stale) {
          this.draftConflict = true;
          this.error = stale ? 'A newer meal exists. Loading the latest draft before editing.' : 'This request already completed. Refresh the latest draft before editing.';
          this.loadAuthoritativeDraft();
        }
        if (this.message.trim() === question) this.message = '';
        if (followReply) this.scrollToLatest();
        this.refreshConversations();
      },
      error: response => {
        this.failedSend = pending;
        this.sessionExpired = isApiError(response) && response.status === 401;
        this.draftConflict = isApiError(response) && response.status === 409;
        this.error = this.localizedSendError(response, question);
        if (this.draftConflict) this.loadAuthoritativeDraft();
      }
    });
  }

  private loadAuthoritativeDraft() {
    const id = this.conversationId;
    if (!id) return;
    this.api.getMealAssistantDraftState(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: state => {
        if (this.conversationId !== id || state.draftVersion < this.draftVersion) return;
        this.currentDraft = state.draft;
        this.draftVersion = state.draftVersion;
        this.draftQuantities = {}; this.editingItem = null;
        this.failedSend = undefined;
        this.draftConflict = false;
        this.error = { en: 'Latest meal loaded. Your message is kept. Review the meal before sending another change.',
          ru: 'Загружен актуальный приём пищи. Сообщение сохранено. Проверь состав перед новой отправкой.',
          uk: 'Завантажено актуальний прийом їжі. Повідомлення збережено. Перевір склад перед новим надсиланням.' }[this.draftLocale(this.message || this.activeDraftTurn?.question || '')];
      },
      error: () => {
        if (this.conversationId === id) this.error += ' Could not load the latest meal. Use Refresh latest draft to try again.';
      }
    });
  }

  private sendError(response: unknown): string {
    if (response instanceof TimeoutError) return 'The request timed out. Retry safely using the same request ID.';
    if (!isApiError(response)) return 'Could not send your message. Try again.';
    if (response.status === 401) return 'Your session expired. Copy your message and sign in again to continue.';
    if (response.status === 403) return 'Your account cannot access this conversation.';
    if (response.status === 429) return 'A search is already running. Try again shortly.';
    if (response.status === 409) return this.conversationId
      ? 'The draft or request changed. Your message is kept. Refresh the latest draft, review it, then send again.'
      : 'This request already has different content. Your message is kept. Open the latest conversation from history before editing.';
    if (response.status === 504) return 'Meal Assistant took too long to respond. Retry your message.';
    if (response.code === 'assistant_configuration') return 'Meal Assistant needs to be configured on the server.';
    if (response.code === 'assistant_provider_auth') return 'Meal Assistant could not connect to its provider. Check the server credentials.';
    if (response.status >= 500) return 'Meal Assistant could not complete the request. Retry your message.';
    return response.message;
  }

  private localizedSendError(response: unknown, question: string) {
    const locale = this.draftLocale(question);
    if (locale === 'en') return this.sendError(response);
    const status = isApiError(response) ? response.status : 0;
    const kind = response instanceof TimeoutError || status === 504 ? 'timeout' : status === 401 ? 'auth' : status === 403 ? 'forbidden'
      : status === 409 ? 'conflict' : status === 429 ? 'busy' : status === 400 ? 'validation' : 'failed';
    return { ru: { timeout: 'Ассистент не успел ответить. Повтори запрос: сообщение сохранено.', auth: 'Сессия истекла. Сообщение сохранено. Войди снова.',
      forbidden: 'У аккаунта нет доступа к этому разговору.', conflict: 'Черновик изменился. Обнови его перед повторной отправкой. Сообщение сохранено.',
      busy: 'Поиск уже выполняется. Попробуй немного позже.', validation: 'Проверь продукт и количество. Сообщение сохранено.', failed: 'Не удалось выполнить запрос. Сообщение сохранено, можно повторить.' },
      uk: { timeout: 'Асистент не встиг відповісти. Повтори запит: повідомлення збережено.', auth: 'Сесія закінчилась. Повідомлення збережено. Увійди знову.',
        forbidden: 'Обліковий запис не має доступу до цієї розмови.', conflict: 'Чернетка змінилась. Онови її перед повторним надсиланням. Повідомлення збережено.',
        busy: 'Пошук уже виконується. Спробуй трохи пізніше.', validation: 'Перевір продукт і кількість. Повідомлення збережено.', failed: 'Не вдалося виконати запит. Повідомлення збережено, можна повторити.' } }[locale][kind];
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

  renameChat(chat: AssistantConversation) {
    if (this.loading) return;
    const title = window.prompt('Conversation title (1–100 characters)', chat.title)?.trim();
    if (!title) return;
    if (title.length > 100) { this.error = 'Conversation titles support up to 100 characters.'; return; }
    this.loading = true;
    this.api.renameMealAssistantConversation(chat.id, title).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.loading = false)).subscribe({
      next: () => this.refreshConversations(), error: error => this.error = getApiErrorMessage(error, 'Could not rename this conversation.') });
  }

  newChat() {
    this.drawerOpen = false;
    this.conversationId = null;
    this.failedSend = undefined;
    this.draftVersion = 0;
    this.draftConflict = false;
    this.currentDraft = undefined;
    this.selectedFood = undefined;
    this.selectedEdit = undefined;
    this.draftQuantities = {};
    this.editingItem = null;
    this.selectedResolution = undefined;
    this.missingNames = {}; this.missingQuantities = {}; this.missingUnits = {}; this.missingCarbs = {};
    this.turns = [];
    this.mealSummaryOpen = false;
    this.expandedTurns.clear();
    this.error = '';
    this.handoffError = '';
  }

  retryFailed() {
    const failed = this.failedSend;
    if (!failed || this.loading || this.draftConflict) return;
    const unsent = this.message;
    this.message = failed.question;
    this.send();
    if (unsent !== failed.question) this.message = unsent;
  }

  refreshDraft() {
    const id = this.conversationId;
    if (!id || this.loading) return;
    this.loading = true;
    this.api.getMealAssistantConversation(id).pipe(
      switchMap(turns => this.api.getMealAssistantDraftState(id).pipe(map(state => ({ turns, state })))),
      takeUntilDestroyed(this.destroyRef), finalize(() => this.loading = false)
    ).subscribe({
      next: ({ turns, state }) => {
        if (this.failedSend?.selection && this.message.trim() === this.failedSend.question)
          this.selectedFood = this.failedSend.selection;
        this.turns = turns;
        this.currentDraft = state.draft;
        this.editingItem = null;
        this.draftQuantities = {};
        this.draftVersion = state.draftVersion;
        this.failedSend = undefined;
        this.draftConflict = false;
        this.error = 'Latest draft loaded. Review it before sending your edit again.';
      },
      error: response => this.error = getApiErrorMessage(response, 'Could not refresh the latest draft. Your message is kept.')
    });
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
          return { ...item, foodItemId: valid ? item.foodItemId : '', sourceMealItemId: item.id };
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
    if (!/[a-zа-яёіїєґ]/i.test(question)) {
      const previous = [...this.turns].reverse().find(turn => /[a-zа-яёіїєґ]/i.test(turn.question));
      if (previous) return this.draftLocale(previous.question);
    }
    if (/[іїєґ]|борщу|хліб|дві|прибери|додай|знайди|скільки|вуглевод|замовлення/i.test(question)) return 'uk';
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
    if (this.draftLocale(question) === 'uk')
      return { title: 'Поточний прийом їжі', subtotal: 'Вуглеводи знайдених продуктів', total: 'Усього вуглеводів', action: 'Відкрити в калькуляторі', partial: 'Відкрити знайдені продукти', sources: 'Збережені варіанти' }[key];
    if (this.draftLocale(question) === 'ru')
      return { title: 'Текущий приём пищи', subtotal: 'Углеводы найденных продуктов', total: 'Всего углеводов', action: 'Открыть в калькуляторе', partial: 'Открыть найденные продукты', sources: 'Сохранённые варианты' }[key];
    return { title: 'Current meal', subtotal: 'Matched carbs', total: 'Total carbohydrates', action: 'Use in Calculator', partial: 'Open matched foods in Calculator', sources: 'Saved options' }[key];
  }

  responseText(turn: AssistantTurn, index: number): string {
    if (turn.answer.operationSummary) return turn.answer.operationSummary;
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
      if (draft.issues.length) action = { en: 'Meal partially assembled', ru: 'Приём пищи собран частично', uk: 'Прийом їжі зібрано частково' }[locale];
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
    if (draft.unresolved?.length) return {
      en: 'Choose a saved source or clarify the missing items below. Your current meal is kept.',
      ru: 'Выбери сохранённую позицию или уточни недостающие продукты ниже. Текущий приём пищи сохранён.',
      uk: 'Обери збережену позицію або уточни відсутні продукти нижче. Поточний прийом їжі збережено.'
    }[locale];
    if (!draft.items.length) {
      const hasOptions = !!(turn.answer.knowledge?.foods.length || turn.answer.knowledge?.orders.length || turn.answer.knowledge?.items?.length);
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
  clarifyLabel(key: 'unresolved' | 'inferred' | 'name' | 'unit' | 'search' | 'manual' | 'total' | 'useManual' | 'manualNote' | 'resolve' | 'discard') {
    const locale = this.draftLocale(this.message || this.turns[this.turns.length - 1]?.question || '');
    return { en: { unresolved: 'Needs clarification', inferred: 'Unit inferred — check it', name: 'Search name', unit: 'Unit', search: 'Search saved foods again', manual: 'Enter carbs manually', total: 'Total carbs (g)', useManual: 'Use my carb total', manualNote: 'User-provided total for this item only. No Food will be created.', resolve: 'Resolve item', discard: 'Remove unresolved item' },
      ru: { unresolved: 'Нужно уточнение', inferred: 'Единица определена автоматически — проверь', name: 'Название для поиска', unit: 'Единица', search: 'Поискать в сохранённых', manual: 'Ввести углеводы вручную', total: 'Всего углеводов (г)', useManual: 'Использовать мои углеводы', manualNote: 'Твоё значение только для этой позиции. Продукт в Foods не создаётся.', resolve: 'Уточнить позицию', discard: 'Убрать неуточнённую позицию' },
      uk: { unresolved: 'Потрібне уточнення', inferred: 'Одиницю визначено автоматично — перевір', name: 'Назва для пошуку', unit: 'Одиниця', search: 'Пошукати серед збережених', manual: 'Ввести вуглеводи вручну', total: 'Усього вуглеводів (г)', useManual: 'Використати мої вуглеводи', manualNote: 'Твоє значення лише для цієї позиції. Продукт у Foods не створюється.', resolve: 'Уточнити позицію', discard: 'Вилучити неуточнену позицію' } }[locale][key];
  }
  ui(key: 'quantity' | 'addItem' | 'addOrder' | 'apply' | 'remove' | 'partial' | 'clarify' | 'savedFood' | 'saved' | 'carbs') {
    const language = this.draftLocale(this.message || this.turns[this.turns.length - 1]?.question || '');
    return { en: { quantity: 'Quantity', addItem: 'Add item', addOrder: 'Add entire order', apply: 'Apply quantity', remove: 'Remove item', partial: 'Partially assembled', clarify: 'Needs clarification', savedFood: 'Saved food', saved: 'Saved', carbs: 'g carbs' },
      ru: { quantity: 'Количество', addItem: 'Добавить позицию', addOrder: 'Добавить весь заказ', apply: 'Изменить количество', remove: 'Убрать продукт', partial: 'Собрано частично', clarify: 'Нужно уточнение', savedFood: 'Сохранённый продукт', saved: 'Сохранено', carbs: 'г углеводов' },
      uk: { quantity: 'Кількість', addItem: 'Додати позицію', addOrder: 'Додати все замовлення', apply: 'Змінити кількість', remove: 'Вилучити продукт', partial: 'Зібрано частково', clarify: 'Потрібне уточнення', savedFood: 'Збережений продукт', saved: 'Збережено', carbs: 'г вуглеводів' } }[language][key];
  }
  unitLabel(unit: string) {
    const question = this.message || this.turns[this.turns.length - 1]?.question || '';
    if (this.draftLocale(question) !== 'en') return this.draftUnit(question, unit, 2);
    return unit === 'Grams' ? 'g' : unit === 'Piece' ? 'piece(s)' : 'serving(s)';
  }
}
