export interface RemoteStatusPayload {
  status: string;
}

export async function loadRemoteStatus(endpoint: string): Promise<string> {
  const response = await fetch(endpoint);
  const payload = await response.json() as RemoteStatusPayload;
  return payload.status;
}
