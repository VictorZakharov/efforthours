import express from "express";
import helmet from "helmet";
import passport from "passport";

const app = express();
app.use(helmet());
app.get("/status", passport.authenticate("jwt"), (_request, response) => {
  response.json({ status: "ok" });
});

export default app;
