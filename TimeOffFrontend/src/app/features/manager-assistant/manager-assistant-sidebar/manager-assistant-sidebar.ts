import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';

import { ApiError } from '../../../core/api/api-error.model';
import { DownloadResponse } from '../../../core/api/download-response';
import { ManagerAssistantApiService } from '../../../core/api/manager-assistant-api.service';
import {
  ManagerAssistantHistoryMessage,
  ManagerAssistantUiMessage,
  TeamTimeLogExportPart,
} from '../../../shared/models/manager-assistant.model';
import { SecondsDurationPipe } from '../../../shared/pipes/seconds-duration-pipe';

@Component({
  selector: 'app-manager-assistant-sidebar',
  imports: [SecondsDurationPipe],
  templateUrl: './manager-assistant-sidebar.html',
  styleUrl: './manager-assistant-sidebar.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[class.assistant-hidden]': '!available()',
  },
})
export class ManagerAssistantSidebar implements OnInit {
  private readonly api = inject(ManagerAssistantApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly activeObjectUrls = new Set<string>();

  protected readonly available = signal(false);
  protected readonly messages = signal<readonly ManagerAssistantUiMessage[]>([]);
  protected readonly draft = signal('');
  protected readonly sending = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly downloadingUrl = signal<string | null>(null);
  protected readonly maxMessageLength = signal(1_000);

  constructor() {
    this.destroyRef.onDestroy(() => {
      for (const url of this.activeObjectUrls) {
        URL.revokeObjectURL(url);
      }
      this.activeObjectUrls.clear();
    });
  }

  ngOnInit(): void {
    this.api
      .getCapabilities()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (capabilities) => {
          this.available.set(capabilities.enabled);
          this.maxMessageLength.set(capabilities.maxMessageLength);
          if (capabilities.enabled) {
            this.messages.set([
              {
                id: 'welcome',
                role: 'assistant',
                text: 'Ask me about your current team\'s hours, status, or Excel exports.',
                parts: [],
              },
            ]);
          }
        },
        error: () => this.available.set(false),
      });
  }

  protected updateDraft(event: Event): void {
    this.draft.set((event.target as HTMLTextAreaElement).value);
  }

  protected useExample(prompt: string): void {
    this.draft.set(prompt);
  }

  protected send(): void {
    const prompt = this.draft().trim();
    if (!prompt || this.sending() || prompt.length > this.maxMessageLength()) {
      return;
    }

    const history = this.history();
    const userMessage: ManagerAssistantUiMessage = {
      id: `user-${Date.now()}`,
      role: 'user',
      text: prompt,
      parts: [],
    };
    this.messages.update((messages) => [...messages, userMessage]);
    this.draft.set('');
    this.error.set(null);
    this.sending.set(true);

    this.api
      .sendMessage({ message: prompt, history })
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.sending.set(false)),
      )
      .subscribe({
        next: (response) => {
          this.messages.update((messages) => [
            ...messages,
            {
              id: response.messageId,
              role: 'assistant',
              text: response.answer,
              parts: response.parts,
            },
          ]);
        },
        error: (error: unknown) => {
          this.error.set(this.errorMessage(error));
        },
      });
  }

  protected chooseEmployee(employeeNumber: string): void {
    this.draft.set(`Show worked time for ${employeeNumber} for the same period.`);
  }

  protected download(part: TeamTimeLogExportPart): void {
    if (this.downloadingUrl()) {
      return;
    }

    this.downloadingUrl.set(part.downloadUrl);
    this.error.set(null);
    this.api
      .downloadExport(part.downloadUrl, part.fileName)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.downloadingUrl.set(null)),
      )
      .subscribe({
        next: (download) => this.saveDownload(download),
        error: (error: unknown) => this.error.set(this.errorMessage(error)),
      });
  }

  private history(): readonly ManagerAssistantHistoryMessage[] {
    return this.messages()
      .filter((message) => message.id !== 'welcome')
      .slice(-8)
      .map((message) => ({ role: message.role, text: message.text }));
  }

  private saveDownload(download: DownloadResponse): void {
    const url = URL.createObjectURL(download.blob);
    this.activeObjectUrls.add(url);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = download.fileName;
    anchor.hidden = true;
    document.body.append(anchor);
    anchor.click();
    anchor.remove();
    queueMicrotask(() => {
      URL.revokeObjectURL(url);
      this.activeObjectUrls.delete(url);
    });
  }

  private errorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse && this.isApiError(error.error)) {
      return error.error.message;
    }
    return 'The team assistant is temporarily unavailable. Please try again.';
  }

  private isApiError(value: unknown): value is ApiError {
    return (
      typeof value === 'object' &&
      value !== null &&
      'message' in value &&
      typeof value.message === 'string'
    );
  }
}
