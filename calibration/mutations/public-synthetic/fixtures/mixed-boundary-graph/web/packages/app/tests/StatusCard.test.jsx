import { render, screen } from "@testing-library/react";

test("renders API readiness", () => {
  render(StatusCard({ healthy: true }));
  expect(screen.getByRole("article")).not.toBeNull();
});
