import { HttpResponse } from '@angular/common/http';

export interface DownloadResponse {
  blob: Blob;
  fileName: string;
}

export function mapDownloadResponse(
  response: HttpResponse<Blob>,
  fallbackFileName: string,
): DownloadResponse {
  return {
    blob: response.body ?? new Blob(),
    fileName: safeFileName(
      fileNameFromContentDisposition(response.headers.get('content-disposition')),
      fallbackFileName,
    ),
  };
}

function fileNameFromContentDisposition(value: string | null): string | null {
  if (!value) {
    return null;
  }

  const encoded = /filename\*=UTF-8''([^;]+)/i.exec(value)?.[1];
  if (encoded) {
    try {
      return decodeURIComponent(encoded.trim());
    } catch {
      return null;
    }
  }

  return /filename="?([^";]+)"?/i.exec(value)?.[1]?.trim() ?? null;
}

function safeFileName(value: string | null, fallback: string): string {
  const name = value
    ?.split(/[\\/]/)
    .at(-1)
    ?.replace(/[\u0000-\u001f\u007f]/g, '')
    .trim();
  return name && name.toLowerCase().endsWith('.xlsx') ? name : fallback;
}
