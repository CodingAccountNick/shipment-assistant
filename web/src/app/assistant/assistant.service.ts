import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';

interface AskResponse {
  answer: string;
}

@Injectable({ providedIn: 'root' })
export class AssistantService {
  private http = inject(HttpClient);

  ask(question: string) {
    return this.http.post<AskResponse>('/api/assistant/ask', { question });
  }
}