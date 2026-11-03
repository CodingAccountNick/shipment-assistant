import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AssistantService } from './assistant.service';

interface ChatMessage {
  from: 'you' | 'assistant';
  text: string;
  isError?: boolean;
}

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [FormsModule],
  template: `
    <h2>Ask the assistant</h2>

    <div class="messages">
      @for (m of messages(); track $index) {
        <div class="message" [class.you]="m.from === 'you'" [class.error]="m.isError">
          <strong>{{ m.from === 'you' ? 'You' : 'Assistant' }}:</strong> {{ m.text }}
        </div>
      } @empty {
        <p class="hint">Try one of these:</p>
        @for (q of suggestions; track q) {
          <button type="button" class="suggestion" (click)="question.set(q)">{{ q }}</button>
        }
      }

      @if (sending()) {
        <p class="hint">Assistant is thinking…</p>
      }
    </div>

    <form (ngSubmit)="send()">
      <input
        name="question"
        [(ngModel)]="question"
        [disabled]="sending()"
        placeholder="Ask about shipments, deliveries or transport…" />
      <button type="submit" [disabled]="!canSend()">Send</button>
    </form>
  `,
  styles: [`
    .messages { border: 1px solid #ddd; border-radius: 8px; padding: 12px; min-height: 120px; margin-bottom: 8px; }
    .message { margin: 6px 0; padding: 8px 12px; border-radius: 8px; background: #f1f3f5; }
    .message.you { background: #e7f1ff; }
    .message.error { background: #f8d7da; }
    .hint { color: #777; }
    .suggestion { margin: 4px 4px 0 0; }
    form { display: flex; gap: 8px; }
    input { flex: 1; padding: 8px; }
  `]
})
export class ChatComponent {
  private assistant = inject(AssistantService);

  suggestions = [
    "What does 'out for delivery' mean?",
    'Why might a shipment be delayed?',
    'What is a tracking number?'
  ];

  messages = signal<ChatMessage[]>([]);
  question = signal('');
  sending = signal(false);

  // Send is only possible with some text, and not while waiting for an answer
  canSend = computed(() => this.question().trim().length > 0 && !this.sending());

  send() {
    if (!this.canSend()) return;

    const text = this.question().trim();
    this.messages.update(list => [...list, { from: 'you', text }]);
    this.question.set('');
    this.sending.set(true);

    this.assistant.ask(text).subscribe({
      next: res => {
        this.messages.update(list => [...list, { from: 'assistant', text: res.answer }]);
        this.sending.set(false);
      },
      error: () => {
        this.messages.update(list => [...list, {
          from: 'assistant',
          text: 'Sorry, the assistant is not available right now.',
          isError: true
        }]);
        this.sending.set(false);
      }
    });
  }
}