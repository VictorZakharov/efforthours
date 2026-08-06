// @generated
export interface GeneratedStatusPayload {
  status: string;
  updatedAt: string;
}

export interface GeneratedStatusClient {
  load(): Promise<GeneratedStatusPayload>;
  refresh(): Promise<GeneratedStatusPayload>;
}
