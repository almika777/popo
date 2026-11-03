const apiBaseUrl = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5109/api";

export type IssuerLogo = {
  emitterId: number;
  title: string;
  hasLogo: boolean;
};

async function ensureSuccess(response: Response): Promise<void> {
  if (response.ok) return;
  const message = await response.text();
  throw new Error(message && !message.trimStart().startsWith("<") ? message : `Ошибка запроса: ${response.status}`);
}

export const issuerLogosApi = {
  imageUrl(emitterId: number): string {
    return `${apiBaseUrl}/issuer-logos/${emitterId}/image`;
  },

  async getIssuers(signal?: AbortSignal): Promise<IssuerLogo[]> {
    let response: Response;
    try {
      response = await fetch(`${apiBaseUrl}/issuer-logos`, { signal });
    } catch (error) {
      if (error instanceof DOMException && error.name === "AbortError") throw error;
      throw new Error("Не удалось подключиться к серверу.");
    }
    await ensureSuccess(response);
    return await response.json() as IssuerLogo[];
  },

  async upload(emitterId: number, file: File): Promise<void> {
    const formData = new FormData();
    formData.append("EmitterId", String(emitterId));
    formData.append("File", file);

    let response: Response;
    try {
      response = await fetch(`${apiBaseUrl}/issuer-logos`, { method: "PUT", body: formData });
    } catch {
      throw new Error("Не удалось подключиться к серверу.");
    }
    await ensureSuccess(response);
  }
};
