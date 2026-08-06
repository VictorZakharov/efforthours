import { formatStatus } from "./status.js";

const app = {
  get(path, handler) {
    return { path, handler };
  },
};

app.get("/status", () => ({ status: formatStatus("ready") }));

export { app };
