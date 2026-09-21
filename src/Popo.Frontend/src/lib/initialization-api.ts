const apiBaseUrl = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5109/api";

export type InitializationJobStatus = "Pending" | "Running" | "Succeeded" | "Failed";

export type InitializationJobState = {
  jobKey: string;
  displayName: string;
  status: InitializationJobStatus;
  processedItems: number;
  totalItems: number | null;
  progressPercent: number | null;
  phase: string | null;
  attempts: number;
  startedAt: string | null;
  completedAt: string | null;
  lastError: string | null;
  updatedAt: string;
};

export type InitializationStatus = {
  version: string;
  isVisible: boolean;
  isComplete: boolean;
  jobs: InitializationJobState[];
};

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl}${path}`, {
      ...init,
      headers: { "Content-Type": "application/json", ...(init?.headers ?? {}) }
    });
  } catch {
    throw new Error("Не удалось подключиться к серверу.");
  }
  if (!response.ok) {
    const message = await response.text();
    throw new Error(message && !message.trimStart().startsWith("<") ? message : `Ошибка запроса: ${response.status}`);
  }
  try {
    return await response.json() as T;
  } catch {
    throw new Error("Сервер вернул некорректный ответ.");
  }
}

export const initializationApi = {
  getStatus: () => request<InitializationStatus>("/initialization/status"),
  retryJob: (jobKey: string) => request<InitializationStatus>(`/initialization/jobs/${encodeURIComponent(jobKey)}/retry`, { method: "POST" })
};
