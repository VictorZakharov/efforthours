import { render, screen } from "@testing-library/react";
import { axe } from "jest-axe";

test("exposes the summary as a named region", () => {
  render(Card());
  expect(screen.getByRole("region", { name: "Summary" })).not.toBeNull();
});

test("has no detected accessibility violations", async () => {
  const view = render(Card());
  expect(await axe(view.container)).toHaveNoViolations();
  expect(screen.getByLabelText("Summary")).not.toBeNull();
});
