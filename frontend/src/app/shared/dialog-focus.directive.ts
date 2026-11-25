import { afterNextRender, Directive, ElementRef, HostListener, OnDestroy, output } from '@angular/core';

/** Keyboard interaction for conditionally rendered modal dialogs. */
@Directive({ selector: '[appDialogFocus]', standalone: true })
export class DialogFocusDirective implements OnDestroy {
  readonly dialogEscape = output<void>();
  private readonly opener: HTMLElement | null;

  constructor(private readonly element: ElementRef<HTMLElement>) {
    const active = document.activeElement;
    this.opener = active instanceof HTMLElement
      ? active.closest('details')?.querySelector('summary') ?? active : null;
    afterNextRender(() => this.focusFirst());
  }

  private controls(): HTMLElement[] {
    return Array.from(this.element.nativeElement.querySelectorAll<HTMLElement>(
      'button:not([disabled]),input:not([disabled]),select:not([disabled]),textarea:not([disabled]),a[href],summary,[tabindex="0"]'
    )).filter(control => control.getClientRects().length > 0);
  }

  private focusFirst(): void {
    (this.element.nativeElement.querySelector<HTMLElement>('input:not([disabled])')
      ?? this.controls()[0] ?? this.element.nativeElement).focus();
  }

  @HostListener('document:focusin', ['$event'])
  containFocus(event: FocusEvent): void {
    if (event.target instanceof Node && !this.element.nativeElement.contains(event.target)) this.focusFirst();
  }

  @HostListener('keydown', ['$event'])
  onKey(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault(); event.stopPropagation(); this.dialogEscape.emit();
    } else if (event.key === 'Tab') {
      const controls = this.controls();
      const first = controls[0]; const last = controls[controls.length - 1];
      if (!first || event.shiftKey && document.activeElement === first) {
        event.preventDefault(); (last ?? this.element.nativeElement).focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault(); first.focus();
      }
    }
  }

  ngOnDestroy(): void {
    // Defer until Angular has removed the dialog and its focus listener.
    queueMicrotask(() => { if (this.opener?.isConnected) this.opener.focus(); });
  }
}
