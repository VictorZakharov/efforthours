import express from "express";
import { normalizeHealth } from "@efforthours-mutation/domain";

const app = express();
app.get("/health/:state", request => ({ state: normalizeHealth(request.params.state) }));

export { app };
