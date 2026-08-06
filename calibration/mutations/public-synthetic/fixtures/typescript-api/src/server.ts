import { formatStatus } from "./status.js";

type Handler = () => { status: string };

const app = {
  get(path: string, handler: Handler): { path: string; handler: Handler } {
    return { path, handler };
  },
};

app.get("/status", () => ({ status: formatStatus({ value: "ready" }) }));

export { app };
